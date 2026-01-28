using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Licenta.Services.Ml.Dtos;

namespace Licenta.Services.Ml
{
    public interface IMlImagingClient
    {
        Task<MlStatusResponse> GetStatusAsync(CancellationToken ct);

        Task<ImagingPredictResponse> PredictImagingAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            string modelId,
            int imageSize,
            CancellationToken ct);
    }
}