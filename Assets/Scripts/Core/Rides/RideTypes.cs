using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Rides
{
    /// <summary>Les trois types de course (décision de Kevin, 2026-11-11, vision §4.1).</summary>
    public enum RideKind
    {
        /// <summary>Course partagée : plusieurs clients, chacun sa destination (jalon 1).</summary>
        Shared,
        /// <summary>Dépôt : une personne (ou son groupe) privatise le taxi, prix négocié, plus cher.</summary>
        Depot,
        /// <summary>Course via l'appli fictive : seulement les passagers de la commande.</summary>
        App,
    }

    /// <summary>Dépôt (jalon 8). Prix et marchandage : valeurs provisoires (question 2 du jalon 8).</summary>
    [Serializable]
    public class DepotConfig
    {
        /// <summary>Le client propose … fois le prix d'une course partagée sur la même distance, par place du taxi.</summary>
        public float offerPerSeatFactor = 1f;
        /// <summary>Places du taxi qu'il paie (le taxi est à lui seul).</summary>
        public int seatsPaid = 5;
        /// <summary>Demander plus : le client accepte avec une chance qui baisse de … par tranche de 10 % demandée en plus.</summary>
        public float acceptDropPer10Percent = 0.25f;
        /// <summary>Chance qu'un client qui fait signe veuille un dépôt (0 : jamais ; à décider par Kevin).</summary>
        public float depotClientChance;
        public int roundTo = 100;
    }

    /// <summary>L'appli fictive (jalon 8). Nom et commission : questions 1 et 2 du jalon 8.</summary>
    [Serializable]
    public class AppConfig
    {
        /// <summary>Nom affiché (à choisir par Kevin ; propositions de la vision : Allô Taxi, Kmer Ride, Mbolo).</summary>
        public string appName = "l'appli";
        /// <summary>Part prise par l'appli sur chaque course (0 tant que Kevin n'a pas décidé).</summary>
        public float commission;
        /// <summary>Prix d'une course de l'appli : … fois le prix partagé de la distance.</summary>
        public float fareFactor = 1.5f;
        /// <summary>Commandes par heure de jeu (0 : aucune).</summary>
        public float ordersPerHour;
        /// <summary>Une commande reste proposée … s réelles avant de passer à un autre chauffeur.</summary>
        public float offerSeconds = 15f;
        /// <summary>Note du client selon l'attente à la prise en charge (minutes de jeu) : 5 étoiles sous …, puis une de moins par tranche.</summary>
        public int fiveStarsWithinMinutes = 10;
        public int minutesPerStarLost = 10;
    }

    [Serializable]
    public class RideTypesConfig
    {
        /// <summary>Dépôts et appli actifs dans le jeu (false : seulement la course partagée).</summary>
        public bool enabled;
        public DepotConfig depot = new DepotConfig();
        public AppConfig app = new AppConfig();
    }

    /// <summary>Règle des passagers selon le type de course (vision §4.1).</summary>
    public static class RideRules
    {
        /// <summary>
        /// Un client de la rue peut-il monter ? En course partagée oui ; pendant un dépôt ou une course de l'appli, non :
        /// seuls le client du dépôt (et son groupe) ou les passagers de la commande sont à bord.
        /// </summary>
        public static bool AcceptsStreetClients(RideKind current) => current == RideKind.Shared;

        /// <summary>On ne commence un dépôt ou une course de l'appli qu'avec le taxi vide.</summary>
        public static bool CanStartExclusive(int passengersOnBoard) => passengersOnBoard == 0;

        /// <summary>Le dépôt : offre du client pour la distance (prix partagé × places × facteur), arrondie.</summary>
        public static int DepotOffer(DepotConfig depot, FareConfig fares, int distanceMeters)
        {
            var v = fares.FareFor(Math.Max(0, distanceMeters)) * Math.Max(1, depot.seatsPaid) * Math.Max(0f, depot.offerPerSeatFactor);
            return Round(v, depot.roundTo);
        }

        /// <summary>
        /// Le chauffeur demande <paramref name="ask"/> au lieu de l'offre : chance que le client accepte (1 si on demande
        /// l'offre ou moins ; baisse avec ce qu'on demande en plus ; 0 au plus bas).
        /// </summary>
        public static float DepotAcceptChance(DepotConfig depot, int offer, int ask)
        {
            if (offer <= 0 || ask <= offer) return 1f;
            var extraTenths = (ask - offer) / (float)offer * 10f;
            return Math.Max(0f, 1f - extraTenths * depot.acceptDropPer10Percent);
        }

        /// <summary>Prix d'une course de l'appli et ce qu'il en reste au chauffeur après la commission.</summary>
        public static int AppFare(AppConfig app, FareConfig fares, int distanceMeters) =>
            Round(fares.FareFor(Math.Max(0, distanceMeters)) * Math.Max(0f, app.fareFactor), 100);

        public static int AfterCommission(AppConfig app, int fare) =>
            fare - (int)Math.Round(fare * Math.Max(0f, Math.Min(1f, app.commission)), MidpointRounding.AwayFromZero);

        /// <summary>Note du client de l'appli (1 à 5 étoiles) selon son attente en minutes de jeu.</summary>
        public static int AppRating(AppConfig app, int waitedMinutes)
        {
            if (waitedMinutes <= app.fiveStarsWithinMinutes) return 5;
            var lost = 1 + (waitedMinutes - app.fiveStarsWithinMinutes - 1) / Math.Max(1, app.minutesPerStarLost);
            return Math.Max(1, 5 - lost);
        }

        private static int Round(double v, int step)
        {
            step = Math.Max(1, step);
            return (int)(Math.Round(v / step, MidpointRounding.AwayFromZero) * step);
        }
    }

    /// <summary>Une commande de l'appli : prise en charge à un lieu, destination, nombre de passagers.</summary>
    public sealed class AppOrder
    {
        public int Id { get; internal set; }
        public string PickupStopId { get; internal set; } = "";
        public string DestinationStopId { get; internal set; } = "";
        public int Passengers { get; internal set; } = 1;
        public int Fare { get; internal set; }
        /// <summary>Proposée depuis … s réelles.</summary>
        public float OfferedSeconds { get; internal set; }
        public bool Accepted { get; internal set; }
        /// <summary>Minute de jeu de l'acceptation (pour la note).</summary>
        public int AcceptedAtMinute { get; internal set; }
    }

    /// <summary>
    /// Le téléphone du chauffeur (jalon 8) : des commandes arrivent au fil de la journée, restent proposées quelques
    /// secondes, le chauffeur en accepte une (taxi vide), va la chercher, la dépose, est noté et paie la commission.
    /// </summary>
    public sealed class AppDispatcher
    {
        private readonly AppConfig _config;
        private readonly StopsConfig _stops;
        private readonly FareConfig _fares;
        private readonly Random _random;
        private int _counter;

        /// <summary>Commande proposée en ce moment (null : rien).</summary>
        public AppOrder Offer { get; private set; }
        /// <summary>Commande acceptée en cours (null : aucune).</summary>
        public AppOrder Active { get; private set; }
        public List<int> Ratings { get; } = new List<int>();

        public AppDispatcher(AppConfig config, StopsConfig stops, FareConfig fares, Random random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _stops = stops ?? throw new ArgumentNullException(nameof(stops));
            _fares = fares ?? throw new ArgumentNullException(nameof(fares));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (stops.stops.Length < 2) throw new ArgumentException("Il faut au moins deux lieux.");
        }

        /// <summary><paramref name="dtReal"/> s réelles, valant <paramref name="gameMinutes"/> minutes de jeu, sont passées.</summary>
        public void Update(float dtReal, float gameMinutes)
        {
            if (Offer != null)
            {
                Offer.OfferedSeconds += dtReal;
                if (Offer.OfferedSeconds >= _config.offerSeconds) Offer = null; // passée à un autre chauffeur
            }
            if (Offer != null || Active != null || _config.ordersPerHour <= 0f) return;
            var chance = 1.0 - Math.Exp(-_config.ordersPerHour * gameMinutes / 60.0);
            if (_random.NextDouble() >= chance) return;
            var a = _stops.stops[_random.Next(_stops.stops.Length)];
            Stop b;
            do b = _stops.stops[_random.Next(_stops.stops.Length)]; while (b.id == a.id);
            Offer = new AppOrder
            {
                Id = ++_counter,
                PickupStopId = a.id,
                DestinationStopId = b.id,
                Passengers = 1 + _random.Next(3),
                Fare = RideRules.AppFare(_config, _fares, Math.Abs(b.distanceAlongRouteMeters - a.distanceAlongRouteMeters)),
            };
        }

        /// <summary>Accepte la commande proposée (le taxi doit être vide) ; false sinon.</summary>
        public bool Accept(int passengersOnBoard, int gameMinute)
        {
            if (Offer == null || Active != null || !RideRules.CanStartExclusive(passengersOnBoard)) return false;
            Active = Offer;
            Active.Accepted = true;
            Active.AcceptedAtMinute = gameMinute;
            Offer = null;
            return true;
        }

        /// <summary>Course déposée : rend ce qui revient au chauffeur (après commission) et enregistre la note.</summary>
        public int Complete(int pickupMinute)
        {
            if (Active == null) return 0;
            Ratings.Add(RideRules.AppRating(_config, Math.Max(0, pickupMinute - Active.AcceptedAtMinute)));
            var net = RideRules.AfterCommission(_config, Active.Fare);
            Active = null;
            return net;
        }

        /// <summary>Note moyenne du chauffeur sur l'appli (0 sans course).</summary>
        public float AverageRating
        {
            get
            {
                if (Ratings.Count == 0) return 0f;
                var t = 0;
                foreach (var r in Ratings) t += r;
                return t / (float)Ratings.Count;
            }
        }
    }
}
