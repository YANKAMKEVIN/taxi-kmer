using System;

namespace TaxiKmer.Core.Quality
{
    /// <summary>
    /// Compteur d'images par seconde : moyenne sur une fenêtre (images comptées / temps écoulé), mise à jour à la fin
    /// de chaque fenêtre pour rester lisible. Sert à vérifier sur téléphone le critère de la tâche 1.7
    /// (30 images par seconde en Moyen, 60 en Haut sur le S24).
    /// </summary>
    public sealed class FrameRateMeter
    {
        private readonly float _window;
        private float _elapsed;
        private int _frames;

        /// <summary>Dernière moyenne mesurée (0 tant que la première fenêtre n'est pas finie).</summary>
        public float FramesPerSecond { get; private set; }

        public FrameRateMeter(float windowSeconds = 1f)
        {
            _window = Math.Max(0.05f, windowSeconds);
        }

        /// <summary>À appeler une fois par image avec la durée réelle de l'image (non mise à l'échelle).</summary>
        public void Tick(float unscaledDeltaSeconds)
        {
            if (unscaledDeltaSeconds <= 0f) return;
            _elapsed += unscaledDeltaSeconds;
            _frames++;
            if (_elapsed < _window - 1e-4f) return; // tolérance : 60 × (1/60) s fait 0,99999 s en float
            FramesPerSecond = _frames / _elapsed;
            _elapsed = 0f;
            _frames = 0;
        }

        /// <summary>Repart de zéro (ex. après un changement de niveau, pour ne pas mélanger deux mesures).</summary>
        public void Reset()
        {
            _elapsed = 0f;
            _frames = 0;
            FramesPerSecond = 0f;
        }
    }
}
