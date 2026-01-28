using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Licenta.Services.Ml.Dtos
{
    public class TabularFeaturesResponse
    {
        [JsonPropertyName("model_id")]
        public string ModelId { get; set; } = string.Empty;

        [JsonPropertyName("features")]
        public List<string> Features { get; set; } = new();

        [JsonPropertyName("n_features")]
        public int NFeatures { get; set; }

        [JsonPropertyName("meta")]
        public Dictionary<string, object> Meta { get; set; } = new();
    }

    public class TabularPredictRequest
    {
        [JsonPropertyName("model_id")]
        public string? ModelId { get; set; }

        [JsonPropertyName("features")]
        public Dictionary<string, float>? Features { get; set; }

        [JsonPropertyName("vector")]
        public List<float>? Vector { get; set; }
    }

    public class TabularPredictResponse
    {
        [JsonPropertyName("model_id")]
        public string ModelId { get; set; } = string.Empty;

        [JsonPropertyName("proba_malignant")]
        public float ProbaMalignant { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("extras")]
        public Dictionary<string, object> Extras { get; set; } = new();

        [JsonIgnore]
        public float ProbaBenign => 1f - ProbaMalignant;
    }
}