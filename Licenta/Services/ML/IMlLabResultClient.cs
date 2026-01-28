using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Licenta.Models;
using Licenta.Services.Ml.Dtos;

namespace Licenta.Services.Ml
{
    public interface IMlLabResultClient
    {
        Task<LabResultPredictionResponse> AnalyzeLabResultAsync(
            LabResult labResult,
            Stream fileStream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default);
    }
}