using System.Threading.Tasks;

namespace Licenta.Services.Ml
{
    public interface IBreastCancerClient
    {
        Task<BreastPredictionResponse> PredictAsync(BreastCancerRequest request);
    }
}
