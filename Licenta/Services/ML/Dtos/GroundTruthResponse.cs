using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public sealed class GroundTruthResponse
    {
        [JsonPropertyName("ground_truth")]
        public string? GroundTruth { get; set; }
    }
}
