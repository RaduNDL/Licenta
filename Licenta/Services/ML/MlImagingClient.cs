using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Licenta.Services.Ml.Dtos;

namespace Licenta.Services.Ml
{
    public sealed class MlImagingClient(HttpClient http) : IMlImagingClient
    {
        private readonly HttpClient _http = http;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public async Task<MlStatusResponse> GetStatusAsync(CancellationToken ct)
        {
            using var resp = await _http.GetAsync("api/status", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"ML status failed ({(int)resp.StatusCode}): {body}");

            var dto = JsonSerializer.Deserialize<MlStatusResponse>(body, JsonOpts);
            return dto ?? new MlStatusResponse { Ok = false, Message = "Empty ML status response" };
        }

        public async Task<ImagingPredictResponse> PredictImagingAsync(
            Stream file,
            string fileName,
            string contentType,
            string modelId,
            int imageSize,
            bool requireQuality,
            bool requireDomain,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(file);

            if (file.CanSeek) file.Position = 0;

            fileName = string.IsNullOrWhiteSpace(fileName) ? "upload.bin" : fileName;
            contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            modelId ??= "";
            if (imageSize <= 0) imageSize = 224;

            using var form = new MultipartFormDataContent();

            var sc = new StreamContent(file);
            sc.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(sc, "file", fileName);

            form.Add(new StringContent(modelId, Encoding.UTF8), "model_id");
            form.Add(new StringContent(imageSize.ToString(), Encoding.UTF8), "image_size");
            form.Add(new StringContent(requireQuality ? "1" : "0", Encoding.UTF8), "require_quality");
            form.Add(new StringContent(requireDomain ? "1" : "0", Encoding.UTF8), "require_domain");

            using var resp = await _http.PostAsync("api/imaging/predict", form, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 422)
            {
                return JsonSerializer.Deserialize<ImagingPredictResponse>(body, JsonOpts)
                       ?? throw new Exception("ML predict returned empty JSON.");
            }

            throw new Exception($"ML predict failed ({(int)resp.StatusCode}): {body}");
        }

        public async Task<GroundTruthResponse> GetGroundTruthAsync(string fileNameOrPath, CancellationToken ct)
        {
            fileNameOrPath ??= "";
            var url = "api/imaging/ground_truth?filename=" + Uri.EscapeDataString(fileNameOrPath);

            using var resp = await _http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"ML ground truth failed ({(int)resp.StatusCode}): {body}");

            var dto = JsonSerializer.Deserialize<GroundTruthResponse>(body, JsonOpts);
            return dto ?? new GroundTruthResponse();
        }
    }
}