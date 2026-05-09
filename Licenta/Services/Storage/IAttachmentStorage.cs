using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Services.Storage
{
    public interface IAttachmentStorage
    {
        Task<string> SaveAsync(Stream source, string originalFileName, CancellationToken ct = default);
        string NormalizeLegacyPath(string? rawPath);
        string GetAbsolutePath(string storedPath);
        bool Exists(string storedPath);
        Stream OpenRead(string storedPath);
    }
}