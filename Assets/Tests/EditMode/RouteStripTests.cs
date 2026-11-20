using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class RouteStripTests
    {
        [Test]
        public void Fraction_ClampsToTheStrip()
        {
            Assert.AreEqual(0f, RouteStrip.Fraction(-20f, 6000f));
            Assert.AreEqual(0.5f, RouteStrip.Fraction(3000f, 6000f), 1e-6f);
            Assert.AreEqual(1f, RouteStrip.Fraction(7000f, 6000f));
            Assert.AreEqual(0f, RouteStrip.Fraction(100f, 0f), "Itinéraire vide.");
        }

        [Test]
        public void Labels_SpreadOverTwoRows_WithoutOverlap()
        {
            // Les six lieux du jalon 1 sur une frise de 1 000 px.
            var along = new[] { 0f, 631f, 1567f, 2154f, 2948f, 6132f };
            var widths = new[] { 120f, 100f, 130f, 60f, 155f, 70f };
            var centers = new float[along.Length];
            for (var i = 0; i < along.Length; i++) centers[i] = RouteStrip.Fraction(along[i], 6132f) * 1000f;

            var placed = RouteStrip.LayoutLabels(centers, widths, 1000f, 2, 6f);
            Assert.AreEqual(0f, placed[0].Left, "Premier nom calé à gauche.");
            Assert.AreEqual(1000f - 70f, placed[5].Left, 1e-3f, "Dernier nom calé à droite.");
            Assert.AreEqual(0, placed[0].Row);
            Assert.AreEqual(1, placed[1].Row, "« Carrefour » chevauche « Dispensaire » : deuxième rangée.");
            for (var i = 0; i < placed.Length; i++)
            for (var j = i + 1; j < placed.Length; j++)
            {
                if (placed[i].Row != placed[j].Row) continue;
                var overlap = placed[i].Left < placed[j].Left + widths[j] && placed[j].Left < placed[i].Left + widths[i];
                Assert.IsFalse(overlap, $"Noms {i} et {j} sur la même rangée.");
            }
        }

        [Test]
        public void Labels_StayInsideTheStrip()
        {
            var placed = RouteStrip.LayoutLabels(new[] { 5f, 995f }, new[] { 200f, 200f }, 1000f);
            Assert.GreaterOrEqual(placed[0].Left, 0f);
            Assert.LessOrEqual(placed[1].Left + 200f, 1000f);
        }

        [Test]
        public void StopNames_ShortAndStrip()
        {
            Assert.AreEqual("Étoudi", StopNames.Short("Étoudi (devant le palais de la présidence)"));
            Assert.AreEqual("Carrefour", StopNames.Short("Carrefour / marché de Messassi"));
            Assert.AreEqual("Dispensaire de Messassi", StopNames.Short("Dispensaire de Messassi"));
            Assert.AreEqual("Dispensaire", StopNames.Strip("Dispensaire de Messassi"));
            Assert.AreEqual("Pont d'Emana", StopNames.Strip("Pont d'Emana"));
            Assert.AreEqual("", StopNames.Short(null));
        }
    }
}
