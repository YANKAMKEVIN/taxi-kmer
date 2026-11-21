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

        /// <summary>
        /// Carburant acheté à la station (jalon 3, réservoir réel) : le bilan compte ce qui a été payé dans la journée,
        /// pas ce qui a été brûlé. Sans réservoir (tests anciens, jalon 1), le carburant se compte à la distance.
        /// </summary>
        public bool UsesFuelPurchases { get; set; }
        public float FuelBoughtLitres { get; private set; }
        public int FuelBoughtCost { get; private set; }

        public float FuelLitres => UsesFuelPurchases ? FuelBoughtLitres : MetersDriven / 100000f * _config.fuelLitresPer100Km;

        public int FuelCost => UsesFuelPurchases ? FuelBoughtCost
            : (int)Math.Round(FuelLitres * _config.fuelPricePerLitre, MidpointRounding.AwayFromZero);

        public int FuelPricePerLitre => _config.fuelPricePerLitre;

        /// <summary>Plein (ou partie) payé à la station.</summary>
        public void BuyFuel(float litres, int cost)
        {
            if (litres < 0f) throw new ArgumentOutOfRangeException(nameof(litres));
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            FuelBoughtLitres += litres;
            FuelBoughtCost += cost;
        }

        /// <summary>Reprend le carburant acheté d'une journée sauvegardée.</summary>
        internal void RestoreFuel(float litres, int cost)
        {
            FuelBoughtLitres = Math.Max(0f, litres);
            FuelBoughtCost = Math.Max(0, cost);
        }

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

        /// <summary>Reprend les comptes d'une journée sauvegardée.</summary>
        internal void Restore(int revenue, int rideCount, float metersDriven, IEnumerable<Expense> expenses)
        {
            Revenue = Math.Max(0, revenue);
            RideCount = Math.Max(0, rideCount);
            MetersDriven = Math.Max(0f, metersDriven);
            _expenses.Clear();
            if (expenses != null)
                foreach (var e in expenses)
                    if (e.Amount > 0) _expenses.Add(e);
        }

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

        /// <summary>Pourboires du jour (FCFA), compris dans la recette.</summary>
        public int Tips { get; private set; }

        /// <summary>Pourboire d'un passager content (jalon 5) : s'ajoute à la recette sans compter de course.</summary>
        public void AddTip(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Revenue += amount;
            Tips += amount;
        }

        public void AddDistance(float meters)
        {
            if (meters < 0f) throw new ArgumentOutOfRangeException(nameof(meters));
            MetersDriven += meters;
        }
    }
}
