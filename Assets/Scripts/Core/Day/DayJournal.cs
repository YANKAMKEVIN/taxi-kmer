using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Day
{
    /// <summary>Une journée jouée, telle qu'on la compare à l'étude d'équilibrage (idée n° 38).</summary>
    [Serializable]
    public class JournalDayData
    {
        public int dayNumber;
        public string weekday = "";
        public int revenue;
        public int rideCount;
        public int versement;
        public bool versementReached;
        public int versementMissing;
        public float fuelLitres;
        public int fuelCost;
        public int expensesTotal;
        /// <summary>Contrôles subis, et ce qui leur a été payé (FCFA).</summary>
        public int policeControls;
        public int policePaid;
        public int crashes;
        public int tips;
        public int unpaidPassengers;
        public float kilometres;
        public int savingsAfter;
        public int reputation = -1;
        public string gameVersion = "";
    }

    /// <summary>Journal local des journées (fichier journal.json à côté de la sauvegarde).</summary>
    [Serializable]
    public class JournalData
    {
        public int version = 1;
        public JournalDayData[] days = Array.Empty<JournalDayData>();
    }

    /// <summary>
    /// Journal de fin de journée (idée n° 38) : un petit JSON par partie pour comparer les vraies parties de Kevin à
    /// BalanceStudy (cible : versement atteint 60 à 80 % des jours). Pur, testable.
    /// </summary>
    public static class DayJournal
    {
        /// <summary>Le journal garde les dernières journées seulement.</summary>
        public const int MaxDays = 120;

        public static JournalDayData Entry(DaySummary s, int dayNumber, string weekday, int policeControls, int policePaid,
            int crashes, int tips, int savingsAfter, string gameVersion = "")
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return new JournalDayData
            {
                dayNumber = dayNumber,
                weekday = weekday ?? "",
                revenue = s.Revenue,
                rideCount = s.RideCount,
                versement = s.Versement,
                versementReached = s.VersementReached,
                versementMissing = s.VersementShortfall,
                fuelLitres = s.FuelLitres,
                fuelCost = s.FuelCost,
                expensesTotal = s.ExpensesTotal,
                policeControls = policeControls,
                policePaid = policePaid,
                crashes = crashes,
                tips = tips,
                unpaidPassengers = s.UnpaidPassengers,
                kilometres = s.Kilometres,
                savingsAfter = savingsAfter,
                reputation = s.Reputation,
                gameVersion = gameVersion ?? "",
            };
        }

        /// <summary>Ajoute une journée (en remplaçant celle du même numéro, s'il y en a une) et ne garde que les dernières.</summary>
        public static JournalData Append(JournalData journal, JournalDayData day)
        {
            var list = new List<JournalDayData>();
            foreach (var d in journal?.days ?? Array.Empty<JournalDayData>())
                if (d != null && d.dayNumber != day.dayNumber) list.Add(d);
            list.Add(day);
            list.Sort((a, b) => a.dayNumber.CompareTo(b.dayNumber));
            if (list.Count > MaxDays) list.RemoveRange(0, list.Count - MaxDays);
            return new JournalData { days = list.ToArray() };
        }

        /// <summary>Part des journées où le versement est atteint (0 à 1 ; -1 sans journée).</summary>
        public static float VersementRate(JournalData journal)
        {
            var days = journal?.days ?? Array.Empty<JournalDayData>();
            if (days.Length == 0) return -1f;
            var n = 0;
            foreach (var d in days) if (d.versementReached) n++;
            return n / (float)days.Length;
        }

        /// <summary>Phrase de comparaison avec la cible de l'équilibrage (60 à 80 %).</summary>
        public static string Verdict(JournalData journal, float targetMin = 0.6f, float targetMax = 0.8f)
        {
            var rate = VersementRate(journal);
            if (rate < 0f) return "Aucune journée dans le journal.";
            var pct = (int)Math.Round(rate * 100f);
            var where = rate < targetMin ? "trop dur" : rate > targetMax ? "trop facile" : "dans la cible";
            return $"Versement atteint {pct} % des {journal.days.Length} journées : {where} (cible {targetMin * 100:0} à {targetMax * 100:0} %).";
        }
    }
}
