using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public class ImagingPredictResponse
    {
        [JsonPropertyName("model_id")]
        public string? ModelId { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("probability")]
        public float? Probability { get; set; }

        [JsonPropertyName("best_probability")]
        public float? BestProbability { get; set; }

        [JsonPropertyName("proba_malignant")]
        public float? ProbaMalignant { get; set; }

        [JsonPropertyName("probabilities")]
        public Dictionary<string, float>? Probabilities { get; set; }

        [JsonPropertyName("probas")]
        public Dictionary<string, float>? Probas { get; set; }

        [JsonPropertyName("extras")]
        public Dictionary<string, JsonElement>? Extras { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }

        [JsonIgnore]
        public float? EffectiveProbability
        {
            get
            {
                if (Probability.HasValue) return Probability.Value;
                if (BestProbability.HasValue) return BestProbability.Value;
                if (ProbaMalignant.HasValue) return ProbaMalignant.Value;
                return null;
            }
        }
    }
}