using System;
using System.Collections.Generic;
using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class AmbienceTests
    {
        private static AmbienceMix Mix(AmbienceConfig c = null, int seed = 1) => new AmbienceMix(c ?? new AmbienceConfig(), new[] { 631f }, new Random(seed));

        [Test]
        public void Market_LouderNearIt_AndOnMarketDays_SilentFarAway()
        {
            var m = Mix();
            Assert.AreEqual(1f, m.MarketCloseness(631f), 1e-5f);
            Assert.AreEqual(0.5f, m.MarketCloseness(756f), 1e-5f);
            Assert.AreEqual(0f, m.MarketCloseness(2000f));
            m.Update(0.1f, 631f, 0, 0, null, false, false);
            var ordinary = m.MarketVolume;
            m.Update(0.1f, 631f, 0, 0, null, true, false);
            Assert.Greater(m.MarketVolume, ordinary, "Jour de marché : plus fort.");
            m.Update(0.1f, 3000f, 0, 0, null, true, false);
            Assert.AreEqual(0f, m.MarketVolume);
            Assert.Greater(m.MurmurVolume, 0f, "La rumeur de la ville reste partout.");
        }

        [Test]
        public void Talking_LowersAmbience_PauseSilencesIt_DisabledDoesNothing()
        {
            var m = Mix();
            m.Update(0.1f, 631f, 0, 0, null, true, false);
            var full = m.MarketVolume;
            m.Update(0.1f, 631f, 0, 0, null, true, true);
            Assert.AreEqual(full * 0.5f, m.MarketVolume, 1e-5f);
            var e = m.Update(0.1f, 631f, 50, 50, null, true, false, audible: false);
            Assert.AreEqual(0f, m.MarketVolume);
            Assert.AreEqual(0f, m.MurmurVolume);
            Assert.IsFalse(e.Horn);
            var off = Mix(new AmbienceConfig { enabled = false });
            Assert.IsFalse(off.Update(10f, 631f, 50, 50, new[] { new KeyValuePair<string, float>("c1", 631f) }, true, false).Horn);
        }

        [Test]
        public void Horns_FollowNearbyTraffic_MoreInJams_Capped()
        {
            var m = Mix();
            Assert.AreEqual(0f, m.HornsPerMinute(0, 0));
            Assert.AreEqual(0.4f * 5, m.HornsPerMinute(5, 0), 1e-5f);
            Assert.AreEqual(0.4f * 3 * 5, m.HornsPerMinute(0, 5), 1e-5f, "Embouteillage : trois fois plus.");
            Assert.AreEqual(10f, m.HornsPerMinute(100, 100), "Plafonné.");

            // Mesure : 10 minutes avec 5 voitures qui roulent → ≈ 20 klaxons.
            var horns = 0;
            for (var t = 0f; t < 600f; t += 0.05f) if (m.Update(0.05f, 3000f, 5, 0, null, false, false).Horn) horns++;
            Assert.That(horns, Is.InRange(10, 32));
            var quiet = 0;
            for (var t = 0f; t < 600f; t += 0.05f) if (m.Update(0.05f, 3000f, 0, 0, null, false, false).Horn) quiet++;
            Assert.AreEqual(0, quiet, "Route vide : pas de klaxon.");
        }

        [Test]
        public void ClientCalls_OncePerClient_WhenTheTaxiPasses()
        {
            var m = Mix();
            var waiting = new[] { new KeyValuePair<string, float>("c1", 1000f), new KeyValuePair<string, float>("c2", 2000f) };
            Assert.IsEmpty(m.Update(0.1f, 800f, 0, 0, waiting, false, false).Calls, "Trop loin.");
            CollectionAssert.AreEqual(new[] { "c1" }, m.Update(0.1f, 950f, 0, 0, waiting, false, false).Calls);
            Assert.IsEmpty(m.Update(0.1f, 990f, 0, 0, waiting, false, false).Calls, "Une seule fois.");
            CollectionAssert.AreEqual(new[] { "c2" }, m.Update(0.1f, 2040f, 0, 0, waiting, false, false).Calls);
        }
    }
}
