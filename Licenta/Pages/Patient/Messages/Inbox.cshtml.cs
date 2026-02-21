using Licenta.Areas.Identity.Data;
using Licenta.Models;
using Licenta.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Licenta.Pages.Patient.Messages
{
    [Authorize(Roles = "Patient")]
    public class InboxModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly INotificationService _notifications;

        public InboxModel(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IHubContext<NotificationHub> hub,
            INotificationService notifications)
        {
            _db = db;
            _userManager = userManager;
            _hub = hub;
            _notifications = notifications;
        }

        [BindProperty(SupportsGet = true)]
        public Guid requestId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? kind { get; set; }

        public Guid SelectedRequestId { get; set; } = Guid.Empty;
        public string SelectedKind { get; set; } = "";
        public string SelectedUserName { get; set; } = "";
        public string SelectedSubtitle { get; set; } = "";
        public bool CanSend { get; set; }
        public string CurrentConversationKey { get; set; } = "";

        public List<ConversationVm> Conversations { get; set; } = new();
        public List<MessageVm> Messages { get; set; } = new();

        public class ConversationVm
        {
            public Guid RequestId { get; set; }
            public string Kind { get; set; } = "";
            public string PartnerName { get; set; } = "";
            public string LastMessagePreview { get; set; } = "";
        }

        public class MessageVm
        {
            public bool IsMine { get; set; }
            public string Body { get; set; } = "";
            public DateTime SentAtLocal { get; set; }
        }

        public class InputModel
        {
            [Required]
            public Guid RequestId { get; set; }

            [Required, MaxLength(50)]
            public string Kind { get; set; } = "";

            [Required, MaxLength(2000)]
            public string NewMessageBody { get; set; } = "";
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        private static string ThreadKey(Guid requestId) => $"REQ:{requestId}";

        private bool IsAjax()
        {
            var xrw = Request.Headers["X-Requested-With"].ToString();
            if (string.Equals(xrw, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)) return true;

            var accept = Request.Headers["Accept"].ToString();
            if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static string NormalizeKind(string? k)
        {
            if (string.IsNullOrWhiteSpace(k)) return "Assistant";
            if (k.Equals("assistant", StringComparison.OrdinalIgnoreCase)) return "Assistant";
            if (k.Equals("doctor", StringComparison.OrdinalIgnoreCase)) return "Doctor";
            return k.Trim();
        }

        private static bool CanPatientSendForKind(PatientMessageRequestStatus status, string kind)
        {
            if (status == PatientMessageRequestStatus.Closed) return false;

            if (kind == "Assistant")
                return status == PatientMessageRequestStatus.AssistantChat;

            if (kind == "Doctor")
                return status == PatientMessageRequestStatus.WaitingDoctorApproval
                    || status == PatientMessageRequestStatus.ActiveDoctorChat;

            return false;
        }

        private async Task<ApplicationUser?> CurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task LoadConversationsAsync(ApplicationUser patient)
        {
            var reqs = await _db.PatientMessageRequests
                .AsNoTracking()
                .Where(r => r.PatientId == patient.Id)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ToListAsync();

            var partnerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in reqs)
            {
                if (!string.IsNullOrWhiteSpace(r.AssistantId)) partnerIds.Add(r.AssistantId);
                if (!string.IsNullOrWhiteSpace(r.DoctorId)) partnerIds.Add(r.DoctorId);
            }

            var partners = await _db.Users
                .AsNoTracking()
                .Where(u => partnerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
                .ToListAsync();

            var partnerMap = partners.ToDictionary(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(x.FullName) ? (x.Email ?? x.UserName ?? "User") : x.FullName);

            var convs = new List<ConversationVm>();

            foreach (var r in reqs)
            {
                var thread = ThreadKey(r.Id);

                if (!string.IsNullOrWhiteSpace(r.AssistantId))
                {
                    var last = await _db.InternalMessages
                        .AsNoTracking()
                        .Where(m => m.Subject == thread &&
                                    ((m.SenderId == patient.Id && m.RecipientId == r.AssistantId) ||
                                     (m.SenderId == r.AssistantId && m.RecipientId == patient.Id)))
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefaultAsync();

                    var preview = last?.Body ?? r.Subject ?? "";
                    if (preview.Length > 40) preview = preview[..40] + "...";

                    convs.Add(new ConversationVm
                    {
                        RequestId = r.Id,
                        Kind = "Assistant",
                        PartnerName = partnerMap.TryGetValue(r.AssistantId, out var n) ? n : "Assistant",
                        LastMessagePreview = preview
                    });
                }

                if (!string.IsNullOrWhiteSpace(r.DoctorId))
                {
                    var last = await _db.InternalMessages
                        .AsNoTracking()
                        .Where(m => m.Subject == thread &&
                                    ((m.SenderId == patient.Id && m.RecipientId == r.DoctorId) ||
                                     (m.SenderId == r.DoctorId && m.RecipientId == patient.Id)))
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefaultAsync();

                    var preview = last?.Body ?? r.Subject ?? "";
                    if (preview.Length > 40) preview = preview[..40] + "...";

                    convs.Add(new ConversationVm
                    {
                        RequestId = r.Id,
                        Kind = "Doctor",
                        PartnerName = partnerMap.TryGetValue(r.DoctorId, out var n) ? n : "Doctor",
                        LastMessagePreview = preview
                    });
                }
            }

            Conversations = convs;
        }

        private async Task<(PatientMessageRequest? req, string partnerId, ApplicationUser? partner)> LoadRequestAndPartnerAsync(ApplicationUser patient, Guid reqId, string k)
        {
            var req = await _db.PatientMessageRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == reqId && r.PatientId == patient.Id);

            if (req == null) return (null, "", null);

            var partnerId = k == "Doctor" ? (req.DoctorId ?? "") : (req.AssistantId ?? "");
            if (string.IsNullOrWhiteSpace(partnerId)) return (req, "", null);

            var partner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == partnerId);
            return (req, partnerId, partner);
        }

        private async Task LoadMessagesAsync(ApplicationUser patient, Guid reqId, string partnerId)
        {
            var thread = ThreadKey(reqId);

            Messages = await _db.InternalMessages
                .AsNoTracking()
                .Where(m => m.Subject == thread &&
                            ((m.SenderId == patient.Id && m.RecipientId == partnerId) ||
                             (m.SenderId == partnerId && m.RecipientId == patient.Id)))
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageVm
                {
                    IsMine = m.SenderId == patient.Id,
                    Body = m.Body,
                    SentAtLocal = m.SentAt.ToLocalTime()
                })
                .ToListAsync();

            var unread = await _db.InternalMessages
                .Where(m => m.Subject == thread &&
                            m.SenderId == partnerId &&
                            m.RecipientId == patient.Id &&
                            !m.IsRead)
                .ToListAsync();

            if (unread.Count > 0)
            {
                foreach (var m in unread) m.IsRead = true;
                await _db.SaveChangesAsync();
            }
        }

        private async Task<(Guid reqId, string kind, string body)> ReadPostPayloadAsync()
        {
            Guid reqId = Input?.RequestId ?? Guid.Empty;
            var k = Input?.Kind;
            var body = Input?.NewMessageBody;

            if (Request.HasFormContentType)
            {
                var f = Request.Form;

                if (reqId == Guid.Empty)
                {
                    var s = (string?)f["requestId"];
                    if (Guid.TryParse(s, out var g)) reqId = g;

                    s = (string?)f["Input.RequestId"];
                    if (reqId == Guid.Empty && Guid.TryParse(s, out g)) reqId = g;

                    s = (string?)f["RequestId"];
                    if (reqId == Guid.Empty && Guid.TryParse(s, out g)) reqId = g;
                }

                if (string.IsNullOrWhiteSpace(k))
                {
                    k = (string?)f["kind"];
                    if (string.IsNullOrWhiteSpace(k)) k = (string?)f["Input.Kind"];
                    if (string.IsNullOrWhiteSpace(k)) k = (string?)f["Kind"];
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    body = (string?)f["newMessageBody"];
                    if (string.IsNullOrWhiteSpace(body)) body = (string?)f["body"];
                    if (string.IsNullOrWhiteSpace(body)) body = (string?)f["message"];
                    if (string.IsNullOrWhiteSpace(body)) body = (string?)f["Input.NewMessageBody"];
                    if (string.IsNullOrWhiteSpace(body)) body = (string?)f["NewMessageBody"];
                }

                return (reqId, NormalizeKind(k), body ?? "");
            }

            var ct = Request.ContentType ?? "";
            if (ct.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (reqId == Guid.Empty)
                    {
                        if (root.TryGetProperty("requestId", out var p) && Guid.TryParse(p.ToString(), out var g)) reqId = g;
                        if (reqId == Guid.Empty && root.TryGetProperty("RequestId", out p) && Guid.TryParse(p.ToString(), out g)) reqId = g;
                        if (reqId == Guid.Empty && root.TryGetProperty("conversationId", out p) && Guid.TryParse(p.ToString(), out g)) reqId = g;
                    }

                    if (string.IsNullOrWhiteSpace(k))
                    {
                        if (root.TryGetProperty("kind", out var p)) k = p.ToString();
                        if (string.IsNullOrWhiteSpace(k) && root.TryGetProperty("Kind", out p)) k = p.ToString();
                    }

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        if (root.TryGetProperty("newMessageBody", out var p)) body = p.ToString();
                        if (string.IsNullOrWhiteSpace(body) && root.TryGetProperty("body", out p)) body = p.ToString();
                        if (string.IsNullOrWhiteSpace(body) && root.TryGetProperty("message", out p)) body = p.ToString();
                        if (string.IsNullOrWhiteSpace(body) && root.TryGetProperty("text", out p)) body = p.ToString();
                    }
                }

                return (reqId, NormalizeKind(k), body ?? "");
            }

            return (reqId, NormalizeKind(k), body ?? "");
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var patient = await CurrentUserAsync();
            if (patient == null) return Unauthorized();

            await LoadConversationsAsync(patient);

            if (requestId == Guid.Empty)
            {
                SelectedRequestId = Guid.Empty;
                SelectedKind = "";
                CanSend = false;
                CurrentConversationKey = "";
                Messages = new();
                return Page();
            }

            SelectedRequestId = requestId;
            SelectedKind = NormalizeKind(kind);

            var (req, partnerId, partner) = await LoadRequestAndPartnerAsync(patient, SelectedRequestId, SelectedKind);
            if (req == null) return NotFound();

            CanSend = !string.IsNullOrWhiteSpace(partnerId) && CanPatientSendForKind(req.Status, SelectedKind);
            CurrentConversationKey = $"patient:{SelectedRequestId}:{SelectedKind.ToLowerInvariant()}";
            SelectedUserName = partner?.FullName ?? partner?.Email ?? partner?.UserName ?? (SelectedKind == "Doctor" ? "Doctor" : "Assistant");
            SelectedSubtitle = SelectedKind;

            if (string.IsNullOrWhiteSpace(partnerId))
            {
                Messages = new();
                CanSend = false;
                return Page();
            }

            await LoadMessagesAsync(patient, SelectedRequestId, partnerId);

            Input.RequestId = SelectedRequestId;
            Input.Kind = SelectedKind;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var patient = await CurrentUserAsync();
            if (patient == null) return Unauthorized();

            var (reqId, k, bodyRaw) = await ReadPostPayloadAsync();
            var body = (bodyRaw ?? "").Trim();

            if (reqId == Guid.Empty)
                ModelState.AddModelError(string.Empty, "No conversation selected.");

            if (string.IsNullOrWhiteSpace(body))
                ModelState.AddModelError(nameof(Input.NewMessageBody), "Message cannot be empty.");

            var (req, partnerId, partner) = await LoadRequestAndPartnerAsync(patient, reqId, k);
            if (req == null)
                ModelState.AddModelError(string.Empty, "Request not found.");

            if (req != null && !CanPatientSendForKind(req.Status, k))
                ModelState.AddModelError(string.Empty, "Conversation is closed.");

            if (req != null && string.IsNullOrWhiteSpace(partnerId))
                ModelState.AddModelError(string.Empty, "Conversation partner not available.");

            if (!ModelState.IsValid)
            {
                if (IsAjax())
                {
                    var errors = ModelState
                        .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                        .ToDictionary(
                            x => x.Key,
                            x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    return new JsonResult(new { ok = false, errors }) { StatusCode = 400 };
                }

                return RedirectToPage("/Patient/Messages/Inbox", new { requestId = reqId, kind = k });
            }

            var msg = new InternalMessage
            {
                Id = Guid.NewGuid(),
                SenderId = patient.Id,
                RecipientId = partnerId,
                Subject = ThreadKey(req!.Id),
                Body = body,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.InternalMessages.Add(msg);
            await _db.SaveChangesAsync();

            var senderName = string.IsNullOrWhiteSpace(patient.FullName)
                ? (patient.Email ?? patient.UserName ?? "Patient")
                : patient.FullName;

            if (partner != null)
            {
                await _notifications.NotifyAsync(
                    partner,
                    NotificationType.Message,
                    "New message",
                    $"New message from patient <b>{senderName}</b>",
                    "Message",
                    req.Id.ToString()
                );
            }

            var payload = new
            {
                conversationKey = $"patient:{req.Id}:{k.ToLowerInvariant()}",
                messageId = msg.Id,
                senderId = msg.SenderId,
                body = msg.Body,
                sentAtUtc = msg.SentAt
            };

            await _hub.Clients.Group($"USER_{partnerId}").SendAsync("message:new", payload);
            await _hub.Clients.Group($"USER_{patient.Id}").SendAsync("message:new", payload);

            if (IsAjax())
            {
                return new JsonResult(new
                {
                    ok = true,
                    messageId = msg.Id,
                    sentAtUtc = msg.SentAt
                });
            }

            return RedirectToPage("/Patient/Messages/Inbox", new { requestId = req.Id, kind = k });
        }
    }
}
