using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Rides
{
    /// <summary>
    /// Règles des disputes d'argent avec les passagers (jalon 5, décisions de Kevin du 21/11) ; les valeurs viennent de
    /// dialogues.json (<see cref="TaxiKmer.Core.Talk.DialogueConfig.FareRules"/>). Valeurs par défaut de Claude.
    /// </summary>
    public sealed class FareDisputeRules
    {
        /// <summary>Négociation, option A : chance qu'un client accepté propose moins (avant l'effet de la réputation).</summary>
        public float HaggleChance;
        /// <summary>Il propose entre … et … de moins que le prix annoncé…</summary>
        public float HaggleDiscountMin = 0.15f;
        public float HaggleDiscountMax = 0.35f;
        /// <summary>… arrondi à … FCFA, jamais moins que le plancher.</summary>
        public int HaggleRoundTo = 50;
        public int HaggleFloor = 100;
        /// <summary>Refus de payer, option C : chance qu'un passager arrivé refuse de payer.</summary>
        public float RefusesToPayChance;
        /// <summary>Appeler la police : minutes de jeu perdues, et chance que le policier le fasse payer.</summary>
        public int PoliceMinutes = 30;
        public float PolicePayChance = 0.6f;
    }

    /// <summary>Un client accepté qui propose moins que le prix annoncé (le joueur accepte ou refuse).</summary>
    public sealed class HaggleOffer
    {
        public RoadsideClient Client { get; internal set; }
        /// <summary>Prix annoncé au départ.</summary>
        public int Asked { get; internal set; }
        /// <summary>Ce qu'il propose.</summary>
        public int Offer { get; internal set; }
    }

    /// <summary>Un passager arrivé qui refuse de payer (le joueur le laisse partir ou appelle la police).</summary>
    public sealed class PaymentRefusal
    {
        public Client Client { get; internal set; }
        public int Fare { get; internal set; }
    }

    /// <summary>Ce qu'a donné l'appel à la police pour un passager qui refuse de payer.</summary>
    public struct RefusalOutcome
    {
        public bool PoliceCalled;
        /// <summary>Le policier l'a fait payer.</summary>
        public bool Paid;
        public int Amount;
        public int LostMinutes;
    }

    /// <summary>Calculs purs des disputes d'argent (testés sans rue).</summary>
    public static class FareDisputes
    {
        /// <summary>Le client négocie-t-il ? Rend sa contre-offre, ou 0 s'il accepte le prix.</summary>
        public static int MaybeHaggle(FareDisputeRules rules, int fare, float reputationFactor, Random random)
        {
            if (rules == null || random == null || fare <= 0) return 0;
            var chance = Math.Max(0f, Math.Min(1f, rules.HaggleChance * Math.Max(0f, reputationFactor)));
            if (random.NextDouble() >= chance) return 0;
            var min = Math.Min(rules.HaggleDiscountMin, rules.HaggleDiscountMax);
            var max = Math.Max(rules.HaggleDiscountMin, rules.HaggleDiscountMax);
            var discount = min + random.NextDouble() * (max - min);
            var step = Math.Max(1, rules.HaggleRoundTo);
            var offer = (int)(Math.Round(fare * (1.0 - discount) / step, MidpointRounding.AwayFromZero) * step);
            offer = Math.Max(Math.Min(fare, rules.HaggleFloor), Math.Min(fare - step, offer));
            return offer > 0 && offer < fare ? offer : 0;
        }
    }
}
