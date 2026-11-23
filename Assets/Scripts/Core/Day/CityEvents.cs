using System;

namespace TaxiKmer.Core.Day
{
    /// <summary>Un événement de ville rare (idée n° 14) : cortège officiel, match des Lions indomptables…</summary>
    [Serializable]
    public class CityEventData
    {
        public string id = "";
        public string name = "";
        /// <summary>Chance qu'il ait lieu un jour permis.</summary>
        public float chancePerDay;
        /// <summary>Jours de la semaine permis (indices de calendar.json, 0 = lundi) ; vide : tous.</summary>
        public int[] weekdays = Array.Empty<int>();
        public string fromTime = "";
        public string untilTime = "";
        /// <summary>Pendant l'événement : densité du trafic et cadence des clients multipliées.</summary>
        public float trafficFactor = 1f;
        public float clientsFactor = 1f;
        /// <summary>Tronçon fermé (m le long de l'itinéraire) ; closedToAlong ≤ closedFromAlong : rien de fermé.</summary>
        public float closedFromAlong;
        public float closedToAlong;
        /// <summary>Annonce au début de la journée, et à la fin de l'événement.</summary>
        public string announce = "";
        public string endText = "";
        public string statut = "proposé";
    }

    /// <summary>Événements de ville (données : Assets/Resources/Data/city_events.json). Ton et fréquence à valider par Kevin.</summary>
    [Serializable]
    public class CityEventsConfig
    {
        public bool enabled = true;
        public CityEventData[] events =
        {
            new CityEventData
            {
                id = "cortege", name = "Cortège officiel à Étoudi", chancePerDay = 0.05f, weekdays = new[] { 0, 1, 2, 3, 4 },
                fromTime = "08:00", untilTime = "09:30", trafficFactor = 1.6f, clientsFactor = 0.8f,
                closedFromAlong = 5900f, closedToAlong = 6200f,
                announce = "Cortège officiel ce matin : la route est fermée devant le palais d'Étoudi de 08:00 à 09:30.",
                endText = "Le cortège est passé : la route d'Étoudi est rouverte.",
            },
            new CityEventData
            {
                id = "match", name = "Match des Lions indomptables", chancePerDay = 0.15f, weekdays = new[] { 2, 5, 6 },
                fromTime = "16:30", untilTime = "20:00", trafficFactor = 1.3f, clientsFactor = 1.4f,
                announce = "Ce soir, les Lions indomptables jouent : les supporters cherchent un taxi après 16:30.",
                endText = "Le match est fini : la ville se calme.",
            },
        };
    }

    /// <summary>L'événement du jour (ou aucun) et ses effets minute par minute (pur, testable).</summary>
    public sealed class CityEventDay
    {
        public CityEventData Event { get; }
        private readonly int _from, _until;

        public CityEventDay(CityEventData e)
        {
            Event = e;
            _from = e != null ? Talk.LineData.ParseTime(e.fromTime) : -1;
            _until = e != null ? Talk.LineData.ParseTime(e.untilTime) : -1;
        }

        /// <summary>
        /// Tire l'événement d'un jour : au plus un, dans l'ordre des données ; même jour et même graine, même résultat.
        /// </summary>
        public static CityEventDay Draw(CityEventsConfig config, CalendarDay day, int careerSeed)
        {
            if (config == null || !config.enabled || config.events == null) return new CityEventDay(null);
            var random = new Random(unchecked(careerSeed * 7919 + day.DayNumber * 104729));
            foreach (var e in config.events)
            {
                var roll = random.NextDouble();
                if (e.weekdays != null && e.weekdays.Length > 0 && Array.IndexOf(e.weekdays, day.Weekday) < 0) continue;
                if (roll < e.chancePerDay) return new CityEventDay(e);
            }
            return new CityEventDay(null);
        }

        public bool Any => Event != null;

        public bool ActiveAt(int minute) => Event != null && (_from < 0 || minute >= _from) && (_until < 0 || minute < _until);

        public float TrafficFactor(int minute) => ActiveAt(minute) ? Event.trafficFactor : 1f;
        public float ClientsFactor(int minute) => ActiveAt(minute) ? Event.clientsFactor : 1f;

        public bool HasClosure => Event != null && Event.closedToAlong > Event.closedFromAlong;

        /// <summary>Le tronçon est fermé à cette minute et ce point de l'itinéraire est dedans.</summary>
        public bool ClosedAt(float along, int minute) =>
            HasClosure && ActiveAt(minute) && along >= Event.closedFromAlong && along <= Event.closedToAlong;

        /// <summary>Id pour les répliques liées à l'événement (vide hors événement).</summary>
        public string ActiveId(int minute) => ActiveAt(minute) ? Event.id : "";
    }
}
