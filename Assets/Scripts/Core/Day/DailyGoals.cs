using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Day
{
    /// <summary>Un petit objectif possible (idée n° 25). Kinds : voir <see cref="GoalKinds"/>.</summary>
    [Serializable]
    public class GoalData
    {
        public string id = "";
        public string kind = "";
        /// <summary>Texte affiché ; {n} = nombre visé, {heure} = heure limite, {montant} = somme.</summary>
        public string label = "";
        public int count;
        public string beforeTime = "";
        public int amount;
        /// <summary>Prime proposée (FCFA), versée seulement si <see cref="DailyGoalsConfig.rewardsEnabled"/>.</summary>
        public int reward = 200;
    }

    public static class GoalKinds
    {
        /// <summary>Déposer {n} passagers (courses payées) avant {heure}.</summary>
        public const string DropsBefore = "courses-avant";
        /// <summary>Ne rien payer à la police de la journée.</summary>
        public const string NoPolicePaid = "zero-controle-paye";
        /// <summary>Faire {montant} de recette.</summary>
        public const string Revenue = "recette";
        /// <summary>Aucun accrochage de la journée.</summary>
        public const string NoCrash = "zero-accrochage";
        /// <summary>Avoir {n} passagers à bord en même temps.</summary>
        public const string FullCabin = "taxi-plein";

        public static readonly string[] All = { DropsBefore, NoPolicePaid, Revenue, NoCrash, FullCabin };
    }

    /// <summary>
    /// Petits objectifs du jour (idée n° 25 ; données : Assets/Resources/Data/goals.json) : 1 à 3 défis tirés le
    /// matin, affichés dans la pause et au bilan. Objectifs proposés par Claude ; primes activées par Kevin le 23/11 au
    /// soir, montants fixés d'après l'étude d'équilibrage (le versement reste atteint dans 60 à 80 % des journées).
    /// </summary>
    [Serializable]
    public class DailyGoalsConfig
    {
        public bool enabled = true;
        public int minPerDay = 1;
        public int maxPerDay = 3;
        /// <summary>Primes versées (décision de Kevin du 23/11 au soir ; montants d'après l'étude, docs/tech/equilibrage.md).</summary>
        public bool rewardsEnabled = true;
        public GoalData[] goals =
        {
            new GoalData { id = "matinal", kind = GoalKinds.DropsBefore, label = "Déposer {n} passagers avant {heure}", count = 3, beforeTime = "08:00", reward = 400 },
            new GoalData { id = "honnete", kind = GoalKinds.NoPolicePaid, label = "Ne rien payer aux contrôles", reward = 500 },
            new GoalData { id = "bonne-recette", kind = GoalKinds.Revenue, label = "Faire {montant} de recette", amount = 12000, reward = 100 },
            new GoalData { id = "prudent", kind = GoalKinds.NoCrash, label = "Aucun accrochage de la journée", reward = 200 },
            new GoalData { id = "plein", kind = GoalKinds.FullCabin, label = "Rouler avec {n} passagers à bord", count = 5, reward = 100 },
        };
    }

    /// <summary>Un objectif tiré pour la journée et son avancement.</summary>
    public sealed class DailyGoal
    {
        public GoalData Data { get; internal set; }
        public int Progress { get; internal set; }
        public bool Failed { get; internal set; }
        public bool Done { get; internal set; }

        public string Label => DailyGoals.Fill(Data);

        /// <summary>« ✓ », « ✗ » ou « 2/3 » devant le libellé.</summary>
        public string Line =>
            (Done ? "✓ " : Failed ? "✗ " : Data.count > 1 && Data.kind != GoalKinds.FullCabin ? $"{Progress}/{Data.count} " : "· ") + Label;
    }

    /// <summary>Les objectifs de la journée : tirés au début, suivis au fil des événements, réglés au bilan (pur, testable).</summary>
    public sealed class DailyGoals
    {
        private readonly DailyGoalsConfig _config;
        private readonly List<DailyGoal> _goals = new List<DailyGoal>();

        public IReadOnlyList<DailyGoal> Goals => _goals;

        /// <summary>Objectif tout juste réussi (pour un message), remis à null après lecture.</summary>
        public DailyGoal JustDone { get; private set; }

        public DailyGoals(DailyGoalsConfig config, int dayNumber, int careerSeed)
        {
            _config = config ?? new DailyGoalsConfig();
            if (!_config.enabled || _config.goals == null || _config.goals.Length == 0) return;
            var random = new Random(unchecked(careerSeed * 31337 + dayNumber * 7907));
            var pool = new List<int>();
            for (var i = 0; i < _config.goals.Length; i++) pool.Add(i);
            var count = Math.Min(pool.Count, random.Next(Math.Max(0, _config.minPerDay), Math.Max(_config.minPerDay, _config.maxPerDay) + 1));
            var picked = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var k = random.Next(pool.Count);
                picked.Add(pool[k]);
                pool.RemoveAt(k);
            }
            picked.Sort(); // affichés dans l'ordre des données
            foreach (var i in picked) _goals.Add(new DailyGoal { Data = _config.goals[i] });
        }

        /// <summary>Une course payée à cette minute de jeu.</summary>
        public void RidePaid(int minute)
        {
            foreach (var g in _goals)
            {
                if (g.Data.kind != GoalKinds.DropsBefore || g.Done || g.Failed) continue;
                var limit = Talk.LineData.ParseTime(g.Data.beforeTime);
                if (limit >= 0 && minute >= limit) continue;
                g.Progress++;
                if (g.Progress >= Math.Max(1, g.Data.count)) Complete(g);
            }
        }

        public void PolicePaid(int amount) => FailAll(GoalKinds.NoPolicePaid, amount > 0);
        public void Crash() => FailAll(GoalKinds.NoCrash, true);

        public void Passengers(int onBoard)
        {
            foreach (var g in _goals)
                if (g.Data.kind == GoalKinds.FullCabin && !g.Done && onBoard >= Math.Max(1, g.Data.count)) { g.Progress = onBoard; Complete(g); }
        }

        public void Revenue(int revenue)
        {
            foreach (var g in _goals)
                if (g.Data.kind == GoalKinds.Revenue && !g.Done && revenue >= g.Data.amount) { g.Progress = revenue; Complete(g); }
        }

        /// <summary>Fin de journée : les objectifs « sans… » encore tenus sont réussis, les autres ratés.</summary>
        public void EndDay()
        {
            foreach (var g in _goals)
            {
                if (g.Done || g.Failed) continue;
                if (g.Data.kind == GoalKinds.NoPolicePaid || g.Data.kind == GoalKinds.NoCrash) g.Done = true;
                else g.Failed = true;
            }
        }

        /// <summary>Prime gagnée (somme des objectifs réussis ; 0 si rewardsEnabled est coupé).</summary>
        public int Reward
        {
            get
            {
                if (!_config.rewardsEnabled) return 0;
                var sum = 0;
                foreach (var g in _goals) if (g.Done) sum += Math.Max(0, g.Data.reward);
                return sum;
            }
        }

        public int DoneCount
        {
            get
            {
                var n = 0;
                foreach (var g in _goals) if (g.Done) n++;
                return n;
            }
        }

        /// <summary>Lignes pour la pause et le bilan ; vide sans objectif.</summary>
        public string Summary()
        {
            if (_goals.Count == 0) return "";
            var lines = new List<string> { $"Objectifs du jour ({DoneCount}/{_goals.Count})" };
            foreach (var g in _goals) lines.Add(g.Line);
            return string.Join("\n", lines);
        }

        public DailyGoal TakeJustDone()
        {
            var g = JustDone;
            JustDone = null;
            return g;
        }

        private void Complete(DailyGoal g)
        {
            if (g.Done) return;
            g.Done = true;
            JustDone = g;
        }

        private void FailAll(string kind, bool fail)
        {
            if (!fail) return;
            foreach (var g in _goals) if (g.Data.kind == kind && !g.Done) g.Failed = true;
        }

        public static string Fill(GoalData g) => (g.label ?? "")
            .Replace("{n}", g.count.ToString())
            .Replace("{heure}", g.beforeTime)
            .Replace("{montant}", DaySummary.Money(g.amount));
    }
}
