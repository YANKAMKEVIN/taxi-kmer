using System;
using System.Collections.Generic;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;

namespace TaxiKmer.Core.Road
{
    /// <summary>Un choc entre le taxi et un véhicule du trafic, vu de la logique : vitesse au choc et fautif.</summary>
    public struct CrashJudgement
    {
        /// <summary>Vitesse à laquelle les deux véhicules se rapprochaient au moment du choc (km/h, ≥ 0).</summary>
        public float ImpactKmh;
        public bool PlayerAtFault;
    }

    /// <summary>
    /// Qui est fautif dans un accrochage (jalon 6, règle simple par défaut de Claude) : celui qui fonce sur l'autre. On
    /// compare ce que chacun apporte au rapprochement (sa vitesse projetée vers l'autre) : le taxi qui emboutit une voiture
    /// devant lui, ou qui recule dans celle de derrière, est fautif ; la voiture qui emboutit le taxi arrêté l'est. À
    /// égalité, le taxi est fautif.
    /// </summary>
    public static class CollisionFault
    {
        public static CrashJudgement Judge(PlayerCar player, TrafficVehicle other) =>
            Judge(player, other.X, other.Z, other.DirX, other.DirZ, other.Speed);

        /// <param name="otherSpeed">Vitesse de l'autre (m/s, dans le sens de <paramref name="otherDirX"/>, <paramref name="otherDirZ"/>).</param>
        public static CrashJudgement Judge(PlayerCar player, float otherX, float otherZ, float otherDirX, float otherDirZ, float otherSpeed)
        {
            var nx = otherX - player.X;
            var nz = otherZ - player.Z;
            var len = (float)Math.Sqrt(nx * nx + nz * nz);
            if (len < 1e-4f) { nx = player.DirX; nz = player.DirZ; }
            else { nx /= len; nz /= len; }
            var mine = (player.DirX * nx + player.DirZ * nz) * player.Speed; // > 0 : le taxi va vers l'autre
            var theirs = -(otherDirX * nx + otherDirZ * nz) * otherSpeed;   // > 0 : l'autre va vers le taxi
            return new CrashJudgement
            {
                ImpactKmh = Math.Max(0f, mine + theirs) * 3.6f,
                PlayerAtFault = mine >= theirs,
            };
        }
    }

    /// <summary>Le choix demandé au joueur après un accrochage.</summary>
    public enum CrashChoice
    {
        None,
        /// <summary>Le joueur est fautif : s'arrêter (payer) ou partir (fuite, option B de Kevin).</summary>
        StopOrFlee,
        /// <summary>L'autre fuit : le poursuivre ou le laisser partir.</summary>
        ChaseOrLetGo,
    }

    /// <summary>
    /// Accrochages du taxi avec le trafic (jalon 6, décisions de Kevin du 21/11) : la présentation signale un choc
    /// (<see cref="Report"/>), la logique tire la suite (<see cref="RoadIncidents.Collision"/>), abîme le taxi et, si le
    /// joueur est fautif, lui demande « s'arrêter / partir » ; si l'autre fuit, « poursuivre / laisser ». Sans réponse dans
    /// le délai des données : le joueur qui roule encore est parti, sinon il s'est arrêté ; le chauffard est laissé.
    /// Chaque méthode rend la phrase à afficher (vide si rien ne se passe).
    /// </summary>
    public sealed class TrafficCrash
    {
        private readonly RoadConfig _config;
        private readonly Random _random;
        private readonly HashSet<int> _seen = new HashSet<int>();
        private float _quietUntil = float.NegativeInfinity;
        private float _pendingFor;

        public CrashChoice Pending { get; private set; }
        /// <summary>L'accrochage en attente d'un choix (ou le dernier).</summary>
        public CollisionOutcome Current { get; private set; }
        /// <summary>Nombre d'accrochages comptés dans la journée.</summary>
        public int Count { get; private set; }

