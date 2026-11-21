using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class CalendarTests
    {
        [Test]
        public void Days_FollowTheWeek_StartingMonday()
        {
            var cal = new Calendar(new CalendarConfig());
            Assert.AreEqual("lundi", cal.For(1).Name);
            Assert.AreEqual("mercredi", cal.For(3).Name);
            Assert.AreEqual("dimanche", cal.For(7).Name);
            Assert.AreEqual("lundi", cal.For(8).Name, "La semaine recommence.");
            Assert.AreEqual(6, cal.For(14).Weekday);
        }

        [Test]
        public void FirstWeekday_ShiftsTheWeek()
        {
            var cal = new Calendar(new CalendarConfig { firstWeekday = 5 });
            Assert.AreEqual("samedi", cal.For(1).Name);
            Assert.AreEqual("dimanche", cal.For(2).Name);
            Assert.AreEqual("lundi", cal.For(3).Name);
        }

        [Test]
        public void Sunday_IsCalmer_MarketDays_Busier()
        {
            var cal = new Calendar(new CalendarConfig());
            var sunday = cal.For(7);
            Assert.IsTrue(sunday.Calm);
            Assert.Less(sunday.ClientsFactor, 1f);
            Assert.Less(sunday.TrafficFactor, 1f);
            var markets = Enumerable.Range(1, 7).Select(cal.For).Where(d => d.Market).Select(d => d.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "mercredi", "samedi" }, markets);
            Assert.Greater(cal.For(3).ClientsFactor, 1f);
            Assert.AreEqual(1f, cal.For(1).ClientsFactor);
        }

        [Test]
        public void Labels()
        {
            var cal = new Calendar(new CalendarConfig());
            Assert.AreEqual("Jour 3 · mer.", Calendar.HudLabel(cal.For(3)));
            Assert.AreEqual("Jour 2 · mar.", Calendar.HudLabel(cal.For(2)));
            StringAssert.StartsWith("Mercredi, jour du marché de Messassi", cal.Announcement(cal.For(3)));
            StringAssert.StartsWith("Dimanche : la ville est plus calme", cal.Announcement(cal.For(7)));
            Assert.AreEqual("", cal.Announcement(cal.For(1)), "Rien à annoncer un jour ordinaire.");
        }

        [Test]
        public void SpawnRateScale_SlowsOrSpeedsUpClientArrivals()
        {
            var cfg = new StreetConfig();
            var stops = new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                    new Stop { id = "b", name = "B", distanceAlongRouteMeters = 1000 },
                },
            };
            var street = new Street(cfg, stops, new ClientFactory(stops, FareConfig.Default(), 3),
                new Shift(new DayConfig(), new SeatingConfig()), 1000f) { SpawnRateScale = 0.5f };
            var far = new TaxiOnRoute { Along = 500f, Direction = 1, Lateral = 100f };
            street.TakeByOther(street.Waiting.First(c => c.Pickup.id == "a"));
            street.Update(cfg.spawnIntervalSeconds * 1.5f, far);
            Assert.AreEqual(cfg.maxWaitingPerStop - 1, street.Waiting.Count(c => c.Pickup.id == "a"), "Deux fois moins vite : pas encore là.");
            street.Update(cfg.spawnIntervalSeconds * 0.6f, far);
            Assert.AreEqual(cfg.maxWaitingPerStop, street.Waiting.Count(c => c.Pickup.id == "a"));
        }
    }
}
