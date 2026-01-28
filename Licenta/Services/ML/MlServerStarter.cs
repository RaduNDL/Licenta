using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Services.Ml
{
    public class MlServerStarter : IHostedService
    {
        private readonly ILogger<MlServerStarter> _logger;
        private readonly IConfiguration _config;
        private readonly IHostEnvironment _env;
        private Process? _process;

        public MlServerStarter(ILogger<MlServerStarter> logger, IConfiguration config, IHostEnvironment env)
        {
            _logger = logger;
            _config = config;
            _env = env;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_env.IsDevelopment())
                return Task.CompletedTask;

            var projectDir = _config["MlServer:ProjectDir"];
            var venvPython = _config["MlServer:VenvPython"];
            var host = _config["MlServer:Host"] ?? "127.0.0.1";
            var portStr = _config["MlServer:Port"] ?? "8000";

            if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
            {
                _logger.LogError("ML server project directory not found: {Path}", projectDir);
                return Task.CompletedTask;
            }

            var srcDir = Path.Combine(projectDir, "src");
            if (!Directory.Exists(srcDir))
            {
                _logger.LogError("ML server src directory not found: {Path}", srcDir);
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(venvPython) || !File.Exists(venvPython))
            {
                _logger.LogError("Python venv not found: {Path}", venvPython);
                return Task.CompletedTask;
            }

            if (!int.TryParse(portStr, out var port))
                port = 8000;

            var psi = new ProcessStartInfo
            {
                FileName = venvPython,
                WorkingDirectory = srcDir,
                Arguments = $"-m uvicorn main:app --host {host} --port {port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                _process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                _process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        _logger.LogInformation("[ML] {Line}", e.Data);
                };

                _process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        _logger.LogWarning("[ML] {Line}", e.Data);
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                _logger.LogInformation("ML server started (PID={Pid}) on {Host}:{Port}", _process.Id, host, port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start ML server");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(true);
                    _process.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping ML server");
            }

            return Task.CompletedTask;
        }
    }
}