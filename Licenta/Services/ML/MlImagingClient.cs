using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Licenta.Services.Ml.Dtos;
using Microsoft.Extensions.Configuration;

namespace Licenta.Services.Ml
{
    public class MlImagingClient : IMlImagingClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public MlImagingClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _baseUrl = config["MlService:BaseUrl"] ?? "http://localhost:8002";
        }

        public async Task<MlStatusResponse> GetStatusAsync(CancellationToken ct)
        {
            var resp = await _httpClient.GetAsync($"{_baseUrl}/api/status", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<MlStatusResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new MlStatusResponse();
        }

        public async Task<ImagingPredictResponse> PredictImagingAsync(Stream fileStream, string fileName, string contentType, string modelId, int imageSize, CancellationToken ct)
        {
            var url = $"{_baseUrl}/api/imaging/predict?image_size={imageSize}";
            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", fileName);

            using var resp = await _httpClient.PostAsync(url, form, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"ML Error ({resp.StatusCode}): {body}");

            return JsonSerializer.Deserialize<ImagingPredictResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ImagingPredictResponse();
        }
    }
}