using System;

namespace TaxiKmer.Core.Career
{
    /// <summary>Un palier de réputation et son nom (« bonne », « mauvaise »…).</summary>
    [Serializable]
    public class ReputationLevel
    {
        /// <summary>À partir de ce score (inclus).</summary>
        public int min;
        public string label = "";
    }

    /// <summary>
    /// Réputation du chauffeur (jalon 3, feuille de route : clients satisfaits, conduite ; données :
    /// Assets/Resources/Data/reputation.json). Visible ou cachée : question ouverte de Kevin, réglée par
    /// <see cref="visible"/>. Points gagnés et perdus : valeurs par défaut, à régler avec Kevin. Ses effets sur le jeu
    /// (clients, propriétaire, police…) ne sont pas décidés : elle se calcule et se sauvegarde, sans rien changer encore.
    /// </summary>
    [Serializable]
    public class ReputationConfig
    {
        /// <summary>Le joueur voit sa réputation (HUD et bilan) ; sinon elle reste cachée mais compte quand même.</summary>
        public bool visible = true;
        public int start = 50;
        public int min;
        public int max = 100;
        /// <summary>Client déposé à sa destination, qui a payé.</summary>
        public int ridePaid = 1;
        /// <summary>Client accepté d'un coup de klaxon puis laissé à attendre jusqu'à ce qu'il abandonne.</summary>
        public int acceptedClientGaveUp = -2;
        /// <summary>Passager descendu sans arriver à destination (accident, journée terminée avec lui à bord), par passager.</summary>
        public int passengerNotDelivered = -2;
        /// <summary>Piéton renversé : blessé léger, blessé grave.</summary>
        public int pedestrianLight = -10;
        public int pedestrianSerious = -25;
        /// <summary>Conduite brusque : avec des passagers à bord, au-delà de cette vitesse (km/h)…</summary>
        public float roughSpeedKmh = 60f;
        /// <summary>… on perd un point toutes les … secondes réelles.</summary>
        public float roughSecondsPerPoint = 10f;
        public ReputationLevel[] levels =
        {
            new ReputationLevel { min = 0, label = "très mauvaise" },
            new ReputationLevel { min = 20, label = "mauvaise" },
            new ReputationLevel { min = 40, label = "correcte" },
            new ReputationLevel { min = 60, label = "bonne" },
            new ReputationLevel { min = 80, label = "excellente" },
        };
    }

    /// <summary>Score de réputation, borné, avec ce qu'il valait au début de la journée.</summary>
    public sealed class DriverReputation
    {
        private readonly ReputationConfig _config;
        private float _roughSeconds;

        public ReputationConfig Config => _config;
        public int Score { get; private set; }
        /// <summary>Score au début de la journée (pour le bilan : « +3 aujourd'hui »).</summary>
        public int DayStart { get; private set; }
        public int ChangeToday => Score - DayStart;
        public bool Visible => _config.visible;

        public DriverReputation(ReputationConfig config, int? score = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Score = Clamp(score ?? config.start);
            DayStart = Score;
        }

        /// <summary>Reprend une journée sauvegardée : score au début de la journée et score actuel.</summary>
        public void Restore(int dayStart, int score)
        {
            DayStart = Clamp(dayStart);
            Score = Clamp(score);
        }

        public void Add(int delta) => Score = Clamp(Score + delta);

        public void RidesPaid(int count) => Add(count * _config.ridePaid);
        public void AcceptedClientGaveUp() => Add(_config.acceptedClientGaveUp);
        public void PassengersNotDelivered(int count) => Add(count * _config.passengerNotDelivered);
        public void PedestrianHit(bool serious) => Add(serious ? _config.pedestrianSerious : _config.pedestrianLight);

        /// <summary>Conduite : avec des passagers, trop vite fait perdre un point toutes les roughSecondsPerPoint s.</summary>
        public void Drive(float dt, float speedKmh, int passengers)
        {
            if (passengers <= 0 || Math.Abs(speedKmh) <= _config.roughSpeedKmh || _config.roughSecondsPerPoint <= 0f) return;
            _roughSeconds += dt;
            while (_roughSeconds >= _config.roughSecondsPerPoint)
            {
                _roughSeconds -= _config.roughSecondsPerPoint;
                Add(-1);
            }
        }

        /// <summary>Nom du palier du score (« bonne »).</summary>
        public string Label => LabelFor(_config, Score);

        public static string LabelFor(ReputationConfig config, int score)
        {
            var label = "";
            var best = int.MinValue;
            if (config.levels != null)
                foreach (var l in config.levels)
                    if (score >= l.min && l.min >= best) { best = l.min; label = l.label; }
            return label;
        }

        /// <summary>« Réputation : bonne (62, +4 aujourd'hui) » (bilan).</summary>
        public static string SummaryLine(ReputationConfig config, int score, int change)
        {
            var sign = change > 0 ? "+" : change < 0 ? "-" : "±";
            return $"Réputation : {LabelFor(config, score)} ({score}, {sign}{Math.Abs(change)} aujourd'hui)";
        }

        /// <summary>« Réputation 62 » (HUD).</summary>
        public string HudLabel => $"Réputation {Score}";

        private int Clamp(int v) => Math.Max(_config.min, Math.Min(_config.max, v));
    }
}
