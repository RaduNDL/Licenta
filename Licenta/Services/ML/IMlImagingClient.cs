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
        Stream file,
        string fileName,
        string contentType,
        string modelId,
        int imageSize,
        bool requireQuality,
        bool requireDomain,
        CancellationToken ct);

    Task<GroundTruthResponse> GetGroundTruthAsync(string fileNameOrPath, CancellationToken ct);
}
}
