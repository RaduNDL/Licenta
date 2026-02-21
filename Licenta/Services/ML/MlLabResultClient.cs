using Licenta.Models;
using Licenta.Services.Ml.Dtos;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Services.Ml
{
    public class MlLabResultClient : IMlLabResultClient
    {
        private readonly HttpClient _http;
        private readonly MlServiceOptions _opt;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public MlLabResultClient(HttpClient http, IOptions<MlServiceOptions> opt)
        {
            _http = http;
            _opt = opt.Value;
        }

        public async Task<MlStatusResponse> GetStatusAsync(CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_opt.TimeoutSeconds, 2, 15)));

            using var resp = await _http.GetAsync("/api/status", cts.Token);
            var json = await resp.Content.ReadAsStringAsync(cts.Token);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"ML error ({(int)resp.StatusCode}): {json}");

            var parsed = JsonSerializer.Deserialize<MlStatusResponse>(json, JsonOpts);
            if (parsed == null)
                throw new InvalidOperationException("Invalid ML response JSON.");

            return parsed;
        }

        public async Task<LabAnalyzeResponse> AnalyzeLabResultAsync(
            LabResult lab,
            Stream fileStream,
            string fileName,
            string? contentType,
            CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(20, Math.Clamp(_opt.TimeoutSeconds, 2, 120))));

            if (fileStream.CanSeek)
                fileStream.Position = 0;

            using var form = new MultipartFormDataContent();

            var sc = new StreamContent(fileStream);
            sc.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(sc, "file", fileName);

            form.Add(new StringContent(lab.Id.ToString()), "lab_result_id");
            form.Add(new StringContent(lab.PatientId.ToString()), "patient_id");
            form.Add(new StringContent(fileName ?? ""), "file_name");
            form.Add(new StringContent(contentType ?? ""), "content_type");

            using var resp = await _http.PostAsync("/api/lab/analyze", form, cts.Token);
            var json = await resp.Content.ReadAsStringAsync(cts.Token);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"ML error ({(int)resp.StatusCode}): {json}");

            var parsed = JsonSerializer.Deserialize<LabAnalyzeResponse>(json, JsonOpts);
            if (parsed == null)
                throw new InvalidOperationException("Invalid ML response JSON.");

            return parsed;
        }
    }
}
