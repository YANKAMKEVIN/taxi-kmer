using System;

namespace TaxiKmer.Core.Day
{
    /// <summary>Horloge du jeu : startHour → endHour en dayLengthRealSeconds secondes réelles.</summary>
    public sealed class DayClock
    {
        private readonly DayConfig _config;
        private float _elapsedReal;

        public DayClock(DayConfig config)
        {
            if (config.endHour <= config.startHour) throw new ArgumentException("endHour doit suivre startHour.");
            if (config.dayLengthRealSeconds <= 0f) throw new ArgumentException("Durée de journée nulle.");
            _config = config;
        }

        public bool IsOver => _elapsedReal >= _config.dayLengthRealSeconds;

        /// <summary>Minutes de jeu écoulées depuis minuit.</summary>
        public int GameMinutes
        {
            get
            {
                var span = (_config.endHour - _config.startHour) * 60;
                var t = Math.Min(_elapsedReal / _config.dayLengthRealSeconds, 1f);
                return _config.startHour * 60 + (int)Math.Floor(t * span);
            }
        }

        public string Display => $"{GameMinutes / 60:00}:{GameMinutes % 60:00}";

        public void Advance(float realSeconds)
        {
            if (realSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(realSeconds));
            _elapsedReal = Math.Min(_elapsedReal + realSeconds, _config.dayLengthRealSeconds);
        }
    }
}
