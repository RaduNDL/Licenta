using System.Text.Json.Serialization;

namespace Licenta.Services.Ml
{
    public class BreastCancerRequest
    {
        [JsonPropertyName("radius_mean")]
        public float Radius_mean { get; set; }

        [JsonPropertyName("texture_mean")]
        public float Texture_mean { get; set; }

        [JsonPropertyName("perimeter_mean")]
        public float perimeter_mean { get; set; }

        [JsonPropertyName("area_mean")]
        public float area_mean { get; set; }

        [JsonPropertyName("smoothness_mean")]
        public float smoothness_mean { get; set; }

        [JsonPropertyName("compactness_mean")]
        public float compactness_mean { get; set; }

        [JsonPropertyName("concavity_mean")]
        public float concavity_mean { get; set; }


        [JsonPropertyName("concave_points_mean")]
        public float concavepoints_mean { get; set; }

        [JsonPropertyName("symmetry_mean")]
        public float symmetry_mean { get; set; }

        [JsonPropertyName("fractal_dimension_mean")]
        public float fractal_dimension_mean { get; set; }
    }


    public class BreastPredictionResponse
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("probability")]
        public float Probability { get; set; }

        [JsonPropertyName("probability_benign")]
        public float? ProbabilityBenign { get; set; }

        [JsonPropertyName("probability_malignant")]
        public float? ProbabilityMalignant { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }

        [JsonPropertyName("raw_model_name")]
        public string? RawModelName { get; set; }


        [JsonIgnore]
        public float MalignantProbability =>
            ProbabilityMalignant ?? Probability;

        [JsonIgnore]
        public float BenignProbability =>
            ProbabilityBenign ?? (1f - MalignantProbability);


        [JsonIgnore]
        public float Confidence =>
            Label == "B"
                ? BenignProbability
                : MalignantProbability;
    }
}