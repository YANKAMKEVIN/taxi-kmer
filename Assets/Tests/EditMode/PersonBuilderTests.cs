using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class PersonBuilderTests
    {
        [Test]
        public void Body_HumanSized_WiderWhenCostaud_DressReachesTheCalves()
        {
            float Width(int c) => PersonBuilder.Body(c, false).Get(PersonBuilder.Top).Vertices.Max(v => v.X) * 2f;
            Assert.Less(Width(0), Width(1));
            Assert.Less(Width(1), Width(2), "Costaud plus large (corpulence des clients)");
            var body = PersonBuilder.Body(1, false);
            var top = body.Slots.SelectMany(kv => kv.Value.Vertices).Max(v => v.Y);
            Assert.That(top, Is.InRange(1.6f, 1.9f), "Taille d'un adulte avec les jambes");
            var dress = PersonBuilder.Body(1, true).Get(PersonBuilder.Bottom).Vertices.Min(v => v.Y);
            Assert.Less(dress, 0.45f, "Pagne jusqu'aux mollets");
            var leg = PersonBuilder.Leg(1, false).Slots.SelectMany(kv => kv.Value.Vertices).Min(v => v.Y);
            Assert.AreEqual(-PersonBuilder.HipHeight, leg, 0.01f, "Jambe de la hanche jusqu'au sol");
        }

        [Test]
        public void Swing_OppositeLegs_StillWhenStanding()
        {
            Assert.AreEqual(0f, PersonBuilder.Swing(1.2f, 0f, true), 1e-5f);
            var l = PersonBuilder.Swing(1.2f, 1.3f, true);
            Assert.AreEqual(-l, PersonBuilder.Swing(1.2f, 1.3f, false), 1e-5f);
            Assert.LessOrEqual(System.Math.Abs(PersonBuilder.Swing(1.57f, 9f, true)), 28f, "Amplitude bornée");
        }
    }
}
