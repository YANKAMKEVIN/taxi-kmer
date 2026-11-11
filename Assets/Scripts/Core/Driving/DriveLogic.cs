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

        public static DriveInput operator |(DriveInput a, DriveInput b) => new DriveInput
        {
            Left = a.Left || b.Left,
            Right = a.Right || b.Right,
            Accelerate = a.Accelerate || b.Accelerate,
            Brake = a.Brake || b.Brake,
            Horn = a.Horn || b.Horn,
            Action = a.Action || b.Action,
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
    }

    /// <summary>
    /// Traduit les commandes du joueur en consignes (direction progressive, frein qui devient marche arrière
    /// quand on le maintient à l'arrêt, limiteur de vitesse). Sans moteur Unity : testable.
    /// </summary>
    public sealed class DriveLogic
    {
        private readonly SteeringConfig _steering;
        private readonly PedalConfig _pedals;
        private float _steer;
        private float _holdTimer;

        public Gear Gear { get; private set; } = Gear.Forward;

        public DriveLogic(DriveConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _steering = config.steering;
            _pedals = config.pedals;
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
                else cmd.Throttle = Limiter(speedKmh, _pedals.maxForwardKmh);
            }
            else if (input.Brake)
            {
                if (Gear == Gear.Reverse && speedKmh <= _pedals.stoppedKmh)
                {
                    cmd.Throttle = -Limiter(-speedKmh, _pedals.maxReverseKmh);
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
                        cmd.Throttle = -Limiter(-speedKmh, _pedals.maxReverseKmh);
                    }
                    else cmd.Brake = 1f;
                }
            }
            else
            {
                _holdTimer = 0f;
                if (stopped) cmd.Brake = _pedals.parkingBrake;
            }

            cmd.Gear = Gear;
            return cmd;
        }

        /// <summary>Gaz plein jusqu'à la bande du limiteur, puis décroissance linéaire jusqu'à 0 à la vitesse maximale.</summary>
        private float Limiter(float speedKmh, float maxKmh)
        {
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
