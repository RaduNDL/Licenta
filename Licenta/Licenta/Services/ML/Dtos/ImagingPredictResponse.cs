using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public sealed class ImagingPredictResponse
    {
        [JsonPropertyName("model_id")]
        public string? ModelId { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("effective_probability")]
        public double? EffectiveProbability { get; set; }

        [JsonPropertyName("best_probability")]
        public double? BestProbability { get; set; }

        [JsonPropertyName("probability")]
        public double? Probability { get; set; }

        [JsonPropertyName("proba_malignant")]
        public double? ProbaMalignant { get; set; }

        [JsonPropertyName("probabilities")]
        public Dictionary<string, float>? Probabilities { get; set; }

        [JsonPropertyName("ground_truth")]
        public string? GroundTruth { get; set; }

        [JsonPropertyName("match_with_dataset")]
        public bool? MatchWithDataset { get; set; }

        [JsonPropertyName("quality_ok")]
        public bool? QualityOk { get; set; }

        [JsonPropertyName("quality_score")]
        public double? QualityScore { get; set; }

        [JsonPropertyName("quality_issues")]
        public List<string>? QualityIssues { get; set; }

        [JsonPropertyName("quality_metrics")]
        public Dictionary<string, double>? QualityMetrics { get; set; }

        [JsonPropertyName("domain_ok")]
        public bool? DomainOk { get; set; }

        [JsonPropertyName("domain_score")]
        public double? DomainScore { get; set; }

        [JsonPropertyName("domain_issues")]
        public List<string>? DomainIssues { get; set; }

        [JsonPropertyName("domain_metrics")]
        public Dictionary<string, double>? DomainMetrics { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        [JsonIgnore]
        public bool QualityOkSafe => QualityOk ?? true;

        [JsonIgnore]
        public bool IsOutOfDomain =>
            string.Equals(Label, "OUT_OF_DOMAIN", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public bool IsUnusableImage =>
            string.Equals(Label, "UNUSABLE_IMAGE", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public bool IsRejected => IsOutOfDomain || IsUnusableImage;

        [JsonIgnore]
        public double EffectiveProbabilitySafe
        {
            get
            {
                if (EffectiveProbability.HasValue) return Clamp01(EffectiveProbability.Value);
                if (BestProbability.HasValue) return Clamp01(BestProbability.Value);
                if (Probability.HasValue) return Clamp01(Probability.Value);

                if (Probabilities != null && Label != null && Probabilities.TryGetValue(Label, out var v))
                    return Clamp01(v);

                if (Probabilities != null && Probabilities.Count > 0)
                    return Clamp01(Probabilities.Values.Max());

                return 0.0;
            }
        }

        private static double Clamp01(double x)
        {
            if (x < 0) return 0;
            if (x > 1) return 1;
            return x;
        }
    }
}
