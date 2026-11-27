using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class CarBodyBuilderTests
    {
        [Test]
        public void Sedan_FitsTheTaxi_AndHasEveryPart()
        {
            var set = CarBodyBuilder.Sedan();
            var all = set.Slots.SelectMany(kv => kv.Value.Vertices).ToList();
            Assert.AreEqual(4.4f, all.Max(v => v.Z) - all.Min(v => v.Z), 0.2f, "Longueur d'une Corolla");
            Assert.AreEqual(1.42f, all.Max(v => v.Y), 0.1f, "Hauteur du toit");
            Assert.LessOrEqual(all.Max(v => v.X), 1.72f / 2f + 0.17f, "Largeur (rétroviseurs compris)");
            foreach (var slot in new[] { CarBodyBuilder.Paint, CarBodyBuilder.Glass, CarBodyBuilder.Trim, CarBodyBuilder.Headlight, CarBodyBuilder.Taillight })
                Assert.Greater(set.Get(slot).TriangleCount, 0, $"Partie {slot}");
            Assert.AreEqual(0, set.Get(CarBodyBuilder.Tyre).TriangleCount, "Taxi du joueur : roues à part (elles tournent)");
            Assert.Greater(CarBodyBuilder.Sedan(wheels: true).Get(CarBodyBuilder.Tyre).TriangleCount, 0, "Trafic : roues comprises");
            var heads = set.Get(CarBodyBuilder.Headlight).Vertices;
            Assert.IsTrue(heads.All(v => v.Z > 2f), "Phares à l'avant (+z)");
        }
    }
}
