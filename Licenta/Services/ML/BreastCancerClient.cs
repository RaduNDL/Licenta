using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Licenta.Services.Ml
{
    public class BreastCancerClient : IBreastCancerClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public BreastCancerClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["MlService:BaseUrl"]
                         ?? throw new InvalidOperationException("MlService:BaseUrl is not configured.");
        }

        public async Task<BreastPredictionResponse> PredictAsync(BreastCancerRequest request)
        {
            var url = $"{_baseUrl.TrimEnd('/')}/api/breast/analyze";

            var response = await _httpClient.PostAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BreastPredictionResponse>();
            if (result == null)
                throw new InvalidOperationException("Empty response from breast cancer ML service.");

            return result;
        }
    }
}