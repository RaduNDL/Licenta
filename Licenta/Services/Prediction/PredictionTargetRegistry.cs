using Licenta.Services.Prediction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Licenta.Services.Prediction
{
    public class PredictionTargetRegistry : IPredictionTargetRegistry
    {
        private static readonly List<PredictionTargetOption> Items =
        [
            new PredictionTargetOption("CBIS-DDSM", "CBIS-DDSM (imaging)", "imaging")
        ];

        private const string DefaultKey = "CBIS-DDSM";

        public IReadOnlyList<PredictionTargetOption> GetAll() => Items;

        public bool IsAllowed(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return Items.Any(x => string.Equals(x.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public string NormalizeOrDefault(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return DefaultKey;

            var k = key.Trim();
            var found = Items.FirstOrDefault(x => string.Equals(x.Key, k, StringComparison.OrdinalIgnoreCase));
            return found?.Key ?? DefaultKey;
        }
    }
}