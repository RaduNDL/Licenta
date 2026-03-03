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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Licenta.Pages.Doctor.Messages;

[Authorize(Roles = "Doctor")]
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

    public record ConversationVm(
        string PartnerId,
        string PartnerName,
        DateTime LastAt,
        string LastPreview,
        string ConversationKey,
        bool IsActive);

    public record MessageVm(
        Guid Id,
        string Body,
        DateTime SentAtUtc,
        DateTime SentAtLocal,
        bool IsMine);

    public record AssistantVm(string Id, string Name);

    public class InputModel
    {
        public Guid RequestId { get; set; }
        public string NewMessageBody { get; set; } = "";
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public List<ConversationVm> Conversations { get; set; } = new();
    public List<MessageVm> Messages { get; set; } = new();
    public List<AssistantVm> Assistants { get; set; } = new();

    public string? SelectedPartnerId { get; set; }
    public string SelectedPartnerName { get; set; } = "";
    public string CurrentConversationKey { get; set; } = "";

    public Guid SelectedRequestId { get; set; }
    public bool CanSend { get; set; }
    public bool CanClose { get; set; }

    private static string ConversationKey(string doctorId, string patientId) => $"chat:doctor:{doctorId}:patient:{patientId}";

    public async Task<IActionResult> OnGetAsync(string? partnerId)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return Page();

        var doctorProfile = await _db.Doctors
            .Include(d => d.Assistants)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);

        if (doctorProfile == null) return Page();

        if (doctorProfile.Assistants != null)
        {
            Assistants = doctorProfile.Assistants
                .Where(a => !a.IsSoftDeleted)
                .Select(a => new AssistantVm(a.Id, a.FullName ?? a.Email ?? "Assistant"))
                .ToList();
        }

        var hiddenPartners = await GetHiddenChatsForUser(doctorUser.Id);

        if (!string.IsNullOrEmpty(partnerId) && hiddenPartners.Contains(partnerId))
        {
            return RedirectToPage("/Doctor/Messages/Inbox");
        }

        var doctorMessages = await _db.InternalMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m => m.SenderId == doctorUser.Id || m.RecipientId == doctorUser.Id)
            .ToListAsync();

        var activePartnerIds = await _db.PatientMessageRequests
            .Where(r => r.DoctorProfileId == doctorProfile.Id && r.Status == PatientMessageRequestStatus.ActiveDoctorChat)
            .Select(r => r.Patient.UserId)
            .ToListAsync();

        var groupedByPartner = doctorMessages
            .GroupBy(m => m.SenderId == doctorUser.Id ? m.RecipientId : m.SenderId)
            .Where(g => !hiddenPartners.Contains(g.Key))
            .ToList();

        foreach (var group in groupedByPartner)
        {
            var pId = group.Key;
            var lastMsg = group.OrderByDescending(m => m.SentAt).First();

            var partnerName = lastMsg.SenderId == doctorUser.Id
                ? (lastMsg.Recipient?.FullName ?? lastMsg.Recipient?.Email ?? "Patient")
                : (lastMsg.Sender?.FullName ?? lastMsg.Sender?.Email ?? "Patient");

            var preview = lastMsg.Body ?? "";
            if (preview.Length > 60) preview = preview[..60] + "...";

            bool isActive = activePartnerIds.Contains(pId);

            Conversations.Add(new ConversationVm(
                pId,
                partnerName,
                lastMsg.SentAt,
                preview,
                ConversationKey(doctorUser.Id, pId),
                isActive));
        }

        Conversations = Conversations.OrderByDescending(x => x.LastAt).ToList();

        SelectedPartnerId = partnerId ?? Conversations.FirstOrDefault()?.PartnerId;

        if (string.IsNullOrEmpty(SelectedPartnerId)) return Page();

        var currentPartner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == SelectedPartnerId);
        SelectedPartnerName = currentPartner?.FullName ?? currentPartner?.Email ?? "Patient";
        CurrentConversationKey = ConversationKey(doctorUser.Id, SelectedPartnerId);

        var activeRequest = await _db.PatientMessageRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(r => r.DoctorProfileId == doctorProfile.Id
                                      && r.Patient.UserId == SelectedPartnerId
                                      && r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

        if (activeRequest != null)
        {
            CanSend = true;
            CanClose = true;
            SelectedRequestId = activeRequest.Id;
            Input.RequestId = activeRequest.Id;
        }
        else
        {
            CanSend = false;
            CanClose = false;
        }

        Messages = await _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                (m.SenderId == doctorUser.Id && m.RecipientId == SelectedPartnerId) ||
                (m.SenderId == SelectedPartnerId && m.RecipientId == doctorUser.Id)
            )
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageVm(
                m.Id,
                m.Body,
                m.SentAt,
                m.SentAt.ToLocalTime(),
                m.SenderId == doctorUser.Id))
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null || string.IsNullOrWhiteSpace(Input.NewMessageBody))
            return new JsonResult(new { ok = false });

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == Input.RequestId && r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

        if (req == null)
            return new JsonResult(new { ok = false });

        var patientUserId = req.Patient.UserId;

        var msg = new InternalMessage
        {
            Id = Guid.NewGuid(),
            SenderId = doctorUser.Id,
            RecipientId = patientUserId,
            Subject = $"REQ:{req.Id}",
            Body = Input.NewMessageBody.Trim(),
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.InternalMessages.Add(msg);
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var patientUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == patientUserId);
        if (patientUser != null)
        {
            await _notifications.NotifyAsync(
                patientUser,
                NotificationType.Message,
                "New Message",
                $"You received a new message from Dr. {doctorUser.FullName ?? doctorUser.Email}.",
                actionUrl: $"/Patient/Messages/Inbox?partnerId={doctorUser.Id}&kind=Doctor",
                actionText: "Reply",
                relatedEntity: "InternalMessage",
                relatedEntityId: msg.Id.ToString()
            );
        }

        var payload = new
        {
            conversationKey = ConversationKey(doctorUser.Id, patientUserId),
            requestId = req.Id,
            messageId = msg.Id,
            senderId = msg.SenderId,
            partnerId = patientUserId,
            body = msg.Body,
            sentAtUtc = msg.SentAt
        };

        await _hub.Clients.Users(new[] { patientUserId, doctorUser.Id }).SendAsync("message:new", payload);

        return new JsonResult(new { ok = true, messageId = msg.Id, sentAtUtc = msg.SentAt });
    }

    public async Task<IActionResult> OnGetSync(string conversationKey, string? after)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null)
            return new JsonResult(new { messages = Array.Empty<object>() });

        if (string.IsNullOrWhiteSpace(conversationKey))
            return new JsonResult(new { messages = Array.Empty<object>() });

        var parts = conversationKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
            return new JsonResult(new { messages = Array.Empty<object>() });

        var partnerId = parts[4];

        DateTime? afterUtc = null;
        if (!string.IsNullOrWhiteSpace(after) &&
            DateTimeOffset.TryParse(after, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
        {
            afterUtc = dto.UtcDateTime;
        }

        var messages = await _db.InternalMessages
            .AsNoTracking()
            .Where(m =>
                (afterUtc == null || m.SentAt > afterUtc.Value) &&
                (
                    (m.SenderId == doctorUser.Id && m.RecipientId == partnerId) ||
                    (m.SenderId == partnerId && m.RecipientId == doctorUser.Id)
                ))
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                messageId = m.Id,
                body = m.Body,
                sentAtUtc = m.SentAt,
                senderId = m.SenderId
            })
            .ToListAsync();

        return new JsonResult(new { messages });
    }

    public async Task<IActionResult> OnPostDelegateAsync(Guid id, string assistantId)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return Unauthorized();

        var doctorProfile = await _db.Doctors
            .Include(d => d.Assistants)
            .FirstOrDefaultAsync(d => d.UserId == doctorUser.Id);

        if (doctorProfile == null || !doctorProfile.Assistants.Any(a => !a.IsSoftDeleted))
        {
            TempData["StatusMessage"] = "You have no assistants assigned.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(assistantId))
        {
            TempData["StatusMessage"] = "Please select an assistant.";
            return RedirectToPage();
        }

        var chosen = doctorProfile.Assistants.FirstOrDefault(a => a.Id == assistantId && !a.IsSoftDeleted);
        if (chosen == null)
        {
            TempData["StatusMessage"] = "Invalid assistant selection.";
            return RedirectToPage();
        }

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == id && r.DoctorProfileId == doctorProfile.Id && r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

        if (req == null) return NotFound();

        req.AssistantId = chosen.Id;
        req.Status = PatientMessageRequestStatus.Pending;
        req.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _notifications.NotifyAsync(
            chosen,
            NotificationType.System,
            "New Delegated Request",
            $"Dr. {doctorUser.FullName ?? doctorUser.Email} has delegated an active chat to you.",
            actionUrl: "/Assistant/Messages/Requests/Index",
            actionText: "View Request",
            relatedEntity: "PatientMessageRequest",
            relatedEntityId: req.Id.ToString()
        );

        var patientUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.Patient.UserId);
        if (patientUser != null)
        {
            await _notifications.NotifyAsync(
                patientUser,
                NotificationType.System,
                "Chat Delegated",
                $"Dr. {doctorUser.FullName ?? doctorUser.Email} has delegated the conversation to an assistant.",
                actionUrl: $"/Patient/Messages/Inbox?partnerId={chosen.Id}&kind=Assistant",
                actionText: "Open Chat",
                relatedEntity: "PatientMessageRequest",
                relatedEntityId: req.Id.ToString(),
                sendEmail: false
            );
        }

        TempData["StatusMessage"] = "Chat delegated to assistant.";
        return RedirectToPage("/Doctor/Messages/Inbox");
    }

    public async Task<IActionResult> OnPostCloseAsync(Guid id)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null) return Unauthorized();

        var doctorProfileId = await _db.Doctors
            .AsNoTracking()
            .Where(d => d.UserId == doctorUser.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        if (doctorProfileId == null) return Unauthorized();

        var req = await _db.PatientMessageRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == id && r.DoctorProfileId == doctorProfileId.Value && r.Status == PatientMessageRequestStatus.ActiveDoctorChat);

        if (req != null)
        {
            req.Status = PatientMessageRequestStatus.Closed;
            req.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var patientUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.Patient.UserId);
            if (patientUser != null)
            {
                await _notifications.NotifyAsync(
                    patientUser,
                    NotificationType.System,
                    "Conversation Closed",
                    $"Your conversation with Dr. {doctorUser.FullName ?? doctorUser.Email} has been closed.",
                    actionUrl: $"/Patient/Messages/Inbox?partnerId={doctorUser.Id}&kind=Doctor",
                    actionText: "View History",
                    relatedEntity: "PatientMessageRequest",
                    relatedEntityId: req.Id.ToString()
                );
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostHideChatAsync(string partnerIdToHide)
    {
        var doctorUser = await _userManager.GetUserAsync(User);
        if (doctorUser == null || string.IsNullOrEmpty(partnerIdToHide)) return Unauthorized();

        var log = new UserActivityLog
        {
            UserId = doctorUser.Id,
            Action = "HideConversation",
            EntityName = "ApplicationUser",
            EntityId = partnerIdToHide,
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new { hiddenFromInbox = true })
        };

        _db.UserActivityLogs.Add(log);
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = "Conversation hidden from your view.";
        return RedirectToPage("/Doctor/Messages/Inbox");
    }

    private async Task<List<string>> GetHiddenChatsForUser(string userId)
    {
        return await _db.UserActivityLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.Action == "HideConversation" && l.EntityName == "ApplicationUser")
            .Select(l => l.EntityId)
            .ToListAsync();
    }
}