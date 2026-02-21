using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public class TrainStartResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("started")]
        public bool Started { get; set; }

        [JsonPropertyName("already_running")]
        public bool AlreadyRunning { get; set; }
    }
}
