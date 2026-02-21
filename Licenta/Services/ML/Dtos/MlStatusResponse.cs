using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public sealed class MlStatusResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("training")]
        public MlTrainingState? Training { get; set; }
    }
}
