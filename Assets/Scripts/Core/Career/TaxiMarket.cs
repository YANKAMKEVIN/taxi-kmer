using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Career
{
    /// <summary>Un taxi à vendre (modèle fictif inspiré des taxis de Yaoundé : question 2 du jalon 9).</summary>
    [Serializable]
    public class VehicleOfferData
    {
        public string id = "";
        public string model = "";
        public bool used;
        /// <summary>État de 0 (épave) à 100 (neuf).</summary>
        public int condition = 100;
        public int price;
    }

    /// <summary>Prêt pour acheter le taxi : remboursé chaque jour, avec des intérêts.</summary>
    [Serializable]
    public class LoanData
    {
        /// <summary>Apport minimum (part du prix payée comptant).</summary>
        public float minDownPayment = 0.3f;
        /// <summary>Intérêts sur la somme prêtée (total, pas annuel).</summary>
        public float interest = 0.2f;
        /// <summary>Remboursé en … jours de jeu.</summary>
        public int days = 60;
    }

    /// <summary>
    /// Devenir propriétaire (jalon 9 ; données : Assets/Resources/Data/taxi_market.json). Modèles, prix, prêt et charges
    /// du propriétaire : décisions de Kevin ; aucune offre dans les données et <see cref="enabled"/> à false.
    /// </summary>
    [Serializable]
    public class TaxiMarketConfig
    {
        public bool enabled;
        public VehicleOfferData[] offers = Array.Empty<VehicleOfferData>();
        public LoanData loan = new LoanData();
        /// <summary>Propriétaire : entretien par km roulé (FCFA ; 0 tant que Kevin n'a pas décidé).</summary>
        public float maintenancePerKm;
    }

    /// <summary>Calculs d'achat : apport, remboursement du prêt, jours d'épargne nécessaires.</summary>
    public static class TaxiPurchase
    {
        public static int DownPayment(LoanData loan, int price) =>
            (int)Math.Ceiling(price * Math.Round((double)Math.Max(0f, Math.Min(1f, loan.minDownPayment)), 6));

        /// <summary>Remboursement par jour d'un prêt de <paramref name="borrowed"/> FCFA (intérêts compris), arrondi au franc supérieur.</summary>
        public static int DailyRepayment(LoanData loan, int borrowed)
        {
            if (borrowed <= 0) return 0;
            // Total arrondi au franc d'abord (0,2f n'est pas exactement 0,2), puis division entière arrondie au-dessus.
            var total = (long)Math.Round(borrowed * (1.0 + Math.Max(0.0, Math.Round(loan.interest, 6))));
            var days = Math.Max(1, loan.days);
            return (int)((total + days - 1) / days);
        }

        /// <summary>Peut-on acheter comptant, ou avec le prêt (apport) ?</summary>
        public static bool CanBuyCash(int savings, int price) => savings >= price;
        public static bool CanBuyWithLoan(LoanData loan, int savings, int price) => savings >= DownPayment(loan, price);

        /// <summary>
        /// Jours de jeu pour réunir <paramref name="target"/> en mettant de côté <paramref name="savedPerDay"/> par jour
        /// (-1 si on n'y arrive jamais) : sert à la question 1 du jalon 9 (durée visée pour acheter son taxi).
        /// </summary>
        public static int DaysToSave(int savings, int target, int savedPerDay)
        {
            if (savings >= target) return 0;
            if (savedPerDay <= 0) return -1;
            return (int)Math.Ceiling((target - savings) / (double)savedPerDay);
        }

        /// <summary>
        /// Une journée de propriétaire : plus de versement, mais l'entretien (au km), le prêt et les autres charges du
        /// véhicule sont pour lui. Rend ce qu'il garde.
        /// </summary>
        public static int OwnerDayNet(TaxiMarketConfig market, int revenue, int fuelCost, float km, int loanRepayment, int otherCharges)
        {
            var maintenance = (int)Math.Round(Math.Max(0f, market.maintenancePerKm) * Math.Max(0f, km));
            return revenue - fuelCost - maintenance - Math.Max(0, loanRepayment) - Math.Max(0, otherCharges);
        }

        /// <summary>Offres triées par prix (les moins chères d'abord).</summary>
        public static List<VehicleOfferData> Sorted(TaxiMarketConfig market)
        {
            var list = new List<VehicleOfferData>(market.offers ?? Array.Empty<VehicleOfferData>());
            list.Sort((a, b) => a.price.CompareTo(b.price));
            return list;
        }
    }
}