        public TrafficCrash(RoadConfig config, Random random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Le taxi vient de toucher le véhicule <paramref name="vehicleId"/>. Ignoré : jalon coupé, journée finie, choc trop
        /// léger, choix en attente, même véhicule déjà compté, ou juste après un autre accrochage (<paramref name="now"/> en
        /// secondes réelles).
        /// </summary>
        public string Report(Street street, VehicleCondition car, int vehicleId, CrashJudgement crash, float now)
        {
            if (street == null) throw new ArgumentNullException(nameof(street));
            if (car == null) throw new ArgumentNullException(nameof(car));
            if (!_config.enabled || street.DayFinished || Pending != CrashChoice.None) return "";
            if (crash.ImpactKmh < _config.collisionMinKmh || now < _quietUntil || _seen.Contains(vehicleId)) return "";
            _seen.Add(vehicleId);
            Count++;
            var c = RoadIncidents.Collision(_config, crash.ImpactKmh, crash.PlayerAtFault, _random);
            Current = c;
            car.AddDamage(c.Damage);
            _pendingFor = 0f;
            _quietUntil = now + Math.Max(0f, _config.collisionCooldownSeconds);
            if (c.PlayerAtFault)
            {
                if (_config.playerCanFlee)
                {
                    Pending = CrashChoice.StopOrFlee;
                    return $"Accrochage, c'est ta faute : t'arrêter (payer {DaySummary.Money(c.PaidToOther)}, {Minutes(c.LostMinutes)}) ou partir ?";
                }
                return Settle(street);
            }
            if (c.HitAndRun)
            {
                Pending = CrashChoice.ChaseOrLetGo;
                return "Accrochage : le chauffard prend la fuite ! Le poursuivre ou le laisser ?";
            }
            street.ApplyOtherAtFaultCollision(c);
            return c.OtherDriver == "negocie"
                ? $"Accrochage, l'autre est fautif : il négocie et paie {DaySummary.Money(c.Received)} ({Minutes(c.LostMinutes)} perdues)"
                : $"Accrochage, l'autre est fautif : il paie les réparations, {DaySummary.Money(c.Received)} ({Minutes(c.LostMinutes)} perdues)";
        }

        /// <summary>Fautif, le joueur s'arrête : il paie les dégâts de l'autre et perd du temps.</summary>
        public string Stop(Street street)
        {
            if (Pending != CrashChoice.StopOrFlee) return "";
            return Settle(street);
        }

        private string Settle(Street street)
        {
            Pending = CrashChoice.None;
            street.ApplyAtFaultCollision(Current, false, default);
            return $"Tu t'arrêtes et tu paies les dégâts : {DaySummary.Money(Current.PaidToOther)}, {Minutes(Current.LostMinutes)} perdues";
        }

        /// <summary>Fautif, le joueur part (option B) : réputation perdue ; la police peut le retrouver.</summary>
        public string Flee(Street street)
        {
            if (Pending != CrashChoice.StopOrFlee) return "";
            Pending = CrashChoice.None;
            var flight = RoadIncidents.PlayerFlees(_config, Current, _random);
            street.ApplyAtFaultCollision(Current, true, flight);
            var rep = $"réputation {flight.ReputationDelta}";
            return flight.Found
                ? $"Tu files ({rep})… mais la police t'a retrouvé : {DaySummary.Money(flight.Fine)} et {Minutes(flight.LostMinutes)} de convocation"
                : $"Tu files sans t'arrêter ({rep}). Personne ne t'a retrouvé, cette fois";
        }

        /// <summary>L'autre fuit : le joueur le poursuit (temps perdu ; rattrapé, il paie les réparations).</summary>
        public string Chase(Street street)
        {
            if (Pending != CrashChoice.ChaseOrLetGo) return "";
            Pending = CrashChoice.None;
            var chase = RoadIncidents.Chase(_config, Current, _random);
            street.ApplyChase(chase);
            return chase.Caught
                ? $"Rattrapé après {Minutes(chase.LostMinutes)} de poursuite : il paie les réparations, {DaySummary.Money(chase.Received)}"
                : $"Il t'a semé après {Minutes(chase.LostMinutes)} de poursuite";
        }

        /// <summary>L'autre fuit : le joueur le laisse partir (quelques minutes perdues, rien reçu).</summary>
        public string LetGo(Street street)
        {
            if (Pending != CrashChoice.ChaseOrLetGo) return "";
            Pending = CrashChoice.None;
            var gone = Current;
            gone.Received = 0; // il a filé : il ne paie rien
            street.ApplyOtherAtFaultCollision(gone);
            return "Tu le laisses filer : les réparations sont pour toi";
        }

        /// <summary>
        /// À chaque image : sans réponse au bout de <c>collisionChoiceSeconds</c>, le joueur fautif qui roule encore au-dessus
        /// de <c>collisionDriveAwayKmh</c> est parti, sinon il s'est arrêté ; le chauffard en fuite est laissé.
        /// </summary>
        public string Tick(Street street, float dt, float speedKmh)
        {
            if (Pending == CrashChoice.None) return "";
            if (street.DayFinished) { Pending = CrashChoice.None; return ""; }
            _pendingFor += Math.Max(0f, dt);
            if (_pendingFor < _config.collisionChoiceSeconds) return "";
            if (Pending == CrashChoice.ChaseOrLetGo) return LetGo(street);
            return Math.Abs(speedKmh) >= _config.collisionDriveAwayKmh ? Flee(street) : Stop(street);
        }

        /// <summary>Durée lisible : 30 → « 30 min », 90 → « 1 h 30 ».</summary>
        public static string Minutes(int minutes)
        {
            minutes = Math.Max(0, minutes);
            if (minutes < 60) return $"{minutes} min";
            return minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60:00}";
        }
    }
}
