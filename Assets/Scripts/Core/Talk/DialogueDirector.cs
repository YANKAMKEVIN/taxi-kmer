using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Talk
{
    /// <summary>Une bulle à l'écran : la réplique, qui la dit, combien de temps elle reste, et ses réponses rapides.</summary>
    public sealed class Bubble
    {
        public LineData Line { get; internal set; }
        /// <summary>Registre du passager qui parle (vide : discussion sans passager désigné).</summary>
        public string Register { get; internal set; } = "";
        /// <summary>Passager qui parle (id du client), vide si personne en particulier.</summary>
        public string SpeakerId { get; internal set; } = "";
        public float ShowSeconds { get; internal set; }
        public float Age { get; internal set; }
        /// <summary>Réponse choisie par le chauffeur (null tant qu'il n'a pas répondu).</summary>
        public ReplyData Reply { get; internal set; }
        public bool HasReplies => Line.replies != null && Line.replies.Length > 0;
        /// <summary>Les boutons de réponse sont encore proposés.</summary>
        public bool RepliesOpen(float replySeconds) => HasReplies && Reply == null && Age < replySeconds;
    }

    /// <summary>
    /// Choisit qui dit quoi et quand (jalon 5) : une bulle à la fois, un silence entre deux, le temps de la lire en
    /// conduisant, pas de réplique répétée trop tôt (on préfère se taire que se répéter), réponses rapides qui
    /// expirent seules. Les répliques viennent des données (<see cref="DialogueConfig"/>).
    /// </summary>
    public sealed class DialogueDirector
    {
        private readonly DialogueConfig _config;
        private readonly Random _random;
        private readonly List<string> _recent = new List<string>();
        private float _silence;
        private float _chatClock;

        public Bubble Current { get; private set; }
        /// <summary>Répliques dites depuis le début (pour mesurer les répétitions).</summary>
        public int Said { get; private set; }
        /// <summary>Réplique dite alors qu'elle l'avait déjà été dans la journée.</summary>
        public int Repeats { get; private set; }
        private readonly HashSet<string> _saidToday = new HashSet<string>();
        private readonly Dictionary<string, string> _assigned = new Dictionary<string, string>();

        public DialogueDirector(DialogueConfig config, Random random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _silence = config.minGapSeconds;
        }

        public bool Enabled => _config.enabled;

        /// <summary>Temps d'affichage d'un texte : de quoi le lire en conduisant.</summary>
        public static float ShowSecondsFor(DialogueConfig config, string text)
        {
            var len = string.IsNullOrEmpty(text) ? 0 : text.Length;
            return Math.Max(config.minShowSeconds, config.charsPerSecond > 0f ? len / config.charsPerSecond : 0f);
        }

        /// <summary>Fait avancer le temps : la bulle vieillit puis part ; le silence se compte ; les discussions se déclenchent.</summary>
        /// <returns>La bulle qui vient de partir (null sinon), pour appliquer la réponse choisie.</returns>
        public Bubble Update(float dt, int passengers)
        {
            Bubble gone = null;
            if (Current != null)
            {
                Current.Age += dt;
                var stay = Math.Max(Current.ShowSeconds, Current.HasReplies && Current.Reply == null ? _config.replySeconds : 0f);
                if (Current.Age >= stay)
                {
                    gone = Current;
                    Current = null;
                    _silence = 0f;
                }
            }
            else _silence += dt;

            if (passengers >= 2 && _config.chatEverySeconds > 0f)
            {
                _chatClock += dt;
                if (Current == null && _chatClock >= _config.chatEverySeconds && Trigger(TalkTriggers.Chat, "")) _chatClock = 0f;
            }
            return gone;
        }

        /// <summary>
        /// Une situation se produit : un passager parle si personne ne parle, si le silence a assez duré et s'il reste
        /// une réplique de cette situation qui n'a pas été dite récemment. Rend true si une bulle s'affiche.
        /// </summary>
        public bool Trigger(string trigger, string speakerId, string character = "")
        {
            if (!_config.enabled || Current != null || _silence < _config.minGapSeconds) return false;
            var register = string.IsNullOrEmpty(speakerId) ? "" : RegisterOf(speakerId, character);
            var line = Pick(trigger, character, register);
            if (line == null && !string.IsNullOrEmpty(register) && !string.IsNullOrEmpty(_config.fallbackRegister) && register != _config.fallbackRegister)
                line = Pick(trigger, character, _config.fallbackRegister);
            if (line == null) return false;
            Current = new Bubble { Line = line, Register = register, SpeakerId = speakerId ?? "", ShowSeconds = ShowSecondsFor(_config, line.text) };
            Said++;
            if (!_saidToday.Add(line.id)) Repeats++;
            _recent.Add(line.id);
            while (_recent.Count > Math.Max(0, _config.noRepeatWithin)) _recent.RemoveAt(0);
            return true;
        }

        /// <summary>Le chauffeur répond (bouton rapide) ; false si plus possible.</summary>
        public bool Answer(string replyId)
        {
            if (Current == null || !Current.RepliesOpen(_config.replySeconds)) return false;
            foreach (var r in Current.Line.replies)
                if (r.id == replyId)
                {
                    Current.Reply = r;
                    return true;
                }
            return false;
        }

        /// <summary>Nouvelle journée : les répétitions se comptent à nouveau à partir de zéro.</summary>
        public void NewDay()
        {
            _saidToday.Clear();
            Said = 0;
            Repeats = 0;
        }

        /// <summary>
        /// Registre d'un passager : celui qu'on lui a donné (<see cref="AssignRegister"/>), sinon celui de son personnage
        /// type, sinon <see cref="RegisterFor"/>. Toujours le même pour un même passager.
        /// </summary>
        public string RegisterOf(string passengerId, string character = "")
        {
            if (string.IsNullOrEmpty(passengerId)) return "";
            if (_assigned.TryGetValue(passengerId, out var r)) return r;
            if (!string.IsNullOrEmpty(character) && _config.characters != null)
                foreach (var c in _config.characters)
                    if (c.id == character && !string.IsNullOrEmpty(c.register)) return c.register;
            return RegisterFor(_config, passengerId);
        }

        /// <summary>Impose le registre d'un passager (par ex. choisi par la présentation ou repris d'une sauvegarde).</summary>
        public void AssignRegister(string passengerId, string register)
        {
            if (string.IsNullOrEmpty(passengerId)) return;
            if (string.IsNullOrEmpty(register)) _assigned.Remove(passengerId);
            else _assigned[passengerId] = register;
        }

        /// <summary>
        /// Registre tiré pour un passager selon les parts des données (<see cref="DialogueConfig.registers"/>), à partir
        /// de son identifiant : le même passager parle toujours de la même façon, sans toucher au hasard du jeu.
        /// </summary>
        public static string RegisterFor(DialogueConfig config, string passengerId)
        {
            var registers = config?.registers;
            if (registers == null || registers.Length == 0) return "";
            var total = 0.0;
            foreach (var r in registers) total += Math.Max(0f, r.weight);
            if (total <= 0.0) return "";
            var roll = Fraction(passengerId ?? "") * total;
            foreach (var r in registers)
            {
                var w = Math.Max(0f, r.weight);
                if (w <= 0f) continue;
                if (roll < w) return r.id;
                roll -= w;
            }
            for (var i = registers.Length - 1; i >= 0; i--) if (registers[i].weight > 0f) return registers[i].id;
            return "";
        }

        /// <summary>Empreinte FNV-1a 32 bits de l'identifiant ramenée dans [0, 1).</summary>
        private static double Fraction(string id)
        {
            var h = 2166136261u;
            foreach (var ch in id)
            {
                h ^= ch;
                h *= 16777619u;
            }
            h ^= h >> 15; // mélange les derniers caractères (« c1 », « c2 »…) dans les bits de poids fort
            h *= 0x2c1b3c6du;
            h ^= h >> 12;
            return h / 4294967296.0;
        }

        private LineData Pick(string trigger, string character, string register)
        {
            var candidates = new List<LineData>();
            foreach (var l in _config.lines ?? Array.Empty<LineData>())
            {
                if (l.trigger != trigger || string.IsNullOrEmpty(l.text) || _recent.Contains(l.id)) continue;
                if (!string.IsNullOrEmpty(register) && !string.IsNullOrEmpty(l.register) && l.register != register) continue;
                if (!string.IsNullOrEmpty(l.character) && !string.IsNullOrEmpty(character) && l.character != character) continue;
                candidates.Add(l);
            }
            if (candidates.Count == 0) return null;
            // Les répliques pas encore dites aujourd'hui d'abord.
            var fresh = candidates.FindAll(l => !_saidToday.Contains(l.id));
            var pool = fresh.Count > 0 ? fresh : candidates;
            return pool[_random.Next(pool.Count)];
        }
    }

    /// <summary>
    /// Détour (jalon 5 : plaintes sur le détour) : suit, pour chaque passager, la distance qui le sépare de sa destination
    /// le long de l'itinéraire ; quand le taxi s'en est éloigné de <see cref="DialogueConfig.detourMeters"/> depuis le
    /// point le plus proche atteint, le passager se plaint (une fois, puis de nouveau s'il s'éloigne encore autant).
    /// </summary>
    public sealed class DetourWatch
    {
        private readonly float _threshold;
        private readonly Dictionary<string, float> _closest = new Dictionary<string, float>();

        public DetourWatch(float detourMeters) => _threshold = Math.Max(1f, detourMeters);

        /// <summary>Le taxi est à <paramref name="along"/> ; rend true si ce passager se plaint maintenant.</summary>
        public bool Update(string passengerId, float destinationAlong, float along)
        {
            var d = Math.Abs(destinationAlong - along);
            if (!_closest.TryGetValue(passengerId, out var best) || d < best)
            {
                _closest[passengerId] = d;
                return false;
            }
            if (d - best < _threshold) return false;
            _closest[passengerId] = d; // repart de là : il se plaindra encore s'il s'éloigne d'autant
            return true;
        }

        /// <summary>Le passager est descendu.</summary>
        public void Forget(string passengerId) => _closest.Remove(passengerId);
    }
}
