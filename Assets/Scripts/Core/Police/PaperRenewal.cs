using System;
using System.Collections.Generic;
using TaxiKmer.Core.Career;

namespace TaxiKmer.Core.Police
{
    /// <summary>Ce qu'a donné le renouvellement d'un papier arrivé à échéance.</summary>
    public struct PaperRenewalResult
    {
        public PaperData Paper;
        /// <summary>Coût du renouvellement (renewalCost de police.json).</summary>
        public int Cost;
        /// <summary>Renouvelé et payé ; false : pas assez d'épargne, le papier reste expiré.</summary>
        public bool Renewed;
        /// <summary>Dernier jour de validité après l'opération (inchangé s'il n'est pas renouvelé).</summary>
        public int LastValidDay;
    }

    /// <summary>
    /// Renouvellement automatique des papiers (décision de Kevin du 21/11) : au début de chaque journée, chaque papier
    /// arrivé à échéance (expiré ce jour-là) est renouvelé et payé sur l'épargne, au prix de police.json
    /// (<see cref="PaperData.renewalCost"/>). Si l'épargne ne suffit pas (valeur par défaut de Claude, à confirmer par
    /// Kevin) : le papier reste expiré, un message l'annonce, et on réessaie au début de la journée suivante. Un papier
    /// gratuit (coût 0) est toujours renouvelé, même avec une épargne négative.
    /// </summary>
    public static class PaperRenewal
    {
        /// <summary>
        /// Renouvelle les papiers expirés au jour <paramref name="day"/>, dans l'ordre des données, tant que
        /// <paramref name="savings"/> les paie (chaque paiement réduit ce qui reste pour le suivant). Ne touche pas à
        /// l'argent : rend ce qui a été fait (la somme des coûts payés est à retirer de l'épargne).
        /// </summary>
        public static List<PaperRenewalResult> Renew(PaperSet papers, int day, int savings)
        {
            if (papers == null) throw new ArgumentNullException(nameof(papers));
            var results = new List<PaperRenewalResult>();
            var left = savings;
            foreach (var p in papers.Expired(day))
            {
                var cost = Math.Max(0, p.renewalCost);
                var affordable = cost == 0 || left >= cost;
                if (affordable)
                {
                    papers.Renew(p.id, day);
                    left -= cost;
                }
                results.Add(new PaperRenewalResult { Paper = p, Cost = cost, Renewed = affordable, LastValidDay = papers.LastValidDay(p.id) });
            }
            return results;
        }

        /// <summary>Somme payée pour les papiers renouvelés.</summary>
        public static int TotalPaid(IEnumerable<PaperRenewalResult> results)
        {
            var total = 0;
            if (results != null) foreach (var r in results) if (r.Renewed) total += r.Cost;
            return total;
        }

        /// <summary>
        /// Début de journée dans le jeu : renouvelle ce qui est arrivé à échéance au jour de la carrière et le paie sur
        /// son épargne. Rend ce qui a été fait, pour les messages (<see cref="PoliceTexts.Renewal"/>).
        /// </summary>
        public static List<PaperRenewalResult> AtDayStart(PaperSet papers, DriverCareer career)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            var results = Renew(papers, career.DayNumber, career.Savings);
            career.PayFromSavings(TotalPaid(results));
            return results;
        }
    }
}
