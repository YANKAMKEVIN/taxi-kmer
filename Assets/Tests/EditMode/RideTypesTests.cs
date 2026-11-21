using System;
using NUnit.Framework;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class RideTypesTests
    {
        private static StopsConfig Stops() => new StopsConfig
        {
            stops = new[]
            {
                new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                new Stop { id = "b", name = "B", distanceAlongRouteMeters = 1500 },
                new Stop { id = "c", name = "C", distanceAlongRouteMeters = 5000 },
            },
        };

        [Test]
        public void PassengerRules_ByRideKind()
        {
            Assert.IsTrue(RideRules.AcceptsStreetClients(RideKind.Shared));
            Assert.IsFalse(RideRules.AcceptsStreetClients(RideKind.Depot), "Dépôt : pas d'autre passager.");
            Assert.IsFalse(RideRules.AcceptsStreetClients(RideKind.App), "Appli : seulement les passagers de la commande.");
            Assert.IsTrue(RideRules.CanStartExclusive(0));
            Assert.IsFalse(RideRules.CanStartExclusive(2));
        }

        [Test]
        public void Depot_DearerThanShared_AskingMoreLowersTheChance()
        {
            var depot = new DepotConfig();
            var fares = FareConfig.Default();
            var offer = RideRules.DepotOffer(depot, fares, 3000);
            Assert.AreEqual(fares.FareFor(3000) * 5, offer, "Le taxi entier : 5 places.");
            Assert.Greater(offer, fares.FareFor(3000), "Plus cher qu'une course partagée.");
            Assert.AreEqual(1f, RideRules.DepotAcceptChance(depot, offer, offer));
            Assert.AreEqual(0.75f, RideRules.DepotAcceptChance(depot, 2000, 2200), 1e-4f);
            Assert.AreEqual(0f, RideRules.DepotAcceptChance(depot, 2000, 4000));
        }

        [Test]
        public void App_FareCommissionRating()
        {
            var app = new AppConfig { commission = 0.2f };
            var fares = FareConfig.Default();
            Assert.AreEqual(fares.FareFor(3000) * 3 / 2, RideRules.AppFare(app, fares, 3000));
            Assert.AreEqual(800, RideRules.AfterCommission(app, 1000));
            Assert.AreEqual(1000, RideRules.AfterCommission(new AppConfig(), 1000), "Commission 0 tant que Kevin n'a pas décidé.");
            Assert.AreEqual(5, RideRules.AppRating(app, 10));
            Assert.AreEqual(4, RideRules.AppRating(app, 11));
            Assert.AreEqual(4, RideRules.AppRating(app, 20));
            Assert.AreEqual(3, RideRules.AppRating(app, 21));
            Assert.AreEqual(1, RideRules.AppRating(app, 500));
        }

        [Test]
        public void Dispatcher_NoOrdersByDefault()
        {
            var d = new AppDispatcher(new AppConfig(), Stops(), FareConfig.Default(), new Random(1));
            for (var i = 0; i < 1000; i++) d.Update(1f, 1.4f);
            Assert.IsNull(d.Offer);
        }

        [Test]
        public void Dispatcher_OfferExpires_AcceptNeedsEmptyTaxi_CompletePays()
        {
            var app = new AppConfig { ordersPerHour = 60f, commission = 0.1f };
            var d = new AppDispatcher(app, Stops(), FareConfig.Default(), new Random(2));
            for (var i = 0; i < 100 && d.Offer == null; i++) d.Update(0.1f, 1f);
            Assert.IsNotNull(d.Offer);
            Assert.AreNotEqual(d.Offer.PickupStopId, d.Offer.DestinationStopId);
            Assert.That(d.Offer.Passengers, Is.InRange(1, 3));
            var first = d.Offer.Id;
            d.Update(app.offerSeconds, 0f);
            Assert.IsTrue(d.Offer == null || d.Offer.Id != first, "Pas acceptée à temps : passée à un autre chauffeur.");

            for (var i = 0; i < 100 && d.Offer == null; i++) d.Update(0.1f, 1f);
            Assert.IsFalse(d.Accept(2, 400), "Taxi pas vide : pas de commande.");
            var fare = d.Offer.Fare;
            Assert.IsTrue(d.Accept(0, 400));
            Assert.IsNull(d.Offer);
            for (var i = 0; i < 100; i++) d.Update(0.1f, 1f);
            Assert.IsNull(d.Offer, "Une commande à la fois.");
            Assert.AreEqual(fare - (int)Math.Round(fare * 0.1), d.Complete(415));
            Assert.AreEqual(4f, d.AverageRating, "15 min d'attente : 4 étoiles.");
        }
    }
}
