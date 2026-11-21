using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Home
{
    /// <summary>Une charge du foyer : montant dû tous les … jours (loyer chaque mois, nourriture chaque jour…).</summary>
    [Serializable]
    public class ChargeData
    {
        public string id = "";
        public string label = "";
        public int amount;
        /// <summary>Due tous les … jours (1 = chaque soir, 30 = chaque mois).</summary>
        public int everyDays = 1;
        /// <summary>Par enfant (montant × nombre d'enfants) ; sinon pour le foyer.</summary>
        public bool perChild;
    }

    /// <summary>Une situation familiale (célibataire, en couple, avec enfants) et ses charges.</summary>
    [Serializable]
    public class SituationData
    {
        public string id = "";
        public string name = "";
        public int children;
        public ChargeData[] charges = Array.Empty<ChargeData>();
    }

    /// <summary>Un événement de vie (maladie d'un enfant, rentrée scolaire, deuil, mariage…).</summary>
    [Serializable]
    public class LifeEventData
    {
        public string id = "";
        public string label = "";
        public int cost;
        /// <summary>Chance par jour.</summary>
        public float chancePerDay;
        /// <summary>Seulement s'il y a des enfants.</summary>
        public bool needsChildren;
        /// <summary>Revient au plus une fois tous les … jours.</summary>
        public int cooldownDays = 30;
    }

    /// <summary>La tontine : chacun cotise à chaque tour ; à son tour, un membre reçoit tout le pot.</summary>
    [Serializable]
    public class TontineData
    {
        public bool join;
        public int members = 10;
        public int contribution = 5000;
        /// <summary>Un tour tous les … jours.</summary>
        public int everyDays = 7;
        /// <summary>Le chauffeur reçoit le pot au tour n° … (1 = le premier).</summary>
        public int playerTurn = 5;
    }

    /// <summary>
    /// La vie à la maison (jalon 7, feuille de route ; données : Assets/Resources/Data/home.json) : situation familiale,
    /// charges, événements de vie, tontine. Montants et situations : décisions de Kevin (questions du jalon 7) ;
    /// <see cref="enabled"/> reste à false.
    /// </summary>
    [Serializable]
    public class HomeConfig
    {
        public bool enabled;
        /// <summary>Situation au départ (id dans <see cref="situations"/>).</summary>
        public string startSituation = "celibataire";
        public SituationData[] situations =
        {
            new SituationData
            {
                id = "celibataire", name = "Célibataire", children = 0,
                charges = new[]
                {
                    new ChargeData { id = "loyer", label = "Loyer", amount = 0, everyDays = 30 },
                    new ChargeData { id = "nourriture", label = "Nourriture", amount = 0, everyDays = 1 },
                },
            },
            new SituationData
            {
                id = "famille", name = "En couple, deux enfants", children = 2,
                charges = new[]
                {
                    new ChargeData { id = "loyer", label = "Loyer", amount = 0, everyDays = 30 },
                    new ChargeData { id = "nourriture", label = "Nourriture", amount = 0, everyDays = 1 },
                    new ChargeData { id = "scolarite", label = "Scolarité", amount = 0, everyDays = 30, perChild = true },
                },
            },
        };
        public LifeEventData[] lifeEvents =
        {
            new LifeEventData { id = "enfant-malade", label = "Un enfant est malade", cost = 0, chancePerDay = 0f, needsChildren = true },
            new LifeEventData { id = "deuil", label = "Deuil dans la famille", cost = 0, chancePerDay = 0f },
        };
        public TontineData tontine = new TontineData();
    }

    /// <summary>Une ligne de l'écran du soir (charge, événement, tontine).</summary>
    public struct HomeLine
    {
        public string Label;
        /// <summary>Positif : reçu (tontine) ; négatif : payé.</summary>
        public int Amount;
    }

    /// <summary>Le soir à la maison : ce qui est dû ce jour-là, ce qui arrive, ce que donne la tontine.</summary>
    public sealed class Household
    {
        private readonly HomeConfig _config;
        private readonly Dictionary<string, int> _lastEventDay = new Dictionary<string, int>();

        public SituationData Situation { get; private set; }

        public Household(HomeConfig config, string situationId = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Situation = Find(situationId ?? config.startSituation) ?? throw new ArgumentException($"Situation inconnue : {situationId ?? config.startSituation}");
        }

        public SituationData Find(string id)
        {
            foreach (var s in _config.situations ?? Array.Empty<SituationData>()) if (s.id == id) return s;
            return null;
        }

        /// <summary>La situation change (mariage, naissance : question 1 du jalon 7) ; false si inconnue.</summary>
        public bool ChangeSituation(string id)
        {
            var s = Find(id);
            if (s == null) return false;
            Situation = s;
            return true;
        }

        /// <summary>Charges dues le soir du jour <paramref name="day"/> (jour 1 : tout est dû le premier soir).</summary>
        public List<HomeLine> ChargesDue(int day)
        {
            var lines = new List<HomeLine>();
            foreach (var c in Situation.charges ?? Array.Empty<ChargeData>())
            {
                var every = Math.Max(1, c.everyDays);
                if ((Math.Max(1, day) - 1) % every != 0) continue;
                var amount = c.amount * (c.perChild ? Situation.children : 1);
                if (amount > 0) lines.Add(new HomeLine { Label = c.label, Amount = -amount });
            }
            return lines;
        }

        /// <summary>Événements de vie tirés ce soir-là (au plus un de chaque, avec leur délai de retour).</summary>
        public List<HomeLine> Events(int day, Random random)
        {
            var lines = new List<HomeLine>();
            foreach (var e in _config.lifeEvents ?? Array.Empty<LifeEventData>())
            {
                if (e.needsChildren && Situation.children <= 0) continue;
                if (_lastEventDay.TryGetValue(e.id, out var last) && day - last < Math.Max(1, e.cooldownDays)) continue;
                if (random.NextDouble() >= e.chancePerDay) continue;
                _lastEventDay[e.id] = day;
                lines.Add(new HomeLine { Label = e.label, Amount = -Math.Max(0, e.cost) });
            }
            return lines;
        }

        /// <summary>Tontine du soir : cotisation les jours de tour, et le pot le jour où c'est le tour du chauffeur.</summary>
        public static List<HomeLine> Tontine(TontineData t, int day)
        {
            var lines = new List<HomeLine>();
            if (t == null || !t.join || t.members <= 1) return lines;
            var every = Math.Max(1, t.everyDays);
            if (day % every != 0) return lines;
            var round = day / every; // 1, 2, 3…
            var turnInCycle = (round - 1) % t.members + 1;
            lines.Add(new HomeLine { Label = "Cotisation à la tontine", Amount = -t.contribution });
            if (turnInCycle == Math.Max(1, Math.Min(t.members, t.playerTurn)))
                lines.Add(new HomeLine { Label = "Ton tour de tontine", Amount = t.contribution * t.members });
            return lines;
        }

        /// <summary>Tout ce qui se passe à la maison ce soir-là, dans l'ordre de l'écran du soir.</summary>
        public List<HomeLine> Evening(int day, Random random)
        {
            var lines = ChargesDue(day);
            lines.AddRange(Events(day, random));
            lines.AddRange(Tontine(_config.tontine, day));
            return lines;
        }

        public static int Total(IEnumerable<HomeLine> lines)
        {
            var t = 0;
            foreach (var l in lines) t += l.Amount;
            return t;
        }

        /// <summary>
        /// Écran du soir (critère du jalon 7) : « Gagné », « Dépensé », « Mis de côté » en trois lignes lisibles.
        /// <paramref name="dayRest"/> = ce qui reste de la journée après versement, carburant et frais (clôture du propriétaire).
        /// </summary>
        public static string[] EveningSummary(int earned, int dayRest, IEnumerable<HomeLine> home)
        {
            var homeTotal = Total(home);
            var saved = dayRest + homeTotal;
            var spent = earned - saved;
            return new[]
            {
                $"Gagné : {Day.DaySummary.Money(earned)}",
                $"Dépensé : {Day.DaySummary.Money(spent)}",
                $"Mis de côté : {Day.DaySummary.Money(saved)}",
            };
        }
    }
}
