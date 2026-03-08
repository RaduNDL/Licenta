using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public sealed class MlTrainingState
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

        [JsonPropertyName("path_map_ok")]
        public bool PathMapOk { get; set; }

        [JsonPropertyName("path_map_path")]
        public string? PathMapPath { get; set; }

        [JsonPropertyName("path_map_size")]
        public int PathMapSize { get; set; }

        [JsonPropertyName("name_map_ok")]
        public bool NameMapOk { get; set; }

        [JsonPropertyName("name_map_path")]
        public string? NameMapPath { get; set; }

        [JsonPropertyName("name_map_size")]
        public int NameMapSize { get; set; }

        [JsonPropertyName("hash_map_ok")]
        public bool HashMapOk { get; set; }

        [JsonPropertyName("hash_map_path")]
        public string? HashMapPath { get; set; }

        [JsonPropertyName("hash_map_size")]
        public int HashMapSize { get; set; }
    }
}
