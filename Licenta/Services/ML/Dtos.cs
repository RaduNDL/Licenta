namespace Licenta.Services.Ml
{
    public class LabResultPredictionResponse
    {
        public string Label { get; set; } = null!;          // ex: "High risk", "Normal"
        public float Probability { get; set; }              // ex: 0.87
        public string? Explanation { get; set; }            // text simplu
        public string? RawModelName { get; set; }           // optional: numele modelului ("xgboost_v1")
    }
}
