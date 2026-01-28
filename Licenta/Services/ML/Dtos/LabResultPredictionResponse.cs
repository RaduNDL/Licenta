using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public class LabResultPredictionResponse
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("probability")]
        public float? Probability { get; set; }

        [JsonPropertyName("proba_malignant")]
        public float? ProbaMalignant { get; set; }

        [JsonPropertyName("model_id")]
        public string? ModelId { get; set; }

        [JsonPropertyName("extras")]
        public Dictionary<string, object>? Extras { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }
    }
}