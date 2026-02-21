using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Services.Ml
{
    public class PythonServerHostedService(IOptions<MlServiceOptions> options, ILogger<PythonServerHostedService> logger) : IHostedService, IDisposable
    {
        private readonly MlServiceOptions _options = options.Value;
        private readonly ILogger<PythonServerHostedService> _logger = logger;
        private Process? _process;
        private bool _disposed;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.AutoStartPythonServer) return Task.CompletedTask;

            try
            {
                var workingDir = _options.PythonProjectDirectory;
                var startInfo = new ProcessStartInfo
                {
                    FileName = _options.PythonExecutablePath,
                    Arguments = _options.PythonScriptPath,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _process = new Process { StartInfo = startInfo };

                _process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogInformation("[ML-Server] {Data}", e.Data);
                };

                _process.ErrorDataReceived += (sender, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;

                    if (e.Data.Contains("INFO:", StringComparison.OrdinalIgnoreCase))
                        _logger.LogInformation("[ML-Server] {Data}", e.Data);
                    else
                        _logger.LogWarning("[ML-Server] {Data}", e.Data);
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Python ML server.");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_process is { HasExited: false })
            {
                try
                {
                    _process.Kill(true);
                    _process.WaitForExit(2000);
                }
                catch { /* Ignore */ }
            }
            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing) _process?.Dispose();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}