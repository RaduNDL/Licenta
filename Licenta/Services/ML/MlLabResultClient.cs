using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Licenta.Models;
using Licenta.Services.Ml.Dtos;
using Microsoft.Extensions.Configuration;

namespace Licenta.Services.Ml
{
    public class MlLabResultClient : IMlLabResultClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private const string DefaultModelId = "cbis_ddsm_images:torch_cnn";

        public MlLabResultClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _baseUrl = config["MlService:BaseUrl"]
                ?? throw new InvalidOperationException("MlService:BaseUrl is not configured.");
        }

        public async Task<LabResultPredictionResponse> AnalyzeLabResultAsync(
            LabResult labResult,
            Stream fileStream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var url = $"{_baseUrl.TrimEnd('/')}/api/lab/analyze?model_id={Uri.EscapeDataString(DefaultModelId)}&image_size=224";

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(labResult.Id.ToString()), "lab_result_id");
            form.Add(new StringContent(labResult.PatientId.ToString()), "patient_id");

            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(fileContent, "file", fileName);

            using var resp = await _httpClient.PostAsync(url, form, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync<LabResultPredictionResponse>(cancellationToken: cancellationToken);
            if (result == null)
                throw new InvalidOperationException("ML returned an empty response.");

            if (string.IsNullOrWhiteSpace(result.ModelId))
                result.ModelId = DefaultModelId;

            if ((!result.Probability.HasValue || result.Probability.Value <= 0f) && result.ProbaMalignant.HasValue)
                result.Probability = result.ProbaMalignant.Value;

            return result;
        }
    }
}