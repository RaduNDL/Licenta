using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Licenta.Models;
using Microsoft.Extensions.Configuration;

namespace Licenta.Services.Ml
{
    public class MlLabResultClient : IMlLabResultClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

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
            var url = $"{_baseUrl.TrimEnd('/')}/api/lab/analyze";

            using var form = new MultipartFormDataContent();

            form.Add(new StringContent(labResult.Id.ToString()), "lab_result_id");
            form.Add(new StringContent(labResult.PatientId.ToString()), "patient_id");

            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            form.Add(fileContent, "file", fileName);

            using var response = await _httpClient.PostAsync(url, form, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LabResultPredictionResponse>(
                cancellationToken: cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("ML service returned empty response.");
            }

            return result;
        }
    }
}
