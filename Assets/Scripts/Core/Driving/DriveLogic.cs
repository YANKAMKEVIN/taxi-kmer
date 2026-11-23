using System;

namespace TaxiKmer.Core.Driving
{
    /// <summary>État des commandes à un instant (tactile ou clavier, déjà combinés).</summary>
    public struct DriveInput
    {
        public bool Left;
        public bool Right;
        public bool Accelerate;
        public bool Brake;
        public bool Horn;
        public bool Action;
        /// <summary>Bouton pause (ou Échap / Retour Android).</summary>
        public bool Pause;
        /// <summary>Bouton de qualité graphique (passe au niveau suivant). Ignoré par la conduite.</summary>
        public bool Quality;
        /// <summary>Frein à main (idée n° 26).</summary>
        public bool Handbrake;

        public static DriveInput operator |(DriveInput a, DriveInput b) => new DriveInput
        {
            Left = a.Left || b.Left,
            Right = a.Right || b.Right,
            Accelerate = a.Accelerate || b.Accelerate,
            Brake = a.Brake || b.Brake,
            Horn = a.Horn || b.Horn,
            Action = a.Action || b.Action,
            Pause = a.Pause || b.Pause,
            Quality = a.Quality || b.Quality,
            Handbrake = a.Handbrake || b.Handbrake,
        };

        public void Press(DriveControl control)
        {
            switch (control)
            {
                case DriveControl.SteerLeft: Left = true; break;
                case DriveControl.SteerRight: Right = true; break;
                case DriveControl.Accelerate: Accelerate = true; break;
                case DriveControl.Brake: Brake = true; break;
                case DriveControl.Horn: Horn = true; break;
                case DriveControl.Action: Action = true; break;
                case DriveControl.Pause: Pause = true; break;
                case DriveControl.Quality: Quality = true; break;
                case DriveControl.Handbrake: Handbrake = true; break;
            }
        }
    }

    public enum Gear
    {
        Forward,
        Reverse,
    }

    /// <summary>Consignes pour la physique du véhicule.</summary>
    public struct DriveCommand
    {
        /// <summary>Position du volant, de -1 (gauche) à 1 (droite).</summary>
        public float Steer;
        /// <summary>Angle des roues avant (degrés, négatif = gauche).</summary>
        public float SteerAngleDeg;
        /// <summary>Gaz, de -1 (marche arrière à fond) à 1 (marche avant à fond).</summary>
        public float Throttle;
        /// <summary>Freinage, de 0 à 1.</summary>
        public float Brake;
        public Gear Gear;
        /// <summary>Frein à main serré (0 ou 1) : couple de freinage en plus sur les roues arrière.</summary>
        public float Handbrake;
        /// <summary>Adhérence latérale des roues arrière (1 = normale ; moins avec le frein à main : l'arrière glisse).</summary>
        public float RearGrip;
    }

    /// <summary>
    /// Traduit les commandes du joueur en consignes (direction progressive, frein qui devient marche arrière
    /// quand on le maintient à l'arrêt, limiteur de vitesse). Sans moteur Unity : testable.
    /// </summary>
    public sealed class DriveLogic
    {
        private readonly SteeringConfig _steering;
        private readonly PedalConfig _pedals;
        private readonly CarConfig _car;
        private float _steer;
        private float _holdTimer;

        public Gear Gear { get; private set; } = Gear.Forward;

        /// <summary>Vitesse maximale imposée (km/h), en plus du limiteur, en marche avant comme en arrière : panne sèche (jalon 3), panne.</summary>
        public float SpeedCapKmh { get; set; } = float.PositiveInfinity;

        public DriveLogic(DriveConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _steering = config.steering;
            _pedals = config.pedals;
            _car = config.car ?? new CarConfig();
        }

        /// <param name="forwardSpeedMs">Vitesse le long de l'axe du taxi (m/s), négative en marche arrière.</param>
        public DriveCommand Step(DriveInput input, float forwardSpeedMs, float dt)
        {
            var cmd = new DriveCommand();
            var speedKmh = forwardSpeedMs * 3.6f;

            // Volant : monte vers la cible à riseRate, revient (ou change de côté) à returnRate.
            var target = (input.Right ? 1f : 0f) - (input.Left ? 1f : 0f);
            var returning = target == 0f || Math.Sign(target) != Math.Sign(_steer) && _steer != 0f;
            var rate = returning ? _steering.returnRate : _steering.riseRate;
            _steer = MoveTowards(_steer, target, rate * dt);
            cmd.Steer = _steer;
            var t = Clamp01(Math.Abs(speedKmh) / Math.Max(1f, _steering.highSpeedKmh));
            cmd.SteerAngleDeg = _steer * (_steering.maxAngleDeg + (_steering.highSpeedAngleDeg - _steering.maxAngleDeg) * t);

            var stopped = Math.Abs(speedKmh) < _pedals.stoppedKmh;
            if (speedKmh > _pedals.stoppedKmh) Gear = Gear.Forward;

            if (input.Accelerate && input.Brake)
            {
                cmd.Brake = 1f;
                _holdTimer = 0f;
            }
            else if (input.Accelerate)
            {
                _holdTimer = 0f;
                Gear = Gear.Forward;
                if (speedKmh < -_pedals.stoppedKmh) cmd.Brake = 1f; // on recule encore : on freine d'abord
                else cmd.Throttle = Limiter(speedKmh, Math.Min(_pedals.maxForwardKmh, SpeedCapKmh));
            }
            else if (input.Brake)
            {
                if (Gear == Gear.Reverse && speedKmh <= _pedals.stoppedKmh)
                {
                    cmd.Throttle = -Limiter(-speedKmh, Math.Min(_pedals.maxReverseKmh, SpeedCapKmh));
                }
                else if (!stopped)
                {
                    cmd.Brake = 1f;
                    _holdTimer = 0f;
                }
                else
                {
                    _holdTimer += dt;
                    if (_holdTimer >= _pedals.reverseHoldSeconds)
                    {
                        Gear = Gear.Reverse;
                        cmd.Throttle = -Limiter(-speedKmh, Math.Min(_pedals.maxReverseKmh, SpeedCapKmh));
                    }
                    else cmd.Brake = 1f;
                }
            }
            else
            {
                _holdTimer = 0f;
                if (stopped) cmd.Brake = _pedals.parkingBrake;
            }

            // Frein à main (idée n° 26) : coupe les gaz, bloque l'arrière qui décroche un peu pour pivoter.
            cmd.RearGrip = 1f;
            if (input.Handbrake)
            {
                cmd.Handbrake = 1f;
                cmd.Throttle = 0f;
                cmd.RearGrip = Math.Abs(speedKmh) > _pedals.stoppedKmh ? _car.handbrakeRearGrip : 1f;
            }

            cmd.Gear = Gear;
            return cmd;
        }

        /// <summary>Gaz plein jusqu'à la bande du limiteur, puis décroissance linéaire jusqu'à 0 à la vitesse maximale.</summary>
        private float Limiter(float speedKmh, float maxKmh)
        {
            if (maxKmh <= 0f) return 0f;
            var band = Math.Max(0.5f, maxKmh * _pedals.limiterBand);
            return Clamp01((maxKmh - speedKmh) / band);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
