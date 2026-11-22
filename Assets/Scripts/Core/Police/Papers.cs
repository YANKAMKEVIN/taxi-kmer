using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Police
{
    /// <summary>Les papiers du taxi et du chauffeur, avec leur date d'expiration (jour de la carrière).</summary>
    public sealed class PaperSet
    {
        private readonly PoliceConfig _config;
        /// <summary>Dernier jour de validité de chaque papier (int.MaxValue : n'expire jamais).</summary>
        private readonly Dictionary<string, int> _lastValidDay = new Dictionary<string, int>();

        public PaperSet(PoliceConfig config, int startDay = 1)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            foreach (var p in Papers)
            {
                var left = p.startDaysLeft > 0 ? p.startDaysLeft : p.validityDays;
                _lastValidDay[p.id] = p.validityDays <= 0 ? int.MaxValue : startDay + left - 1;
            }
        }

        public IReadOnlyList<PaperData> Papers => _config.papers ?? Array.Empty<PaperData>();

        public PaperData Find(string id)
        {
            foreach (var p in Papers) if (p.id == id) return p;
            return null;
        }

        /// <summary>Dernier jour où le papier est valable (int.MaxValue s'il n'expire jamais).</summary>
        public int LastValidDay(string id) => _lastValidDay.TryGetValue(id, out var d) ? d : int.MaxValue;

        public bool IsValid(string id, int day) => day <= LastValidDay(id);

        /// <summary>Jours qui restent, le jour même compris (0 : expiré ; int.MaxValue : n'expire jamais).</summary>
        public int DaysLeft(string id, int day)
        {
            var last = LastValidDay(id);
            return last == int.MaxValue ? int.MaxValue : Math.Max(0, last - day + 1);
        }

        /// <summary>Papiers expirés ce jour-là, dans l'ordre des données.</summary>
        public List<PaperData> Expired(int day)
        {
            var list = new List<PaperData>();
            foreach (var p in Papers) if (!IsValid(p.id, day)) list.Add(p);
            return list;
        }

        /// <summary>Renouvelle un papier à partir de ce jour ; rend son coût (0 si inconnu ou s'il n'expire jamais).</summary>
        public int Renew(string id, int day)
        {
            var p = Find(id);
            if (p == null || p.validityDays <= 0) return 0;
            // Renouvelé à temps : la nouvelle période suit l'ancienne ; expiré : elle part de ce jour.
            var from = Math.Max(day, LastValidDay(id) + 1);
            _lastValidDay[id] = from + p.validityDays - 1;
            return Math.Max(0, p.renewalCost);
        }

        /// <summary>Reprend une date d'expiration sauvegardée.</summary>
        public void Restore(string id, int lastValidDay)
        {
            if (Find(id) != null) _lastValidDay[id] = lastValidDay;
        }

        /// <summary>« Assurance : 3 jours » / « Assurance : expirée » / « Permis de conduire : valable ».</summary>
        public string Status(string id, int day)
        {
            var p = Find(id);
            if (p == null) return "";
            var left = DaysLeft(id, day);
            if (left == int.MaxValue) return $"{p.name} : valable";
            if (left == 0) return $"{p.name} : expirée";
            return $"{p.name} : {left} jour{(left > 1 ? "s" : "")}";
        }

        /// <summary>
        /// Les papiers qui expirent, en une ligne pour l'écran de pause (jalon 4) : « Assurance : 9 jours · Visite
        /// technique : 160 jours · Licence de taxi : 300 jours ». Les papiers qui n'expirent pas (permis, carte grise)
        /// sont omis ; un papier expiré est annoncé en premier. Vide si aucun papier n'expire.
        /// </summary>
        public string Summary(int day)
        {
            var expired = new List<string>();
            var valid = new List<string>();
            foreach (var p in Papers)
            {
                if (LastValidDay(p.id) == int.MaxValue) continue;
                (DaysLeft(p.id, day) == 0 ? expired : valid).Add(Status(p.id, day));
            }
            expired.AddRange(valid);
            return string.Join(" · ", expired);
        }
    }
}
