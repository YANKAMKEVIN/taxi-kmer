using System;

namespace TaxiKmer.Core.Incidents
{
    /// <summary>
    /// Réglages d'un piéton renversé (données : Assets/Resources/Data/accidents.json). Règle de Kevin (2026-11-14) :
    /// pas trop grave → on l'emmène à l'hôpital et on paie les premiers soins (argent et temps perdus) ; sinon la
    /// gendarmerie peut interpeller le chauffeur (garde à vue), avec un risque de saisie du véhicule.
    /// Montants en FCFA, vitesses en km/h, durées en minutes de jeu. Valeurs par défaut, à régler avec Kevin.
    /// </summary>
    [Serializable]
    public class AccidentConfig
    {
        /// <summary>En dessous, simple bousculade : pas d'accident.</summary>
        public float minImpactKmh = 4f;
        /// <summary>À partir de cette vitesse au choc, le piéton est gravement blessé.</summary>
        public float seriousImpactKmh = 25f;
        /// <summary>Premiers soins d'un blessé léger (tirés entre les deux bornes).</summary>
        public int lightCareMin = 10000;
        public int lightCareMax = 20000;
        /// <summary>Temps perdu à l'hôpital pour un blessé léger.</summary>
        public int lightGameMinutes = 60;
        /// <summary>Blessé grave : chance que la gendarmerie interpelle le chauffeur.</summary>
        public float seriousArrestChance = 0.6f;
        /// <summary>Interpellé : chance que le véhicule soit saisi.</summary>
        public float seizureChance = 0.3f;
        /// <summary>Blessé grave, sans interpellation : soins à payer et temps perdu.</summary>
        public int seriousCareMin = 40000;
        public int seriousCareMax = 80000;
        public int seriousGameMinutes = 150;
    }

    public enum InjurySeverity
    {
        /// <summary>Bousculade sans gravité : rien ne se passe.</summary>
        None,
        Light,
        Serious,
    }

    /// <summary>Ce qui arrive après avoir renversé un piéton.</summary>
    public struct AccidentOutcome
    {
        public InjurySeverity Severity;
        /// <summary>Soins payés par le chauffeur (FCFA).</summary>
        public int CareCost;
        /// <summary>Temps perdu (minutes de jeu) pour l'emmener à l'hôpital.</summary>
        public int LostGameMinutes;
        /// <summary>Interpellé par la gendarmerie : garde à vue, la journée s'arrête là.</summary>
        public bool Arrested;
        /// <summary>Le taxi est saisi (conséquence sur les jours suivants : jalon 3).</summary>
        public bool VehicleSeized;
        /// <summary>Les passagers descendent sans payer (le taxi part à l'hôpital, ou le chauffeur est interpellé).</summary>
        public bool PassengersLeave;
    }

    public static class PedestrianAccident
    {
        /// <summary>Conséquences d'un choc à <paramref name="impactKmh"/> km/h ; <paramref name="random"/> tire les montants et les risques.</summary>
        public static AccidentOutcome Resolve(AccidentConfig config, float impactKmh, Random random)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var speed = Math.Abs(impactKmh);
            if (speed < config.minImpactKmh) return new AccidentOutcome { Severity = InjurySeverity.None };

            if (speed < config.seriousImpactKmh)
                return new AccidentOutcome
                {
                    Severity = InjurySeverity.Light,
                    CareCost = Between(random, config.lightCareMin, config.lightCareMax),
                    LostGameMinutes = config.lightGameMinutes,
                    PassengersLeave = true,
                };

            if (random.NextDouble() < config.seriousArrestChance)
                return new AccidentOutcome
                {
                    Severity = InjurySeverity.Serious,
                    Arrested = true,
                    VehicleSeized = random.NextDouble() < config.seizureChance,
                    PassengersLeave = true,
                };

            return new AccidentOutcome
            {
                Severity = InjurySeverity.Serious,
                CareCost = Between(random, config.seriousCareMin, config.seriousCareMax),
                LostGameMinutes = config.seriousGameMinutes,
                PassengersLeave = true,
            };
        }

        /// <summary>Montant arrondi à 500 FCFA entre les bornes.</summary>
        private static int Between(Random random, int min, int max)
        {
            if (max < min) max = min;
            var v = min + random.NextDouble() * (max - min);
            return (int)(Math.Round(v / 500.0) * 500);
        }
    }
}
