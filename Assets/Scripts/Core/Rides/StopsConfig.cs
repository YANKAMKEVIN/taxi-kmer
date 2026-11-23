using System;

namespace TaxiKmer.Core.Rides
{
    /// <summary>Un arrêt sur la route, repéré par sa distance depuis le début du tronçon.</summary>
    [Serializable]
    public class Stop
    {
        public string id;
        public string name;
        public int distanceAlongRouteMeters;
        /// <summary>Le lieu tel qu'un passager le dit dans une phrase (« au dispensaire de Messassi ») ; vide : tiré du nom.</summary>
        public SpokenPlace spoken = new SpokenPlace();
        /// <summary>
        /// Position du lieu dans le monde (m, idée n° 7) quand il est dans une rue latérale ; laissée à 0, 0 le lieu est
        /// sur l'itinéraire, à <see cref="distanceAlongRouteMeters"/>. Voir <see cref="World.StopPlacement"/>.
        /// </summary>
        public float x;
        public float z;

        public bool HasPosition => x != 0f || z != 0f;
    }

    /// <summary>
    /// Forme parlée d'un lieu (décision de Kevin, 23/11) : les répliques disent « je descends au dispensaire de
    /// Messassi », « la route du pont d'Emana », jamais « à Dispensaire de Messassi ». Chaque champ vide est tiré du nom
    /// court (<see cref="Talk.TalkValues.PlaceForms"/>).
    /// </summary>
    [Serializable]
    public class SpokenPlace
    {
        /// <summary>Avec son article, en minuscules (« le dispensaire de Messassi », « Emana ») : {destination}.</summary>
        public string name = "";
        /// <summary>Avec « à » contracté (« au dispensaire de Messassi », « à Emana ») : {a_destination}.</summary>
        public string at = "";
        /// <summary>Avec « de » contracté ou élidé (« du pont d'Emana », « d'Étoudi ») : {de_destination}.</summary>
        public string of = "";
        /// <summary>Sans article, comme en pidgin (« for Dispensaire Messassi ») : {destination_nue}.</summary>
        public string bare = "";
    }

    /// <summary>Arrêts du tronçon et répartition des carrures (données : Assets/Resources/Data/stops_*.json).</summary>
    [Serializable]
    public class StopsConfig
    {
        public Stop[] stops = Array.Empty<Stop>();
        public int minceWeight = 30;
        public int normalWeight = 50;
        public int costaudWeight = 20;
    }
}
