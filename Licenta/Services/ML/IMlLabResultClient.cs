using Licenta.Models;
using Licenta.Services.Ml.Dtos;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Services.Ml
{
    public interface IMlLabResultClient
    {
        Task<MlStatusResponse> GetStatusAsync(CancellationToken ct = default);

        Task<LabAnalyzeResponse> AnalyzeLabResultAsync(
            LabResult lab,
            Stream fileStream,
            string fileName,
            string? contentType,
            CancellationToken ct = default);
    }
}
