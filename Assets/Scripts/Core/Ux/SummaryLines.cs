using System.Collections.Generic;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Une ligne du bilan : libellé, signe (« + » ou « - ») et montant positif.</summary>
    public struct SummaryLine
    {
        public string Label;
        public bool Income;
        public int Amount;

        public string Sign => Income ? "+ " : "- ";
    }

    /// <summary>
    /// Lignes de l'écran de bilan (logique pure, testée) : recette des courses, puis pourboires et dédommagements
    /// d'accrochage chacun sur sa ligne quand il y en a (avant : tout dans « Recettes (0 course) »), versement,
    /// carburant, puis les frais imprévus. Libellés courts : ils tiennent sur une ligne d'un téléphone en paysage.
    /// </summary>
    public static class SummaryLines
    {
        public static List<SummaryLine> Build(DaySummary s, DayClosure c)
        {
            var lines = new List<SummaryLine>
            {
                new SummaryLine { Label = $"Courses ({s.RideCount})", Income = true, Amount = s.FaresRevenue },
            };
            if (s.Tips > 0) lines.Add(new SummaryLine { Label = "Pourboires", Income = true, Amount = s.Tips });
            if (s.Compensations > 0) lines.Add(new SummaryLine { Label = "Dédommagements reçus", Income = true, Amount = s.Compensations });
            var versement = c.TakenFromSavings > 0
                ? $"Versement (dont {DaySummary.Money(c.TakenFromSavings)} d'épargne)"
                : c.VersementMissing > 0 ? "Versement (incomplet)" : "Versement au propriétaire";
            lines.Add(new SummaryLine { Label = versement, Amount = c.VersementPaid });
            lines.Add(new SummaryLine
            {
                Label = $"Carburant ({DaySummary.OneDecimal(s.FuelLitres)} L · {DaySummary.OneDecimal(s.Kilometres)} km)",
                Amount = s.FuelCost,
            });
            foreach (var e in s.Expenses) lines.Add(new SummaryLine { Label = e.Label, Amount = e.Amount });
            return lines;
        }

        /// <summary>
        /// « Épargne : 12 000 FCFA (+ 3 000 FCFA) » ; sans la variation quand elle répète le total (épargne de départ nulle).
        /// </summary>
        public static string Savings(int after, int change)
        {
            var total = $"Épargne : {DaySummary.Money(after)}";
            return after == change ? total : $"{total} ({(change >= 0 ? "+ " : "- ")}{DaySummary.Money(System.Math.Abs(change))})";
        }
    }
}
