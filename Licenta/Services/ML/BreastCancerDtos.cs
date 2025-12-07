using System;

namespace Licenta.Services.Ml
{
    public class BreastCancerRequest
    {
        public float Radius_mean { get; set; }
        public float Texture_mean { get; set; }
        public float perimeter_mean { get; set; }
        public float area_mean { get; set; }
        public float smoothness_mean { get; set; }
        public float compactness_mean { get; set; }
        public float concavity_mean { get; set; }
        public float concavepoints_mean { get; set; }
        public float symmetry_mean { get; set; }
        public float fractal_dimension_mean { get; set; }
    }

    public class BreastPredictionResponse
    {
        public string Label { get; set; } = string.Empty;
        public float Probability { get; set; }
        public string? Explanation { get; set; }
        public string? RawModelName { get; set; }
    }
}
