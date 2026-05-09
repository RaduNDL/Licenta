using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Services.Storage
{
    public sealed class AttachmentStorage : IAttachmentStorage
    {
        private readonly string _root;
        private readonly string _rootWithSep;

        public AttachmentStorage(IWebHostEnvironment env, IOptions<AttachmentStorageOptions> opt)
        {
            var configured = (opt.Value.RootPath ?? "").Trim();

            _root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "App_Data", "attachments")
                : (Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured));

            Directory.CreateDirectory(_root);

            var rootFull = Path.GetFullPath(_root);
            _rootWithSep = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
        }

        public async Task<string> SaveAsync(Stream source, string originalFileName, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(originalFileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";

            var year = DateTime.UtcNow.Year.ToString();
            var month = DateTime.UtcNow.Month.ToString("00");
            var dir = Path.Combine(_root, year, month);
            Directory.CreateDirectory(dir);

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var abs = Path.Combine(dir, fileName);

            await using (var fs = new FileStream(abs, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(fs, ct);
            }

            return $"{year}/{month}/{fileName}";
        }

        public string NormalizeLegacyPath(string? rawPath)
        {
            var p = (rawPath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p)) return string.Empty;

            if (Uri.TryCreate(p, UriKind.Absolute, out var absUri))
                p = absUri.AbsolutePath ?? string.Empty;

            var q = p.IndexOf('?');
            if (q >= 0) p = p.Substring(0, q);
            var h = p.IndexOf('#');
            if (h >= 0) p = p.Substring(0, h);

            p = p.Replace('\\', '/').Trim();

            if (p.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                p = p["/uploads/".Length..];
            else if (p.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                p = p["uploads/".Length..];
            else
                p = p.TrimStart('/');

            return string.IsNullOrWhiteSpace(p) ? string.Empty : p;
        }

        public string GetAbsolutePath(string storedPath)
        {
            var normalized = NormalizeLegacyPath(storedPath);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Stored path is empty.");

            var segments = normalized
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => s != "." && s != "..")
                .ToArray();

            if (segments.Length == 0)
                throw new InvalidOperationException("Stored path is invalid.");

            var candidate = Path.Combine(new[] { _root }.Concat(segments).ToArray());
            var full = Path.GetFullPath(candidate);

            if (!full.StartsWith(_rootWithSep, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(full, _root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path traversal blocked.");

            return full;
        }

        public bool Exists(string storedPath)
        {
            try
            {
                return File.Exists(GetAbsolutePath(storedPath));
            }
            catch
            {
                return false;
            }
        }

        public Stream OpenRead(string storedPath)
        {
            return new FileStream(GetAbsolutePath(storedPath), FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public bool TryDelete(string storedPath)
        {
            try
            {
                var abs = GetAbsolutePath(storedPath);
                if (!File.Exists(abs)) return false;
                File.Delete(abs);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}