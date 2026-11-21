using System;

namespace TaxiKmer.Core.Day
{
    /// <summary>Un jour de la semaine (données : Assets/Resources/Data/calendar.json).</summary>
    [Serializable]
    public class WeekdayData
    {
        public string name = "";
        /// <summary>Clients au bord de la route : multiplie la cadence d'arrivée (1 = normal, 0,6 = 40 % de moins).</summary>
        public float clients = 1f;
        /// <summary>Trafic et piétons : multiplie leur densité.</summary>
        public float traffic = 1f;
        /// <summary>Jour de marché (annoncé au début de la journée).</summary>
        public bool market;
    }

    /// <summary>
    /// Calendrier (jalon 3, feuille de route : jours de la semaine, dimanche plus calme, jours de marché).
    /// Valeurs par défaut à régler avec Kevin.
    /// </summary>
    [Serializable]
    public class CalendarConfig
    {
        /// <summary>Jour de la semaine du jour 1 de la carrière (indice dans <see cref="weekdays"/> : 0 = lundi).</summary>
        public int firstWeekday;
        /// <summary>
        /// Date du jour 1 de la carrière (pour les saisons de Yaoundé, jalon 6 : pluie de mars à juin et de septembre à
        /// novembre, tranché par Kevin le 21/11). Par défaut le lundi 2 mars 2026, qui tombe bien un lundi
        /// (<see cref="firstWeekday"/>) : valeur par défaut de Claude.
        /// </summary>
        public int firstYear = 2026;
        public int firstMonth = 3;
        public int firstDayOfMonth = 2;
        /// <summary>Nom du marché annoncé les jours de marché.</summary>
        public string marketName = "marché de Messassi";
        public WeekdayData[] weekdays =
        {
            new WeekdayData { name = "lundi" },
            new WeekdayData { name = "mardi" },
            new WeekdayData { name = "mercredi", clients = 1.25f, traffic = 1.15f, market = true },
            new WeekdayData { name = "jeudi" },
            new WeekdayData { name = "vendredi" },
            new WeekdayData { name = "samedi", clients = 1.25f, traffic = 1.15f, market = true },
            new WeekdayData { name = "dimanche", clients = 0.6f, traffic = 0.6f },
        };
    }

    /// <summary>Ce que le calendrier dit d'un jour de la carrière.</summary>
    public struct CalendarDay
    {
        public int DayNumber;
        /// <summary>Indice dans la semaine (0 = premier jour des données, lundi par défaut).</summary>
        public int Weekday;
        public string Name;
        public float ClientsFactor;
        public float TrafficFactor;
        public bool Market;
        public bool Calm => ClientsFactor < 1f;
        /// <summary>Mois (1 = janvier) et jour du mois de ce jour de la carrière.</summary>
        public int Month;
        public int DayOfMonth;
    }

    /// <summary>Jour de la semaine d'un jour de la carrière, et son effet sur les clients et le trafic.</summary>
    public sealed class Calendar
    {
        private readonly CalendarConfig _config;

        public Calendar(CalendarConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (_config.weekdays == null || _config.weekdays.Length == 0) throw new ArgumentException("Calendrier sans jours.");
        }

        public int WeekLength => _config.weekdays.Length;

        /// <summary>Nom du marché des jours de marché (« marché de Messassi »).</summary>
        public string MarketName => _config.marketName ?? "";

        public CalendarDay For(int dayNumber)
        {
            var n = WeekLength;
            var index = ((Math.Max(1, dayNumber) - 1 + _config.firstWeekday) % n + n) % n;
            var w = _config.weekdays[index];
            return new CalendarDay
            {
                DayNumber = Math.Max(1, dayNumber),
                Weekday = index,
                Name = string.IsNullOrEmpty(w.name) ? $"jour {index + 1}" : w.name,
                ClientsFactor = Math.Max(0f, w.clients),
                TrafficFactor = Math.Max(0f, w.traffic),
                Market = w.market,
                Month = DateOf(dayNumber).Month,
                DayOfMonth = DateOf(dayNumber).Day,
            };
        }

        /// <summary>Date d'un jour de la carrière (le jour 1 est la date des données).</summary>
        public DateTime DateOf(int dayNumber)
        {
            DateTime start;
            try { start = new DateTime(Math.Max(1, _config.firstYear), Math.Max(1, Math.Min(12, _config.firstMonth)), Math.Max(1, Math.Min(28, _config.firstDayOfMonth))); }
            catch (ArgumentOutOfRangeException) { start = new DateTime(2026, 3, 2); }
            return start.AddDays(Math.Max(1, dayNumber) - 1);
        }

        private static readonly string[] MonthNames = { "janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre" };

        /// <summary>« 2 mars ».</summary>
        public static string DateLabel(CalendarDay day) => day.Month >= 1 && day.Month <= 12 ? $"{day.DayOfMonth} {MonthNames[day.Month - 1]}" : "";

        /// <summary>« mer. » : abréviation pour le HUD, à côté du numéro du jour.</summary>
        public static string Short(CalendarDay day) => day.Name.Length <= 4 ? day.Name : day.Name.Substring(0, 3) + ".";

        /// <summary>« Jour 3 · mer. » (HUD).</summary>
        public static string HudLabel(CalendarDay day) => $"Jour {day.DayNumber} · {Short(day)}";

        /// <summary>« Mercredi, jour de marché à Messassi : plus de clients et plus de monde » (début de journée) ; vide un jour ordinaire.</summary>
        public string Announcement(CalendarDay day)
        {
            var name = Capitalize(day.Name);
            if (day.Market) return $"{name}, jour du {_config.marketName} : plus de clients, plus de monde sur la route";
            if (day.Calm) return $"{name} : la ville est plus calme, moins de clients";
            return "";
        }

        public static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
