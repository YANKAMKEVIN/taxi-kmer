using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Talk
{
    /// <summary>Une bulle à l'écran : la réplique, qui la dit, combien de temps elle reste, et ses réponses rapides.</summary>
    public sealed class Bubble
    {
        public LineData Line { get; internal set; }
        /// <summary>Texte à afficher : la réplique avec ses valeurs remplies (<see cref="DialogueText"/>).</summary>
        public string Text { get; internal set; } = "";
        /// <summary>Registre du passager qui parle (vide : discussion sans passager désigné).</summary>
        public string Register { get; internal set; } = "";
        /// <summary>Personnage type de celui qui parle (vide : passager quelconque).</summary>
        public string Character { get; internal set; } = "";
        /// <summary>Passager qui parle (id du client), vide si personne en particulier.</summary>
        public string SpeakerId { get; internal set; } = "";
        public float ShowSeconds { get; internal set; }
        public float Age { get; internal set; }
        /// <summary>Réponse choisie par le chauffeur (null tant qu'il n'a pas répondu).</summary>
        public ReplyData Reply { get; internal set; }
        public bool HasReplies => Line.replies != null && Line.replies.Length > 0;
        /// <summary>Petit son de voix à jouer avec la bulle (null : aucun) ; son clip peut être vide (à fournir).</summary>
        public VoiceSoundData Voice { get; internal set; }
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
        private float _quietClock;

        public Bubble Current { get; private set; }
        /// <summary>Répliques dites depuis le début (pour mesurer les répétitions).</summary>
        public int Said { get; private set; }
        /// <summary>Réplique dite alors qu'elle l'avait déjà été dans la journée.</summary>
        public int Repeats { get; private set; }
        private readonly HashSet<string> _saidToday = new HashSet<string>();
        private readonly Dictionary<string, string> _assigned = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _characters = new Dictionary<string, string>();
        private readonly List<string> _passengers = new List<string>();
        private bool _passengersKnown;
        private readonly List<FollowUp> _followUps = new List<FollowUp>();

        /// <summary>Situation qui attend que la bulle en cours parte (par ex. la destination juste après la montée).</summary>
        private sealed class FollowUp
        {
            public string Trigger;
            public string SpeakerId;
            public IDictionary<string, string> Values;
            public float SecondsLeft;
        }

        /// <summary>Situations en attente (voir <see cref="Follow"/>).</summary>
        public int PendingFollowUps => _followUps.Count;

        /// <summary>
        /// Une situation qui suit une autre (jalon 5) : le passager qui vient de monter précise sa destination dès que la
        /// bulle en cours est partie et que le silence entre deux bulles est passé. Abandonnée après
        /// <paramref name="withinSeconds"/> secondes (on ne dit pas sa destination au milieu du trajet), ou si le passager
        /// n'est plus à bord (<see cref="SetPassengers"/>).
        /// </summary>
        public void Follow(string trigger, string speakerId, IDictionary<string, string> values = null, float withinSeconds = 20f)
        {
            if (!_config.enabled || string.IsNullOrEmpty(trigger)) return;
            _followUps.Add(new FollowUp { Trigger = trigger, SpeakerId = speakerId ?? "", Values = values, SecondsLeft = withinSeconds });
        }

        /// <summary>Passagers à bord (ids), pour choisir qui rompt un long silence. Mis à jour par la présentation.</summary>
        public void SetPassengers(IEnumerable<string> ids)
        {
            _passengers.Clear();
            _passengersKnown = true;
            if (ids != null) _passengers.AddRange(ids);
        }

        /// <summary>
        /// Personnage type d'un passager : imposé (<see cref="AssignCharacter"/>) ou tiré à partir de son identifiant
        /// (<see cref="DialogueConfig.characterShare"/>) ; vide : passager quelconque. Toujours le même pour un passager.
        /// </summary>
        public string CharacterOf(string passengerId)
        {
            if (string.IsNullOrEmpty(passengerId)) return "";
            if (_characters.TryGetValue(passengerId, out var c)) return c;
            var list = _config.characters;
            if (list == null || list.Length == 0) return "";
            var f = Fraction("perso:" + passengerId);
            var share = Math.Max(0f, Math.Min(1f, _config.characterShare));
            if (f >= share) return "";
            return list[Math.Min(list.Length - 1, (int)(f / share * list.Length))].id;
        }

        public void AssignCharacter(string passengerId, string character)
        {
            if (string.IsNullOrEmpty(passengerId)) return;
            _characters[passengerId] = character ?? "";
        }

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

            // Situations qui suivent (destination après la montée) : avant les discussions et le long silence.
            for (var i = _followUps.Count - 1; i >= 0; i--)
            {
                var f = _followUps[i];
                f.SecondsLeft -= dt;
                var gone2 = f.SecondsLeft <= 0f || (!string.IsNullOrEmpty(f.SpeakerId) && _passengersKnown && !_passengers.Contains(f.SpeakerId));
                if (gone2) _followUps.RemoveAt(i);
            }
            if (Current == null && _followUps.Count > 0 && _silence >= _config.minGapSeconds)
            {
                var f = _followUps[0];
                _followUps.RemoveAt(0);
                Trigger(f.Trigger, f.SpeakerId, "", f.Values);
            }

            if (passengers >= 2 && _config.chatEverySeconds > 0f)
            {
                _chatClock += dt;
                if (Current == null && _chatClock >= _config.chatEverySeconds && Trigger(TalkTriggers.Chat, "")) _chatClock = 0f;
            }
            // Long silence avec au moins un passager : quelqu'un le rompt (situation « silence »).
            if (Current != null || passengers <= 0) _quietClock = 0f;
            else if (_config.silenceAfterSeconds > 0f)
            {
                _quietClock += dt;
                var speaker = _passengers.Count > 0 ? _passengers[_random.Next(_passengers.Count)] : "";
                if (_quietClock >= _config.silenceAfterSeconds && Trigger(TalkTriggers.Silence, speaker)) _quietClock = 0f;
            }
            return gone;
        }

        /// <summary>
        /// Une situation se produit : un passager parle si personne ne parle, si le silence a assez duré et s'il reste
        /// une réplique de cette situation qui n'a pas été dite récemment. Rend true si une bulle s'affiche.
        /// </summary>
        /// <param name="values">
        /// Valeurs du moment que les répliques peuvent citer (<see cref="TalkValues"/> : destination, prix, offre) ;
        /// une réplique qui cite une valeur absente n'est pas choisie.
        /// </param>
        public bool Trigger(string trigger, string speakerId, string character = "", IDictionary<string, string> values = null)
        {
            if (!_config.enabled || Current != null || _silence < _config.minGapSeconds) return false;
            var designated = !string.IsNullOrEmpty(speakerId);
            if (designated && string.IsNullOrEmpty(character)) character = CharacterOf(speakerId);
            var register = string.IsNullOrEmpty(speakerId) ? "" : RegisterOf(speakerId, character);
            var line = Pick(trigger, character, register, designated, values);
            if (line == null && !string.IsNullOrEmpty(register) && !string.IsNullOrEmpty(_config.fallbackRegister) && register != _config.fallbackRegister)
                line = Pick(trigger, character, _config.fallbackRegister, designated, values);
            if (line == null) return false;
            var text = DialogueText.Fill(line.text, values);
            Current = new Bubble { Line = line, Text = text, Register = register, SpeakerId = speakerId ?? "", Character = character ?? "", ShowSeconds = ShowSecondsFor(_config, text), Voice = _config.VoiceFor(line, character) };
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
            _followUps.Clear();
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

        /// <param name="designated">
        /// Un passager précis parle : sans personnage type, il ne dit pas les répliques réservées à un personnage
        /// (la maman ne parle pas de son bébé par la bouche d'un autre).
        /// </param>
        private LineData Pick(string trigger, string character, string register, bool designated = false, IDictionary<string, string> values = null)
        {
            var candidates = new List<LineData>();
            foreach (var l in _config.lines ?? Array.Empty<LineData>())
            {
                if (l.trigger != trigger || string.IsNullOrEmpty(l.text) || _recent.Contains(l.id)) continue;
                if (!DialogueText.CanFill(l.text, values)) continue;
                if (!string.IsNullOrEmpty(register) && !string.IsNullOrEmpty(l.register) && l.register != register) continue;
                if (!string.IsNullOrEmpty(l.character) && !string.IsNullOrEmpty(character) && l.character != character) continue;
                if (!string.IsNullOrEmpty(l.character) && string.IsNullOrEmpty(character) && designated) continue;
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
