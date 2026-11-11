using System;

namespace TaxiKmer.Core.Driving
{
    public enum DriveControl
    {
        None,
        SteerLeft,
        SteerRight,
        Accelerate,
        Brake,
        Horn,
    }

    public enum ScreenCorner
    {
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight,
    }

    /// <summary>
    /// Zone tactile ancrée à un coin de l'écran. Marges et tailles en fractions de la <b>hauteur</b> d'écran,
    /// pour que les boutons gardent leur forme quel que soit le format du téléphone (16:9, 19,5:9…).
    /// </summary>
    [Serializable]
    public class ControlZone
    {
        public DriveControl control;
        public ScreenCorner corner;
        /// <summary>Distance au bord vertical le plus proche (gauche ou droit).</summary>
        public float side;
        /// <summary>Distance au bord horizontal le plus proche (haut ou bas).</summary>
        public float edge;
        public float width;
        public float height;
        public string label = "";

        /// <summary>Rectangle en pixels, origine en bas à gauche (convention des écrans Unity).</summary>
        public PixelRect Resolve(float screenWidth, float screenHeight)
        {
            var u = screenHeight;
            var w = width * u;
            var h = height * u;
            var left = corner == ScreenCorner.BottomLeft || corner == ScreenCorner.TopLeft;
            var bottom = corner == ScreenCorner.BottomLeft || corner == ScreenCorner.BottomRight;
            var x = left ? side * u : screenWidth - side * u - w;
            var y = bottom ? edge * u : screenHeight - edge * u - h;
            return new PixelRect(x, y, w, h);
        }
    }

    public struct PixelRect
    {
        public float X, Y, Width, Height;

        public PixelRect(float x, float y, float width, float height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public bool Contains(float px, float py) => px >= X && px <= X + Width && py >= Y && py <= Y + Height;
    }

    /// <summary>
    /// Disposition des commandes tactiles (décision de Kevin, jalon 1) : à gauche les flèches ← →,
    /// à droite la grande pédale d'accélérateur et la pédale de frein / marche arrière, en haut à droite le klaxon.
    /// </summary>
    [Serializable]
    public class ControlLayout
    {
        public ControlZone[] zones = Array.Empty<ControlZone>();

        /// <summary>Commande sous le point (pixels, origine en bas à gauche), ou None (glisser = tourner la caméra).</summary>
        public DriveControl ControlAt(float px, float py, float screenWidth, float screenHeight)
        {
            foreach (var z in zones)
                if (z.Resolve(screenWidth, screenHeight).Contains(px, py)) return z.control;
            return DriveControl.None;
        }

        public static ControlLayout Default()
        {
            return new ControlLayout
            {
                zones = new[]
                {
                    new ControlZone { control = DriveControl.SteerLeft, corner = ScreenCorner.BottomLeft, side = 0.04f, edge = 0.05f, width = 0.27f, height = 0.32f, label = "◄" },
                    new ControlZone { control = DriveControl.SteerRight, corner = ScreenCorner.BottomLeft, side = 0.35f, edge = 0.05f, width = 0.27f, height = 0.32f, label = "►" },
                    new ControlZone { control = DriveControl.Accelerate, corner = ScreenCorner.BottomRight, side = 0.04f, edge = 0.05f, width = 0.22f, height = 0.48f, label = "Accélérer" },
                    new ControlZone { control = DriveControl.Brake, corner = ScreenCorner.BottomRight, side = 0.30f, edge = 0.05f, width = 0.22f, height = 0.28f, label = "Frein\nArrière" },
                    new ControlZone { control = DriveControl.Horn, corner = ScreenCorner.TopRight, side = 0.04f, edge = 0.05f, width = 0.24f, height = 0.24f, label = "Klaxon" },
                },
            };
        }
    }
}
