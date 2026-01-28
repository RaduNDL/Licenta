using System;
using System.Collections.Generic;
using System.Linq;

namespace Licenta.Services.Predictions
{
    public class PredictionTargetRegistry : IPredictionTargetRegistry
    {
        private static readonly List<PredictionTargetOption> Items = new()
        {
            new PredictionTargetOption("ISIC2019", "ISIC 2019 (imaging)", "imaging")
        };

        private const string DefaultKey = "ISIC2019";

        public IReadOnlyList<PredictionTargetOption> GetAll()
        {
            return Items;
        }

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