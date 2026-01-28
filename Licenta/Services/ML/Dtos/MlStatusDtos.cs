using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public class MlStatusResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("training")]
        public MlTrainingState? Training { get; set; }
    }

    public class MlTrainingState
    {
        [JsonPropertyName("started")]
        public bool Started { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("artifact_ok")]
        public bool ArtifactOk { get; set; }

        [JsonPropertyName("artifact_path")]
        public string? ArtifactPath { get; set; }
    }
}