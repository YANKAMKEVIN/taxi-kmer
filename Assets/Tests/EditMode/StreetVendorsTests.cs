using System;
using System.Collections.Generic;
using NUnit.Framework;
using TaxiKmer.Core.Traffic;

namespace TaxiKmer.Core.Tests
{
    public class StreetVendorsTests
    {
        private static readonly List<(int id, float along)> Lights = new List<(int, float)> { (1, 631f), (2, 3545f), (4, 2932f) };

        private static VendorEvent Wait(StreetVendors v, float seconds, float along, int direction, int passengers = 1)
        {
            var last = new VendorEvent();
            for (var t = 0f; t < seconds; t += 0.1f)
            {
                var e = v.Update(0.1f, along, direction, 0f, Lights, passengers);
                if (e.Arrived) return e;
                last = e;
            }
            return last;
        }

        [Test]
        public void Vendor_ComesToTheTaxiWaitingBeforeAVendorLight()
        {
            var v = new StreetVendors(new VendorsConfig { buyChance = 1f }, new Random(1));
            var e = Wait(v, 5f, 610f, 1);
            Assert.IsTrue(e.Arrived);
            Assert.AreEqual(1, e.LightId);
            Assert.IsTrue(e.Bought);
            Assert.IsNotNull(e.Goods);
            StringAssert.StartsWith("Un vendeur propose", StreetVendors.Message(e));
            // Le taxi repart : le vendeur s'en va.
            Assert.IsTrue(v.Update(0.1f, 612f, 1, 20f, Lights, 1).Left);
            Assert.AreEqual(-1, v.VendorAt);
        }

        [Test]
        public void NoVendor_WhenMoving_PastTheLight_AtOtherLights_OrTooSoon()
        {
            var cfg = new VendorsConfig();
            var v = new StreetVendors(cfg, new Random(1));
            for (var i = 0; i < 50; i++) Assert.IsFalse(v.Update(0.1f, 610f, 1, 30f, Lights, 1).Arrived, "en roulant");
            Assert.IsFalse(Wait(v, 5f, 650f, 1).Arrived, "le feu est derrière");
            Assert.IsFalse(Wait(v, 5f, 3530f, 1).Arrived, "pas de vendeur à ce feu");
            Assert.IsTrue(Wait(v, 5f, 650f, -1).Arrived, "vers Messassi, le feu est devant");
            v.Update(0.1f, 650f, -1, 30f, Lights, 1);
            Assert.IsFalse(Wait(v, 5f, 650f, -1).Arrived, "pas deux fois de suite au même feu");
        }

        [Test]
        public void EmptyTaxi_NobodyBuys()
        {
            var v = new StreetVendors(new VendorsConfig { buyChance = 1f }, new Random(1));
            var e = Wait(v, 5f, 2910f, 1, passengers: 0);
            Assert.IsTrue(e.Arrived);
            Assert.IsFalse(e.Bought);
        }
    }
}
