using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class ShapesTests
    {
        private static byte At(byte[] a, int size, int x, int y) => a[y * size + x];

        [Test]
        public void RoundedRect_CornersTransparent_CenterAndEdgesOpaque()
        {
            const int n = 64;
            var a = Shapes.RoundedRect(n, 20f);
            Assert.AreEqual(n * n, a.Length);
            Assert.AreEqual(0, At(a, n, 0, 0), "Coin arrondi.");
            Assert.AreEqual(0, At(a, n, n - 1, n - 1));
            Assert.AreEqual(255, At(a, n, n / 2, n / 2));
            Assert.AreEqual(255, At(a, n, n / 2, 1), "Bord droit (hors coins) plein.");
        }

        [Test]
        public void Ring_HollowCenter_OpaqueBand()
        {
            const int n = 64;
            var a = Shapes.Ring(n, 6f);
            Assert.AreEqual(0, At(a, n, n / 2, n / 2), "Centre vide.");
            Assert.AreEqual(255, At(a, n, n / 2, 3), "Bande opaque près du bord.");
            Assert.AreEqual(0, At(a, n, 0, 0));
        }

        [Test]
        public void Disc_RoundAndSymmetric()
        {
            const int n = 32;
            var a = Shapes.Disc(n);
            Assert.AreEqual(255, At(a, n, n / 2, n / 2));
            Assert.AreEqual(0, At(a, n, 0, 0));
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                Assert.AreEqual(At(a, n, x, y), At(a, n, n - 1 - x, y), "Symétrie gauche-droite.");
        }
    }
}
