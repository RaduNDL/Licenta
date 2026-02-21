using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public class LabAnalyzeResponse
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("probability")]
        public float? Probability { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }
    }
}
