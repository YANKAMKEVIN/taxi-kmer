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
    /// Effets de la réputation sur le jeu (décision de Kevin du 2026-11-21 : clients plus ou moins patients, prix
    /// acceptés, attitude du propriétaire, contrôles de police). Chaque effet est un coefficient autour du score
    /// <see cref="neutral"/> : facteur = 1 + (score - neutral) × parPoint, borné. Coefficients : valeurs par défaut de
    /// Claude, à régler avec Kevin.
    /// </summary>
    [Serializable]
    public class ReputationEffectsConfig
    {
        /// <summary>Effets branchés (false : la réputation se compte sans rien changer).</summary>
        public bool enabled = true;
        /// <summary>Score sans effet (facteurs à 1).</summary>
        public int neutral = 50;
        /// <summary>Patience des clients acceptés : +1 % par point au-dessus de neutral (score 100 : ×1,5 ; 0 : ×0,5).</summary>
        public float patiencePerPoint = 0.01f;
        public float patienceMin = 0.5f;
        public float patienceMax = 1.5f;
        /// <summary>
        /// Prix acceptés : chance qu'un client négocie le prix (jalon 5), -1,2 % par point au-dessus de neutral
        /// (score 100 : ×0,4 ; 0 : ×1,6).
        /// </summary>
        public float hagglePerPoint = -0.012f;
        public float haggleMin = 0.4f;
        public float haggleMax = 1.6f;
        /// <summary>Attitude du propriétaire : à partir de ce score, il tolère un versement manqué de plus avant de renvoyer…</summary>
        public int ownerLenientFrom = 80;
        /// <summary>… et en dessous de celui-ci, un de moins (jamais moins d'un).</summary>
        public int ownerStrictBelow = 20;
        /// <summary>Contrôles de police : chance d'être arrêté, -0,6 % par point au-dessus de neutral (score 100 : ×0,7 ; 0 : ×1,3).</summary>
        public float policeStopPerPoint = -0.006f;
        public float policeStopMin = 0.7f;
        public float policeStopMax = 1.3f;
    }

    /// <summary>Les effets de la réputation, calculés à partir du score (logique pure, testée).</summary>
    public static class ReputationEffects
    {
        private static float Factor(ReputationEffectsConfig e, int score, float perPoint, float min, float max)
        {
            if (e == null || !e.enabled) return 1f;
            var f = 1f + (score - e.neutral) * perPoint;
            return Math.Max(Math.Min(min, max), Math.Min(Math.Max(min, max), f));
        }

        /// <summary>Multiplie la patience d'un client accepté.</summary>
        public static float Patience(ReputationEffectsConfig e, int score) => Factor(e, score, e?.patiencePerPoint ?? 0f, e?.patienceMin ?? 1f, e?.patienceMax ?? 1f);

        /// <summary>Multiplie la chance qu'un client négocie le prix (prix moins bien acceptés quand la réputation est mauvaise).</summary>
        public static float Haggle(ReputationEffectsConfig e, int score) => Factor(e, score, e?.hagglePerPoint ?? 0f, e?.haggleMin ?? 1f, e?.haggleMax ?? 1f);

        /// <summary>Multiplie la chance d'être arrêté par la police.</summary>
        public static float PoliceStop(ReputationEffectsConfig e, int score) => Factor(e, score, e?.policeStopPerPoint ?? 0f, e?.policeStopMin ?? 1f, e?.policeStopMax ?? 1f);

        /// <summary>Versements manqués tolérés en plus (+1, 0 ou -1) par le propriétaire.</summary>
        public static int OwnerTolerance(ReputationEffectsConfig e, int score)
        {
            if (e == null || !e.enabled) return 0;
            if (score >= e.ownerLenientFrom) return 1;
            if (score < e.ownerStrictBelow) return -1;
            return 0;
        }
    }

    /// <summary>
    /// Réputation du chauffeur (jalon 3, feuille de route : clients satisfaits, conduite ; données :
    /// Assets/Resources/Data/reputation.json). Visible ou cachée : question ouverte de Kevin, réglée par
    /// <see cref="visible"/>. Points gagnés et perdus : valeurs par défaut, à régler avec Kevin. Ses effets sur le jeu
    /// (clients, prix, propriétaire, police) : <see cref="ReputationEffects"/> (décision de Kevin du 2026-11-21).
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
        /// <summary>Effets sur le jeu (décision de Kevin du 2026-11-21), coefficients par défaut.</summary>
        public ReputationEffectsConfig effects = new ReputationEffectsConfig();
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

        /// <summary>Effets du score actuel (décision de Kevin du 2026-11-21).</summary>
        public float PatienceFactor => ReputationEffects.Patience(_config.effects, Score);
        public float HaggleFactor => ReputationEffects.Haggle(_config.effects, Score);
        public float PoliceStopFactor => ReputationEffects.PoliceStop(_config.effects, Score);
        public int OwnerTolerance => ReputationEffects.OwnerTolerance(_config.effects, Score);

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
            _roughSeconds = 0f;
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
