using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Police
{
    public enum ControlFindingKind
    {
        /// <summary>Tout est en règle et le policier laisse partir.</summary>
        None,
        /// <summary>Un papier est expiré (problème réel).</summary>
        ExpiredPaper,
        /// <summary>Trop de passagers (problème réel au regard de la règle des données).</summary>
        Overload,
        /// <summary>Deux passagers devant.</summary>
        FrontOverload,
        /// <summary>Tout est en règle, le policier invente un problème.</summary>
        Invented,
        /// <summary>Le chauffeur a fui un contrôle plus tôt dans la journée : il est recherché (fuir, option B).</summary>
        Fled,
    }

    /// <summary>Ce que le policier « trouve ».</summary>
    public struct ControlFinding
    {
        public ControlFindingKind Kind;
        /// <summary>ExpiredPaper : le papier ; Invented : le prétexte.</summary>
        public string Id;
        public string Label;
        /// <summary>Le problème existe vraiment (papier expiré, surcharge) ; false s'il est inventé.</summary>
        public bool Real => Kind == ControlFindingKind.ExpiredPaper || Kind == ControlFindingKind.Overload || Kind == ControlFindingKind.FrontOverload || Kind == ControlFindingKind.Fled;
    }

    public enum ControlChoice
    {
        Pay,
        Negotiate,
        Contest,
        Call,
        /// <summary>Fuir le contrôle (option B, tranché par Kevin le 21/11) : poursuite, fourrière ou recherché.</summary>
        Flee,
    }

    /// <summary>
    /// Un contrôle de police (jalon 4) : le policier vérifie les papiers et le taxi, trouve un problème (ou l'invente),
    /// demande une somme ; le chauffeur paie, négocie, conteste ou appelle quelqu'un. Logique pure : la présentation
    /// lit <see cref="Finding"/> et <see cref="Demand"/>, appelle <see cref="Choose"/>, puis applique le résultat à la
    /// journée (<see cref="TaxiKmer.Core.Rides.Street.ApplyPoliceControl"/>).
    /// </summary>
    public sealed class PoliceControl
    {
        private readonly PoliceConfig _config;
        private readonly Random _random;

        public ControlFinding Finding { get; }
        /// <summary>Somme demandée en ce moment (FCFA).</summary>
        public int Demand { get; private set; }
        public bool Closed { get; private set; }
        /// <summary>Ce que le chauffeur a payé au policier (FCFA).</summary>
        public int Paid { get; private set; }
        /// <summary>Minutes de jeu perdues (contrôle compris).</summary>
        public int LostMinutes { get; private set; }
        public bool Impounded { get; private set; }
        /// <summary>Le chauffeur est arrêté (garde à vue) : la journée s'arrête.</summary>
        public bool Arrested { get; private set; }
        /// <summary>Le chauffeur a fui et s'est échappé : il est recherché jusqu'à la fin de la journée.</summary>
        public bool BecameWanted { get; private set; }
        /// <summary>Le chauffeur a un contact à appeler (crochet du jalon 7).</summary>
        public bool HasContact { get; }
        /// <summary>Les papiers sont en règle (condition pour gagner en contestant).</summary>
        public bool PapersInOrder { get; }
        public int ReputationDelta { get; private set; }
        public int Negotiations { get; private set; }
        /// <summary>Le chauffeur a fui et la police le poursuit (idée n° 9) : le contrôle se termine avec <see cref="EndChase"/>.</summary>
        public bool Chasing { get; private set; }
        /// <summary>Issue tirée pour contester ou appeler (vide sinon).</summary>
        public string OutcomeId { get; private set; } = "";

        /// <param name="hasContact">Le chauffeur a un contact (<see cref="PoliceContacts.HasContact"/>).</param>
        /// <param name="wanted">Il a fui un contrôle plus tôt dans la journée : fourrière, arrestation ou contrôle ordinaire.</param>
        public PoliceControl(PoliceConfig config, PaperSet papers, int day, int passengers, int frontPassengers, Random random,
            bool hasContact = false, bool wanted = false)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (papers == null) throw new ArgumentNullException(nameof(papers));
            HasContact = hasContact;
            PapersInOrder = papers.Expired(day).Count == 0;
            LostMinutes = Math.Max(0, config.checkMinutes);
            if (wanted)
            {
                Finding = new ControlFinding { Kind = ControlFindingKind.Fled, Id = "fuite", Label = "Vous avez fui un contrôle" };
                Demand = Round(config.demandFled);
                var o = Pick(config.wantedOutcomes);
                if (o != null) OutcomeId = o.id;
                if (o != null && (o.impound || o.arrest)) Apply(o);
                return;
            }
            Finding = Find(config, papers, day, passengers, frontPassengers, random);
            Demand = Finding.Kind switch
            {
                ControlFindingKind.ExpiredPaper => Round(config.demandExpiredPaper),
                ControlFindingKind.Overload => Round(config.demandOverload),
                ControlFindingKind.FrontOverload => Round(config.demandOverload),
                ControlFindingKind.Invented => Round(config.demandInventedMin + random.NextDouble() * Math.Max(0, config.demandInventedMax - config.demandInventedMin)),
                _ => 0,
            };
            if (Finding.Kind == ControlFindingKind.None) Closed = true;
        }

        /// <summary>Problème réel d'abord (papier expiré, surcharge, deux devant), sinon peut-être un prétexte inventé.</summary>
        public static ControlFinding Find(PoliceConfig config, PaperSet papers, int day, int passengers, int frontPassengers, Random random)
        {
            var expired = papers.Expired(day);
            if (expired.Count > 0)
                return new ControlFinding { Kind = ControlFindingKind.ExpiredPaper, Id = expired[0].id, Label = $"{expired[0].name} expirée" };
            if (passengers > config.legalMaxPassengers)
                return new ControlFinding { Kind = ControlFindingKind.Overload, Id = "surcharge", Label = $"Surcharge : {passengers} passagers" };
            if (frontPassengers > config.legalMaxFront)
                return new ControlFinding { Kind = ControlFindingKind.FrontOverload, Id = "deux-devant", Label = $"{frontPassengers} passagers devant" };
            var pretexts = config.pretexts ?? Array.Empty<PretextData>();
            if (pretexts.Length > 0 && random.NextDouble() < config.inventChanceWhenInOrder)
            {
                var p = pretexts[random.Next(pretexts.Length)];
                return new ControlFinding { Kind = ControlFindingKind.Invented, Id = p.id, Label = p.label };
            }
            return new ControlFinding { Kind = ControlFindingKind.None, Id = "", Label = "Papiers en règle" };
        }

        /// <summary>On peut encore négocier.</summary>
        public bool CanNegotiate => !Closed && Negotiations < _config.maxNegotiations;

        /// <summary>Le chauffeur choisit ; rend false si ce choix n'est pas possible (contrôle clos, plus de négociation).</summary>
        public bool Choose(ControlChoice choice)
        {
            if (Closed) return false;
            switch (choice)
            {
                case ControlChoice.Pay:
                    Paid += Demand;
                    ReputationDelta += _config.reputationPaid;
                    Closed = true;
                    return true;
                case ControlChoice.Negotiate:
                    if (!CanNegotiate) return false;
                    Negotiations++;
                    LostMinutes += Math.Max(0, _config.negotiateMinutes);
                    Demand = _random.NextDouble() < _config.negotiateFailChance
                        ? Round(Demand * (1f + _config.negotiateHarden))
                        : Math.Max(Math.Min(Demand, _config.negotiateFloor), Round(Demand * (1f - _config.negotiateDiscount)));
                    return true;
                case ControlChoice.Contest:
                    Apply(Pick(_config.contestOutcomes));
                    return true;
                case ControlChoice.Call:
                    Apply(Pick(_config.callOutcomes));
                    return true;
                case ControlChoice.Flee:
                    if (!_config.fleeEnabled || Chasing) return false;
                    if (_config.chase != null && _config.chase.enabled)
                    {
                        Chasing = true; // la poursuite décide de l'issue (EndChase)
                        return true;
                    }
                    Apply(Pick(_config.fleeOutcomes));
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Fin de la poursuite : rattrapé → l'issue de fuite qui mène à la fourrière (ou à l'arrestation) ; échappé →
        /// celle qui rend recherché. Sans issue de ce genre dans les données, tirage de fleeOutcomes.
        /// </summary>
        public void EndChase(bool caught)
        {
            if (!Chasing || Closed) return;
            Chasing = false;
            ChoiceOutcomeData match = null;
            foreach (var o in _config.fleeOutcomes ?? Array.Empty<ChoiceOutcomeData>())
                if (caught ? o.impound || o.arrest : o.wanted) { match = o; break; }
            Apply(match ?? Pick(_config.fleeOutcomes));
        }

        private void Apply(ChoiceOutcomeData o)
        {
            Closed = true;
            if (o == null) { Paid += Demand; return; } // aucune issue possible dans les données : il paie
            OutcomeId = o.id;
            Paid += Round(Demand * Math.Max(0f, o.payFactor));
            LostMinutes += Math.Max(0, o.lostMinutes);
            Impounded = o.impound;
            Arrested = o.arrest;
            BecameWanted = o.wanted;
            ReputationDelta += o.reputation;
        }

        /// <summary>
        /// L'issue est-elle possible dans ce contrôle ? « gagne » seulement si le problème est inventé et les papiers en
        /// règle ; « arrange » seulement avec un contact ; « personne » seulement sans contact.
        /// </summary>
        public bool Possible(ChoiceOutcomeData o)
        {
            if (o == null || o.weight <= 0f) return false;
            if (o.requiresInvented && !(Finding.Kind == ControlFindingKind.Invented && PapersInOrder)) return false;
            if (o.requiresContact && !HasContact) return false;
            if (o.onlyWithoutContact && HasContact) return false;
            return true;
        }

        /// <summary>Chance de chaque issue dans ce contrôle (0 si impossible), pour les tests et l'équilibrage.</summary>
        public double ChanceOf(ChoiceOutcomeData[] outcomes, string id)
        {
            if (outcomes == null) return 0.0;
            var total = 0.0;
            var mine = 0.0;
            foreach (var o in outcomes)
            {
                if (!Possible(o)) continue;
                total += o.weight;
                if (o.id == id) mine += o.weight;
            }
            return total > 0.0 ? mine / total : 0.0;
        }

        private ChoiceOutcomeData Pick(ChoiceOutcomeData[] outcomes)
        {
            if (outcomes == null) return null;
            var total = 0.0;
            foreach (var o in outcomes) if (Possible(o)) total += o.weight;
            if (total <= 0.0) return null;
            var roll = _random.NextDouble() * total;
            ChoiceOutcomeData last = null;
            foreach (var o in outcomes)
            {
                if (!Possible(o)) continue;
                last = o;
                if (roll < o.weight) return o;
                roll -= o.weight;
            }
            return last;
        }

        private int Round(double amount)
        {
            var step = Math.Max(1, _config.roundTo);
            return Math.Max(0, (int)(Math.Round(amount / step, MidpointRounding.AwayFromZero) * step));
        }

        /// <summary>Choix possibles, pour les boutons (négocier disparaît quand on ne peut plus).</summary>
        public List<ControlChoice> AvailableChoices()
        {
            var list = new List<ControlChoice>();
            if (Closed || Chasing) return list;
            list.Add(ControlChoice.Pay);
            if (CanNegotiate) list.Add(ControlChoice.Negotiate);
            list.Add(ControlChoice.Contest);
            list.Add(ControlChoice.Call);
            if (_config.fleeEnabled) list.Add(ControlChoice.Flee);
            return list;
        }
    }
}
