using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Day
{
    /// <summary>Une dépense imprévue de la journée (soins d'un piéton renversé…).</summary>
    public struct Expense
    {
        public string Label;
        public int Amount;
    }

    /// <summary>Comptes de la journée : recettes, versement, carburant, dépenses imprévues, reste.</summary>
    public sealed class DayLedger
    {
        private readonly DayConfig _config;
        private readonly List<Expense> _expenses = new List<Expense>();

        public int Revenue { get; private set; }
        public int RideCount { get; private set; }
        public float MetersDriven { get; private set; }

        public DayLedger(DayConfig config) => _config = config;

        public int Versement => _config.versement;

        public float FuelLitres => MetersDriven / 100000f * _config.fuelLitresPer100Km;

        public int FuelCost => (int)Math.Round(FuelLitres * _config.fuelPricePerLitre, MidpointRounding.AwayFromZero);

        public IReadOnlyList<Expense> Expenses => _expenses;

        public int ExpensesTotal
        {
            get
            {
                var t = 0;
                foreach (var e in _expenses) t += e.Amount;
                return t;
            }
        }

        /// <summary>Ce qui reste au chauffeur ; peut être négatif (versement non atteint, dépenses imprévues).</summary>
        public int Remaining => Revenue - Versement - FuelCost - ExpensesTotal;

        public void AddExpense(string label, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            _expenses.Add(new Expense { Label = label ?? "", Amount = amount });
        }

        public void AddFare(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Revenue += amount;
            RideCount++;
        }

        public void AddDistance(float meters)
        {
            if (meters < 0f) throw new ArgumentOutOfRangeException(nameof(meters));
            MetersDriven += meters;
        }
    }
}
