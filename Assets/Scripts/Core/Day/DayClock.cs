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

        /// <summary>Part de la journée écoulée, de 0 (début) à 1 (heure de fin).</summary>
        public float Progress => Math.Min(_elapsedReal / _config.dayLengthRealSeconds, 1f);

        /// <summary>Temps réel écoulé depuis le début de la journée (s), pour la sauvegarde.</summary>
        public float ElapsedRealSeconds => _elapsedReal;

        /// <summary>Reprend la journée à ce point (sauvegarde), borné à la durée de la journée.</summary>
        internal void Restore(float elapsedRealSeconds) =>
            _elapsedReal = Math.Max(0f, Math.Min(elapsedRealSeconds, _config.dayLengthRealSeconds));

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

        /// <summary>Minutes de jeu avant l'heure de fin (0 une fois atteinte).</summary>
        public int MinutesLeft => _config.endHour * 60 - GameMinutes;

        /// <summary>Minutes de jeu depuis minuit, avec la fraction (pour une lumière qui avance sans à-coups).</summary>
        public float GameMinutesExact
        {
            get
            {
                var span = (_config.endHour - _config.startHour) * 60f;
                var t = Math.Min(_elapsedReal / _config.dayLengthRealSeconds, 1f);
                return _config.startHour * 60f + t * span;
            }
        }

        /// <summary>« 07:05 » ; refait seulement quand la minute change (lu à chaque image par le HUD).</summary>
        public string Display
        {
            get
            {
                var m = GameMinutes;
                if (m != _displayMinute) { _displayMinute = m; _display = $"{m / 60:00}:{m % 60:00}"; }
                return _display;
            }
        }

        private int _displayMinute = -1;
        private string _display = "";

        /// <summary>Fait passer <paramref name="minutes"/> minutes de jeu d'un coup (temps perdu : hôpital…).</summary>
        public void AdvanceGameMinutes(int minutes)
        {
            if (minutes <= 0) return;
            var span = (_config.endHour - _config.startHour) * 60f;
            Advance(minutes / span * _config.dayLengthRealSeconds);
        }

        public void Advance(float realSeconds)
        {
            if (realSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(realSeconds));
            _elapsedReal = Math.Min(_elapsedReal + realSeconds, _config.dayLengthRealSeconds);
        }
    }
}
