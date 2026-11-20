using System;
using TaxiKmer.Core.Driving;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Pictogrammes des commandes tactiles dessinés par le code (aucune image importée) : flèches épaisses, pédales
    /// d'accélérateur et de frein, avertisseur, pause. Masques carrés comme <see cref="Shapes"/>, en coordonnées
    /// normalisées (0 à 1, origine en bas à gauche).
    /// </summary>
    public static class Pictograms
    {
        /// <summary>Pictogramme d'une commande, ou null si elle n'en a pas (bouton contextuel, qui garde son texte).</summary>
        public static byte[] For(DriveControl control, int size)
        {
            switch (control)
            {
                case DriveControl.SteerLeft: return Draw(size, (x, y) => Arrow(1f - x, y));
                case DriveControl.SteerRight: return Draw(size, Arrow);
                case DriveControl.Accelerate: return Draw(size, Accelerator);
                case DriveControl.Brake: return Draw(size, BrakePedal);
                case DriveControl.Horn: return Draw(size, Horn);
                case DriveControl.Pause: return Draw(size, PauseBars);
                default: return null;
            }
        }

        /// <summary>Distance signée normalisée (positive dedans) → masque, bord lissé sur un pixel.</summary>
        private static byte[] Draw(int size, Func<float, float, float> sdf)
            => Shapes.Draw(size, (px, py) => sdf(px / size, py / size) * size);

        // ---------- Formes ----------

        /// <summary>Flèche épaisse vers la droite : tige et pointe triangulaire.</summary>
        private static float Arrow(float x, float y)
        {
            var shaft = Box(x, y, 0.12f, 0.40f, 0.56f, 0.60f, 0.03f);
            var head = Triangle(x, y, 0.50f, 0.18f, 0.90f, 0.50f, 0.50f, 0.82f);
            return Math.Max(shaft, head);
        }

        /// <summary>Pédale d'accélérateur : haute et étroite, trois rainures.</summary>
        private static float Accelerator(float x, float y)
        {
            var pedal = Box(x, y, 0.33f, 0.10f, 0.67f, 0.90f, 0.08f);
            return Grooves(pedal, x, y, 0.40f, 0.60f, new[] { 0.32f, 0.50f, 0.68f });
        }

        /// <summary>Pédale de frein : large et basse, deux rainures.</summary>
        private static float BrakePedal(float x, float y)
        {
            var pedal = Box(x, y, 0.14f, 0.28f, 0.86f, 0.72f, 0.1f);
            return Grooves(pedal, x, y, 0.24f, 0.76f, new[] { 0.43f, 0.57f });
        }

        /// <summary>Avertisseur : pavillon qui s'évase vers la droite et deux ondes sonores.</summary>
        private static float Horn(float x, float y)
        {
            var back = Box(x, y, 0.08f, 0.40f, 0.24f, 0.60f, 0.02f);
            var cone = ConvexQuad(x, y, 0.22f, 0.42f, 0.56f, 0.20f, 0.56f, 0.80f, 0.22f, 0.58f);
            var shape = Math.Max(back, cone);
            foreach (var r in new[] { 0.16f, 0.28f })
            {
                // Arc d'anneau centré sur la bouche du pavillon, limité à ±40°.
                float cx = 0.56f, cy = 0.5f;
                var d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                var ring = 0.03f - Math.Abs(d - r);
                var angle = (float)Math.Atan2(Math.Abs(y - cy), x - cx);
                var inAngle = (0.70f - angle) * d;
                shape = Math.Max(shape, Math.Min(ring, inAngle));
            }
            return shape;
        }

        private static float PauseBars(float x, float y)
            => Math.Max(Box(x, y, 0.26f, 0.18f, 0.42f, 0.82f, 0.04f), Box(x, y, 0.58f, 0.18f, 0.74f, 0.82f, 0.04f));

        // ---------- Primitives (distance signée, positive dedans) ----------

        private static float Box(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
            float hx = (x1 - x0) / 2f - r, hy = (y1 - y0) / 2f - r;
            var qx = Math.Abs(x - cx) - hx;
            var qy = Math.Abs(y - cy) - hy;
            var ox = Math.Max(qx, 0f);
            var oy = Math.Max(qy, 0f);
            return r - ((float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f));
        }

        private static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
            => Math.Min(Edge(x, y, ax, ay, bx, by), Math.Min(Edge(x, y, bx, by, cx, cy), Edge(x, y, cx, cy, ax, ay)));

        private static float ConvexQuad(float x, float y, float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
            => Math.Min(Math.Min(Edge(x, y, ax, ay, bx, by), Edge(x, y, bx, by, cx, cy)),
                        Math.Min(Edge(x, y, cx, cy, dx, dy), Edge(x, y, dx, dy, ax, ay)));

        /// <summary>Distance au côté AB d'un polygone parcouru dans le sens trigonométrique (positive à gauche, dedans).</summary>
        private static float Edge(float x, float y, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax, ey = by - ay;
            var len = (float)Math.Sqrt(ex * ex + ey * ey);
            return (ex * (y - ay) - ey * (x - ax)) / len;
        }

        /// <summary>Creuse des rainures horizontales (épaisseur 0,05) entre x0 et x1.</summary>
        private static float Grooves(float shape, float x, float y, float x0, float x1, float[] ys)
        {
            foreach (var gy in ys) shape = Math.Min(shape, -Box(x, y, x0, gy - 0.025f, x1, gy + 0.025f, 0.02f));
            return shape;
        }
    }
}
