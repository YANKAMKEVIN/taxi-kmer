using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Talk
{
    /// <summary>
    /// Avancement des histoires de passagers réguliers (idée n° 13) : l'étudiant qui revient chaque matin jusqu'à son
    /// examen, la commerçante qui finit par donner un pourboire. Une réplique d'histoire n'est dite qu'à son tour, au
    /// plus une rencontre par jour d'écart ; l'avancement se garde dans la sauvegarde (<c>career.stories</c>).
    /// </summary>
    public sealed class StoryArcs
    {
        private readonly Dictionary<string, StoryArcData> _arcs = new Dictionary<string, StoryArcData>();
        private readonly Dictionary<string, (int done, int lastDay)> _progress = new Dictionary<string, (int, int)>();

        /// <summary>Jour de carrière en cours.</summary>
        public int Today { get; set; } = 1;
        /// <summary>L'avancement a changé depuis la dernière écriture dans la sauvegarde.</summary>
        public bool Changed { get; set; }

        public StoryArcs(IEnumerable<StoryArcData> arcs, IEnumerable<string> saved = null, int today = 1)
        {
            foreach (var a in arcs ?? Array.Empty<StoryArcData>())
                if (!string.IsNullOrEmpty(a.id)) _arcs[a.id] = a;
            Today = today;
            foreach (var entry in saved ?? Array.Empty<string>())
            {
                var parts = (entry ?? "").Split(':');
                if (parts.Length == 3 && _arcs.ContainsKey(parts[0]) && int.TryParse(parts[1], out var done) && int.TryParse(parts[2], out var day))
                    _progress[parts[0]] = (Math.Max(0, done), day);
            }
        }

        /// <summary>Rencontres déjà faites dans cette histoire.</summary>
        public int Done(string arc) => _progress.TryGetValue(arc ?? "", out var p) ? p.done : 0;

        public bool Finished(string arc) => _arcs.TryGetValue(arc ?? "", out var a) && Done(arc) >= a.steps;

        /// <summary>
        /// La réplique peut être dite maintenant : hors histoire toujours ; dans une histoire seulement si c'est la
        /// rencontre suivante et que assez de jours ont passé depuis la précédente.
        /// </summary>
        public bool Eligible(LineData line)
        {
            if (line == null || string.IsNullOrEmpty(line.arc)) return true;
            if (!_arcs.TryGetValue(line.arc, out var arc)) return false;
            if (!_progress.TryGetValue(line.arc, out var p)) p = (0, int.MinValue / 2);
            if (line.arcStep != p.done + 1 || p.done >= arc.steps) return false;
            return Today - p.lastDay >= Math.Max(0, arc.minDaysBetween);
        }

        /// <summary>La réplique a été dite : l'histoire avance d'une rencontre.</summary>
        public void Said(LineData line)
        {
            if (line == null || string.IsNullOrEmpty(line.arc) || !Eligible(line)) return;
            _progress[line.arc] = (line.arcStep, Today);
            Changed = true;
        }

        /// <summary>Pour la sauvegarde : « arc:rencontres:dernier jour ».</summary>
        public string[] Save()
        {
            var list = new List<string>();
            foreach (var kv in _progress) list.Add($"{kv.Key}:{kv.Value.done}:{kv.Value.lastDay}");
            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }
    }
}
