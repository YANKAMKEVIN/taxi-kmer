using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Career
{
    /// <summary>Une compétence du chauffeur : points gagnés, niveau, effet par niveau.</summary>
    [Serializable]
    public class SkillData
    {
        public string id = "";
        public string name = "";
        /// <summary>Points pour passer chaque niveau (le niveau n demande n × pointsPerLevel au total).</summary>
        public int pointsPerLevel = 10;
        public int maxLevel = 10;
        /// <summary>Effet par niveau (part ; 0,03 = 3 % par niveau), sens selon la compétence (voir <see cref="DriverSkills"/>).</summary>
        public float effectPerLevel = 0.03f;
    }

    /// <summary>
    /// Compétences du chauffeur qui progressent avec les journées (idée n° 50, proposition à soumettre à Kevin ; données :
    /// Assets/Resources/Data/skills.json). <b>Coupées par défaut</b> (enabled = false) : décision de design de Kevin.
    /// Négociation : gagnée en négociant (clients, police) ; réduit le rabais demandé par les clients qui marchandent et
    /// la chance qu'une négociation avec la police échoue. Endurance : gagnée par journée complète ; allonge la patience
    /// des clients acceptés. Raccourcis : gagnée au kilomètre ; les passagers se plaignent plus tard d'un détour.
    /// </summary>
    [Serializable]
    public class SkillsConfig
    {
        public bool enabled;
        public SkillData[] skills =
        {
            new SkillData { id = DriverSkills.Negotiation, name = "Négociation", pointsPerLevel = 6, maxLevel = 10, effectPerLevel = 0.04f },
            new SkillData { id = DriverSkills.Endurance, name = "Endurance", pointsPerLevel = 3, maxLevel = 10, effectPerLevel = 0.02f },
            new SkillData { id = DriverSkills.Shortcuts, name = "Raccourcis", pointsPerLevel = 40, maxLevel = 10, effectPerLevel = 0.05f },
        };
    }

    /// <summary>Ce que la journée a apporté aux compétences.</summary>
    public struct SkillDay
    {
        /// <summary>Négociations (clients qui marchandent, policiers).</summary>
        public int Negotiations;
        /// <summary>Journée menée jusqu'au bout (pas de renvoi, de garde à vue ni de fourrière).</summary>
        public bool FullDay;
        public float Kilometres;
    }

    /// <summary>Points et niveaux des compétences, gardés dans la sauvegarde (« id:points ») ; effets bornés.</summary>
    public sealed class DriverSkills
    {
        public const string Negotiation = "negociation";
        public const string Endurance = "endurance";
        public const string Shortcuts = "raccourcis";

        private readonly SkillsConfig _config;
        private readonly Dictionary<string, int> _points = new Dictionary<string, int>();

        public DriverSkills(SkillsConfig config, IEnumerable<string> saved = null)
        {
            _config = config ?? new SkillsConfig();
            foreach (var entry in saved ?? Array.Empty<string>())
            {
                var parts = (entry ?? "").Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out var p) && Find(parts[0]) != null) _points[parts[0]] = Math.Max(0, p);
            }
        }

        public bool Enabled => _config.enabled;

        public int Points(string id) => _points.TryGetValue(id, out var p) ? p : 0;

        public int Level(string id)
        {
            var s = Find(id);
            if (s == null || s.pointsPerLevel <= 0) return 0;
            return Math.Min(s.maxLevel, Points(id) / s.pointsPerLevel);
        }

        /// <summary>Effet total (part) de la compétence : niveau × effet par niveau ; 0 si coupé.</summary>
        public float Effect(string id)
        {
            if (!_config.enabled) return 0f;
            var s = Find(id);
            return s == null ? 0f : Level(id) * s.effectPerLevel;
        }

        /// <summary>Fin de journée : points gagnés. Rend les compétences qui ont gagné un niveau (pour un message).</summary>
        public List<string> EndDay(SkillDay day)
        {
            var up = new List<string>();
            if (!_config.enabled) return up;
            Gain(Negotiation, Math.Max(0, day.Negotiations), up);
            Gain(Endurance, day.FullDay ? 1 : 0, up);
            Gain(Shortcuts, (int)Math.Floor(Math.Max(0f, day.Kilometres)), up);
            return up;
        }

        /// <summary>Rabais demandé par un client qui marchande, réduit par la négociation (jamais sous zéro).</summary>
        public float HaggleDiscount(float discount) => Math.Max(0f, discount * (1f - Effect(Negotiation)));

        /// <summary>Chance d'échec d'une négociation avec la police, réduite par la négociation.</summary>
        public float PoliceNegotiateFail(float chance) => Math.Max(0f, chance * (1f - Effect(Negotiation)));

        /// <summary>Patience des clients acceptés, allongée par l'endurance.</summary>
        public float PatienceFactor => 1f + Effect(Endurance);

        /// <summary>Distance de détour tolérée par les passagers, allongée par la connaissance des raccourcis.</summary>
        public float DetourFactor => 1f + Effect(Shortcuts);

        public string[] Save()
        {
            var list = new List<string>();
            foreach (var kv in _points) list.Add($"{kv.Key}:{kv.Value}");
            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }

        /// <summary>« Négociation niv. 3 · Endurance niv. 1 · Raccourcis niv. 0 » (pause) ; vide si coupé.</summary>
        public string Summary()
        {
            if (!_config.enabled) return "";
            var parts = new List<string>();
            foreach (var s in _config.skills ?? Array.Empty<SkillData>()) parts.Add($"{s.name} niv. {Level(s.id)}");
            return string.Join(" · ", parts);
        }

        private void Gain(string id, int points, List<string> up)
        {
            if (points <= 0 || Find(id) == null) return;
            var before = Level(id);
            _points[id] = Points(id) + points;
            if (Level(id) > before) up.Add(Find(id).name);
        }

        private SkillData Find(string id)
        {
            foreach (var s in _config.skills ?? Array.Empty<SkillData>()) if (s.id == id) return s;
            return null;
        }
    }
}
