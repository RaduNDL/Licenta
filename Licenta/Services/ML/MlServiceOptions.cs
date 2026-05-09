namespace Licenta.Services.Ml
{
    public class MlServiceOptions
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1:8001";
        public int TimeoutSeconds { get; set; } = 60;

        public bool RequireDomainGateImaging { get; set; } = true;
        public double DomainMinProbability { get; set; } = 0.65;
        public double DomainMinMargin { get; set; } = 0.10;

        public bool AutoStartPythonServer { get; set; } = false;
        public string PythonExecutablePath { get; set; } = "";
        public string PythonProjectDirectory { get; set; } = "";
        public string PythonScriptPath { get; set; } = "src/run_server.py";
    }
}