using System.Collections.Generic;

namespace Licenta.Services.Prediction
{
    public record PredictionTargetOption(string Key, string DisplayName, string Kind);

    public interface IPredictionTargetRegistry
    {
        IReadOnlyList<PredictionTargetOption> GetAll();
        bool IsAllowed(string? key);
        string NormalizeOrDefault(string? key);
    }
}