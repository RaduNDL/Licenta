using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Licenta.Services.Ml
{
    public class MlServerStarter : IHostedService
    {
        private Process? _process;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C cd /d \"D:\\Facultate\\Licenta\\Licenta\\Python\" && python run_server.py",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = Process.Start(psi);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_process is { HasExited: false })
                {
                    _process.Kill(true);
                }
            }
            catch
            {

            }

            return Task.CompletedTask;
        }
    }
}
