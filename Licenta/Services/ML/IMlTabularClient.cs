using System.Collections.Generic;
using System.Threading.Tasks;
using Licenta.Services.Ml.Dtos;

namespace Licenta.Services.Ml
{
    public interface IMlTabularClient
    {
        Task<TabularFeaturesResponse> GetFeaturesAsync(string? modelId = null);
        Task<TabularPredictResponse> PredictAsync(Dictionary<string, float> features, string? modelId = null);
        Task<TabularPredictResponse> PredictAsync(TabularPredictRequest tabularPredictRequest);
    }
}