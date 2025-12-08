namespace Licenta.Services.Ml
{
    public class LabResultPredictionResponse
    {
        public string Label { get; set; } = null!;      
        public float Probability { get; set; }              
        public string? Explanation { get; set; }            
        public string? RawModelName { get; set; }          
    }
}
