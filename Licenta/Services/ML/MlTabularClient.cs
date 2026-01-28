using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Licenta.Services.Ml.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Licenta.Services.Ml
{
    public class MlTabularClient : IMlTabularClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<MlTabularClient> _logger;

        public MlTabularClient(HttpClient httpClient, IConfiguration config, ILogger<MlTabularClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = config["MlService:BaseUrl"]
                ?? throw new InvalidOperationException("MlService:BaseUrl is not configured.");
        }

        public async Task<TabularFeaturesResponse> GetFeaturesAsync(string? modelId = null)
        {
            var url = $"{_baseUrl.TrimEnd('/')}/api/tabular/features";
            if (!string.IsNullOrWhiteSpace(modelId))
                url += $"?model_id={Uri.EscapeDataString(modelId)}";

            try
            {
                var result = await _httpClient.GetFromJsonAsync<TabularFeaturesResponse>(url);
                if (result == null)
                    throw new InvalidOperationException("ML returned an empty features response.");
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to ML service at {BaseUrl}", _baseUrl);
                throw new InvalidOperationException($"Failed to connect to ML service at {_baseUrl}.", ex);
            }
        }

        public Task<TabularPredictResponse> PredictAsync(Dictionary<string, float> features, string? modelId = null)
        {
            if (features == null || features.Count == 0)
                throw new ArgumentException("Features dictionary cannot be null or empty.", nameof(features));

            return PredictAsync(new TabularPredictRequest
            {
                ModelId = modelId,
                Features = features,
                Vector = null
            });
        }

        public async Task<TabularPredictResponse> PredictAsync(TabularPredictRequest tabularPredictRequest)
        {
            if (tabularPredictRequest == null)
                throw new ArgumentNullException(nameof(tabularPredictRequest));

            var url = $"{_baseUrl.TrimEnd('/')}/api/tabular/predict";

            try
            {
                using var resp = await _httpClient.PostAsJsonAsync(url, tabularPredictRequest);
                resp.EnsureSuccessStatusCode();

                var result = await resp.Content.ReadFromJsonAsync<TabularPredictResponse>();
                if (result == null)
                    throw new InvalidOperationException("ML returned an empty prediction response.");

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to ML service at {BaseUrl}", _baseUrl);
                throw new InvalidOperationException($"Failed to connect to ML service at {_baseUrl}.", ex);
            }
        }
    }
}