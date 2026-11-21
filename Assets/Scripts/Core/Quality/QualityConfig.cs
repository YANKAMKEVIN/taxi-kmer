using System;

namespace TaxiKmer.Core.Quality
{
    /// <summary>
    /// Un niveau de qualité graphique côté jeu (tâche 1.7). Les ombres, la résolution de rendu, le HDR et l'anticrénelage
    /// viennent de l'asset URP du même rang (Assets/Settings/Rendering/URP-{Bas,Moyen,Haut}.asset, voir ProjectSetup) ;
    /// ce fichier règle ce que l'asset URP ne porte pas : distance d'affichage, brouillard, images par seconde visées.
    /// </summary>
    [Serializable]
    public class QualityLevel
    {
        /// <summary>Nom affiché, identique au nom du niveau dans les Quality Settings d'Unity.</summary>
        public string name = "Moyen";
        /// <summary>Images par seconde visées (Application.targetFrameRate).</summary>
        public int targetFrameRate = 30;
        /// <summary>Distance d'affichage de la caméra, en mètres (au-delà, rien n'est dessiné).</summary>
        public float drawDistance = 450f;
        /// <summary>Début du brouillard linéaire, en mètres.</summary>
        public float fogStart = 150f;
        /// <summary>Fin du brouillard (tout est couleur brouillard), en mètres ; au plus la distance d'affichage.</summary>
        public float fogEnd = 450f;
        /// <summary>Part du trafic et des piétons affichés (1 = densité des données ; moins au réglage bas).</summary>
        public float trafficScale = 1f;
    }

    /// <summary>Réglages de qualité (données : Assets/Resources/Data/quality.json).</summary>
    [Serializable]
    public class QualityConfig
    {
        /// <summary>Niveau choisi au premier lancement (0 = Bas, 1 = Moyen, 2 = Haut).</summary>
        public int defaultLevel = 1;
        /// <summary>Afficher le compteur d'images par seconde à côté du bouton (pour les essais sur téléphone).</summary>
        public bool showFrameRate = true;
        /// <summary>Durée sur laquelle le compteur fait sa moyenne, en secondes.</summary>
        public float frameRateWindowSeconds = 1f;
        public QualityLevel[] levels = DefaultLevels();

        public static QualityLevel[] DefaultLevels()
        {
            return new[]
            {
                new QualityLevel { name = "Bas", targetFrameRate = 30, drawDistance = 300f, fogStart = 100f, fogEnd = 300f, trafficScale = 0.6f },
                new QualityLevel { name = "Moyen", targetFrameRate = 30, drawDistance = 450f, fogStart = 150f, fogEnd = 450f },
                new QualityLevel { name = "Haut", targetFrameRate = 60, drawDistance = 600f, fogStart = 150f, fogEnd = 600f },
            };
        }

        /// <summary>
        /// Niveau à appliquer au démarrage : le choix enregistré s'il est valable, sinon le niveau par défaut
        /// (lui-même ramené dans les bornes). <paramref name="saved"/> &lt; 0 = rien d'enregistré.
        /// </summary>
        public int Resolve(int saved)
        {
            if (levels == null || levels.Length == 0) return 0;
            if (saved >= 0 && saved < levels.Length) return saved;
            return Clamp(defaultLevel);
        }

        /// <summary>Niveau suivant pour le bouton : Bas → Moyen → Haut → Bas.</summary>
        public int Next(int current)
        {
            if (levels == null || levels.Length == 0) return 0;
            return (Clamp(current) + 1) % levels.Length;
        }

        public QualityLevel Level(int index) => levels[Clamp(index)];

        private int Clamp(int index) => Math.Max(0, Math.Min(levels.Length - 1, index));
    }
}
