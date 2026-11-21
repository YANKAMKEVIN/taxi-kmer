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
    }

    /// <summary>Ce que le policier « trouve ».</summary>
    public struct ControlFinding
    {
        public ControlFindingKind Kind;
        /// <summary>ExpiredPaper : le papier ; Invented : le prétexte.</summary>
        public string Id;
        public string Label;
        /// <summary>Le problème existe vraiment (papier expiré, surcharge) ; false s'il est inventé.</summary>
        public bool Real => Kind == ControlFindingKind.ExpiredPaper || Kind == ControlFindingKind.Overload || Kind == ControlFindingKind.FrontOverload;
    }

    public enum ControlChoice
    {
        Pay,
        Negotiate,
        Contest,
        Call,
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
        public int ReputationDelta { get; private set; }
        public int Negotiations { get; private set; }
        /// <summary>Issue tirée pour contester ou appeler (vide sinon).</summary>
        public string OutcomeId { get; private set; } = "";

        public PoliceControl(PoliceConfig config, PaperSet papers, int day, int passengers, int frontPassengers, Random random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (papers == null) throw new ArgumentNullException(nameof(papers));
            LostMinutes = Math.Max(0, config.checkMinutes);
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
            }
            return false;
        }

        private void Apply(ChoiceOutcomeData o)
        {
            Closed = true;
            if (o == null) { Paid += Demand; return; } // aucune issue possible dans les données : il paie
            OutcomeId = o.id;
            Paid += Round(Demand * Math.Max(0f, o.payFactor));
            LostMinutes += Math.Max(0, o.lostMinutes);
            Impounded = o.impound;
            ReputationDelta += o.reputation;
        }

        private ChoiceOutcomeData Pick(ChoiceOutcomeData[] outcomes)
        {
            if (outcomes == null) return null;
            var total = 0.0;
            foreach (var o in outcomes) total += Math.Max(0f, o.weight);
            if (total <= 0.0) return null;
            var roll = _random.NextDouble() * total;
            foreach (var o in outcomes)
            {
                var w = Math.Max(0f, o.weight);
                if (w <= 0f) continue;
                if (roll < w) return o;
                roll -= w;
            }
            for (var i = outcomes.Length - 1; i >= 0; i--) if (outcomes[i].weight > 0f) return outcomes[i];
            return null;
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
            if (Closed) return list;
            list.Add(ControlChoice.Pay);
            if (CanNegotiate) list.Add(ControlChoice.Negotiate);
            list.Add(ControlChoice.Contest);
            list.Add(ControlChoice.Call);
            return list;
        }
    }
}
