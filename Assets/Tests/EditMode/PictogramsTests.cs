using NUnit.Framework;
using TaxiKmer.Core.Driving;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class PictogramsTests
    {
        private const int N = 64;

        private static float Coverage(byte[] a)
        {
            var sum = 0f;
            foreach (var v in a) sum += v / 255f;
            return sum / a.Length;
        }

        private static float CoverageOfHalf(byte[] a, bool right)
        {
            var sum = 0f;
            for (var y = 0; y < N; y++)
            for (var x = right ? N / 2 : 0; x < (right ? N : N / 2); x++) sum += a[y * N + x] / 255f;
            return sum / (a.Length / 2f);
        }

        [Test]
        public void EveryDrivingControl_HasAVisiblePictogram_ExceptTheContextButton()
        {
            foreach (var c in new[] { DriveControl.SteerLeft, DriveControl.SteerRight, DriveControl.Accelerate, DriveControl.Brake, DriveControl.Horn, DriveControl.Pause })
            {
                var a = Pictograms.For(c, N);
                Assert.AreEqual(N * N, a.Length, c.ToString());
                var cov = Coverage(a);
                Assert.Greater(cov, 0.08f, $"{c} visible");
                Assert.Less(cov, 0.6f, $"{c} n'est pas un pavé plein");
                Assert.AreEqual(0, a[0], $"{c} : coin transparent");
            }
            Assert.IsNull(Pictograms.For(DriveControl.Action, N));
        }

        [Test]
        public void Arrows_AreMirrorImages_AndPointTheRightWay()
        {
            var left = Pictograms.For(DriveControl.SteerLeft, N);
            var right = Pictograms.For(DriveControl.SteerRight, N);
            for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
                Assert.AreEqual(right[y * N + x], left[y * N + (N - 1 - x)], 1, "Miroir gauche-droite.");
            // La pointe (pleine sur toute sa hauteur) pèse plus que la tige.
            Assert.Greater(CoverageOfHalf(right, true), CoverageOfHalf(right, false));
        }

        [Test]
        public void Pedals_AcceleratorIsTall_BrakeIsWide()
        {
            var acc = Pictograms.For(DriveControl.Accelerate, N);
            var brake = Pictograms.For(DriveControl.Brake, N);
            Assert.AreEqual(0, acc[(N / 2) * N + 4], "Accélérateur étroit : bord gauche vide à mi-hauteur.");
            Assert.AreEqual(255, acc[(N - 12) * N + N / 2], "Accélérateur haut : plein près du haut.");
            Assert.AreEqual(0, brake[(N - 8) * N + N / 2], "Frein bas : vide près du haut.");
            Assert.AreEqual(255, brake[(N * 30 / 100) * N + N / 4], "Frein large : plein loin du centre.");
        }
    }
}
