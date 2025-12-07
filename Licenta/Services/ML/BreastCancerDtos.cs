using System.Text.Json.Serialization;

namespace Licenta.Services.Ml
{
    // Request trimis către API-ul Python /api/breast/analyze
    public class BreastCancerRequest
    {
        // IMPORTANT: JSON-ul trebuie să aibă cheile exact ca în Python.
        // Folosim JsonPropertyName ca să mapăm corect.

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

        // în Python coloana se numește concave_points_mean.
        // Proprietatea ta este concavepoints_mean, dar o mapăm corect pe JSON:
        [JsonPropertyName("concave_points_mean")]
        public float concavepoints_mean { get; set; }

        [JsonPropertyName("symmetry_mean")]
        public float symmetry_mean { get; set; }

        [JsonPropertyName("fractal_dimension_mean")]
        public float fractal_dimension_mean { get; set; }
    }

    // Răspunsul venit de la FastAPI (app.py)
    //
    // Python trimite ceva de genul:
    // {
    //   "label": "B",
    //   "probability": 0.001,               // = P(Malignant)
    //   "probability_benign": 0.999,
    //   "probability_malignant": 0.001,
    //   "explanation": "....",
    //   "raw_model_name": "BreastCancer_RF_v1"
    // }
    public class BreastPredictionResponse
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        // "probability" în Python = P(Malignant).
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

        // ========= HELPERI PENTRU UI / DB =========

        // Probabilitatea că este malign (P(M))
        [JsonIgnore]
        public float MalignantProbability =>
            ProbabilityMalignant ?? Probability;

        // Probabilitatea că este benign (P(B))
        [JsonIgnore]
        public float BenignProbability =>
            ProbabilityBenign ?? (1f - MalignantProbability);

        // Confidence pentru clasa prezisă:
        //  - dacă Label == "B" -> confidence = P(B)
        //  - dacă Label == "M" -> confidence = P(M)
        [JsonIgnore]
        public float Confidence =>
            Label == "B"
                ? BenignProbability
                : MalignantProbability;
    }
}
