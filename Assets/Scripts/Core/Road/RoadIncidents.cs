using System;
using System.Collections.Generic;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Road
{
    /// <summary>État mécanique du taxi (jalon 6) : dégâts, pneu crevé, panne.</summary>
    public sealed class VehicleCondition
    {
        private readonly RoadConfig _config;

        public float Damage { get; private set; }
        public bool FlatTyre { get; private set; }
        public bool BrokenDown { get; private set; }

        public VehicleCondition(RoadConfig config, float damage = 0f)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Damage = Clamp(damage);
        }

        public void AddDamage(float points) => Damage = Clamp(Damage + Math.Max(0f, points));
        public void Puncture() => FlatTyre = true;
        public void BreakDown() => BrokenDown = true;

        /// <summary>Le chauffeur change la roue lui-même.</summary>
        public void FitSpareWheel() => FlatTyre = false;

        /// <summary>Vitesse permise par l'état du taxi (infinie s'il va bien ; 0 en panne).</summary>
        public float SpeedCapKmh
        {
            get
            {
                if (BrokenDown) return 0f;
                var cap = float.PositiveInfinity;
                if (FlatTyre) cap = Math.Min(cap, _config.flatMaxKmh);
                if (Damage >= _config.damagedThreshold) cap = Math.Min(cap, _config.damagedMaxKmh);
                return cap;
            }
        }

        /// <summary>Devis du garage pour tout remettre en état : prix (FCFA, avant partage avec le propriétaire) et minutes de jeu.</summary>
        public void Quote(out int cost, out int minutes)
        {
            var dmg = (int)Math.Ceiling(Damage);
            cost = dmg * Math.Max(0, _config.repairCostPerDamage);
            minutes = dmg * Math.Max(0, _config.repairMinutesPerDamage);
            if (FlatTyre) { cost += _config.punctureRepairCost; minutes += _config.punctureRepairMinutes; }
            if (BrokenDown) { cost += _config.breakdownRepairCost; minutes += _config.breakdownRepairMinutes; }
        }

        public bool NeedsRepair => Damage > 0f || FlatTyre || BrokenDown;

        /// <summary>
        /// Garage : tout est réparé ; la part du chauffeur est une dépense du jour et le temps passe. Rend ce que le
        /// chauffeur a payé.
        /// </summary>
        public int RepairAtGarage(Shift shift)
        {
            if (shift == null) throw new ArgumentNullException(nameof(shift));
            if (!NeedsRepair) return 0;
            Quote(out var cost, out var minutes);
            var share = (int)Math.Round(cost * Math.Max(0f, Math.Min(1f, _config.driverShareOfRepairs)));
            shift.Ledger.AddExpense("Garage", share);
            shift.Clock.AdvanceGameMinutes(minutes);
            Damage = 0f;
            FlatTyre = false;
            BrokenDown = false;
            return share;
        }

        private static float Clamp(float v) => Math.Max(0f, Math.Min(100f, v));
    }

    /// <summary>Résultat d'un accrochage avec un véhicule du trafic.</summary>
    public struct CollisionOutcome
    {
        public bool PlayerAtFault;
        /// <summary>Autre fautif : ce qu'il fait (« paie », « negocie », « fuit ») ; vide si le joueur est fautif.</summary>
        public string OtherDriver;
        public bool HitAndRun => OtherDriver == "fuit";
        public float Damage;
        /// <summary>Argent reçu de l'autre (FCFA) : remboursement des réparations.</summary>
        public int Received;
        /// <summary>Argent payé à l'autre (FCFA) quand le joueur est fautif.</summary>
        public int PaidToOther;
        public int LostMinutes;
    }

    /// <summary>Les imprévus : accrochages, nids-de-poule, pannes, pluie (logique pure, aléatoire déterministe).</summary>
    public static class RoadIncidents
    {
        /// <summary>
        /// Accrochage à <paramref name="impactKmh"/> km/h. Qui est fautif est dit par la présentation (choc arrière, feu…).
        /// Fautif, le joueur paie les dégâts de l'autre ; sinon l'autre paie, négocie ou fuit (délit de fuite).
        /// </summary>
        public static CollisionOutcome Collision(RoadConfig config, float impactKmh, bool playerAtFault, Random random)
        {
            var speed = Math.Abs(impactKmh);
            var damage = speed * config.damagePerImpactKmh;
            var repair = (int)Math.Ceiling(damage) * config.repairCostPerDamage;
            if (playerAtFault)
                return new CollisionOutcome
                {
                    PlayerAtFault = true,
                    Damage = damage,
                    PaidToOther = RoundTo500(speed * config.otherDamagePerKmh),
                    LostMinutes = config.atFaultLostMinutes,
                };
            var other = Pick(config.otherDriverAtFault, random);
            return new CollisionOutcome
            {
                OtherDriver = other?.id ?? "fuit",
                Damage = damage,
                Received = other == null ? 0 : RoundTo500(repair * Math.Max(0f, other.payFactor)),
                LostMinutes = other?.lostMinutes ?? 0,
            };
        }

        /// <summary>
        /// Le joueur fautif fuit (option B, tranché par Kevin le 21/11) : il perd de la réputation tout de suite ; la police
        /// le retrouve selon une chance, et il paie alors plus que les dégâts de l'autre et perd du temps.
        /// </summary>
        public static PlayerFlightOutcome PlayerFlees(RoadConfig config, CollisionOutcome collision, Random random)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var o = new PlayerFlightOutcome { Fled = config.playerCanFlee && collision.PlayerAtFault };
            if (!o.Fled) return o;
            o.ReputationDelta = config.playerFleeReputation;
            o.Found = random.NextDouble() < config.playerFleeFoundChance;
            if (o.Found)
            {
                o.Fine = RoundTo500(collision.PaidToOther * Math.Max(0f, config.playerFleeFoundFactor));
                o.LostMinutes = Math.Max(0, config.playerFleeFoundMinutes);
            }
            return o;
        }

        /// <summary>
        /// Délit de fuite subi : le joueur poursuit le fautif (tranché par Kevin le 21/11). La poursuite prend du temps ;
        /// rattrapé, le fautif paie les réparations.
        /// </summary>
        public static ChaseOutcome Chase(RoadConfig config, CollisionOutcome collision, Random random)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var o = new ChaseOutcome { LostMinutes = Math.Max(0, config.chaseMinutes) };
            if (!collision.HitAndRun) return default;
            o.Chased = true;
            o.Caught = random.NextDouble() < config.chaseCatchChance;
            if (o.Caught) o.Received = (int)Math.Ceiling(collision.Damage) * config.repairCostPerDamage;
            return o;
        }

        /// <summary>Panne pendant ces <paramref name="meters"/> m ? (chance par km, plus avec les dégâts).</summary>
        public static bool BreakdownWhileDriving(RoadConfig config, VehicleCondition car, float meters, Random random)
        {
            if (meters <= 0f || car.BrokenDown) return false;
            var perKm = config.breakdownPerKm + car.Damage * config.breakdownPerKmPerDamage;
            var chance = 1.0 - Math.Pow(1.0 - Math.Min(1.0, Math.Max(0.0, perKm)), meters / 1000.0);
            return random.NextDouble() < chance;
        }

        private static OtherDriverData Pick(OtherDriverData[] list, Random random)
        {
            if (list == null) return null;
            var total = 0.0;
            foreach (var o in list) total += Math.Max(0f, o.weight);
            if (total <= 0.0) return null;
            var roll = random.NextDouble() * total;
            foreach (var o in list)
            {
                var w = Math.Max(0f, o.weight);
                if (w <= 0f) continue;
                if (roll < w) return o;
                roll -= w;
            }
            return null;
        }

        private static int RoundTo500(double v) => (int)(Math.Round(v / 500.0, MidpointRounding.AwayFromZero) * 500);
    }

    /// <summary>Ce qu'a donné la fuite du joueur fautif.</summary>
    public struct PlayerFlightOutcome
    {
        public bool Fled;
        /// <summary>La police l'a retrouvé.</summary>
        public bool Found;
        public int ReputationDelta;
        /// <summary>Retrouvé : ce qu'il paie (FCFA).</summary>
        public int Fine;
        public int LostMinutes;
    }

    /// <summary>Ce qu'a donné la poursuite d'un fautif qui fuit.</summary>
    public struct ChaseOutcome
    {
        public bool Chased;
        public bool Caught;
        /// <summary>Rattrapé : ce qu'il paie pour les réparations (FCFA).</summary>
        public int Received;
        public int LostMinutes;
    }

    /// <summary>Ce qu'a fait un nid-de-poule pris en roulant.</summary>
    public struct PotholeHit
    {
        public float Along;
        public float Damage;
        public bool Punctured;
    }

    /// <summary>Nids-de-poule le long de l'itinéraire (positions tirées une fois, même graine pour toute la carrière).</summary>
    public sealed class PotholeField
    {
        private readonly RoadConfig _config;
        private readonly List<float> _positions = new List<float>();

        public IReadOnlyList<float> Positions => _positions;

        public PotholeField(RoadConfig config, float routeLength, int seed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            var random = new Random(seed);
            var count = (int)Math.Round(Math.Max(0f, config.potholesPerKm) * routeLength / 1000f);
            for (var i = 0; i < count; i++) _positions.Add((float)(random.NextDouble() * routeLength));
            _positions.Sort();
        }

        /// <summary>Le taxi passe de <paramref name="from"/> à <paramref name="to"/> à cette vitesse : nids-de-poule pris.</summary>
        public List<PotholeHit> Drive(float from, float to, float speedKmh, VehicleCondition car, Random random)
        {
            var hits = new List<PotholeHit>();
            var lo = Math.Min(from, to);
            var hi = Math.Max(from, to);
            if (hi <= lo) return hits;
            var excess = Math.Abs(speedKmh) - _config.potholeSafeKmh;
            foreach (var p in _positions)
            {
                if (p < lo || p >= hi) continue;
                if (random.NextDouble() >= _config.potholeHitChance) continue;
                var hit = new PotholeHit { Along = p };
                if (excess > 0f)
                {
                    hit.Damage = excess * _config.potholeDamagePerKmh;
                    car?.AddDamage(hit.Damage);
                    hit.Punctured = car != null && !car.FlatTyre && random.NextDouble() < _config.potholePunctureChance;
                    if (hit.Punctured) car?.Puncture();
                }
                hits.Add(hit);
            }
            return hits;
        }
    }

    /// <summary>Le temps de la journée : une averse éventuelle et ses effets (route glissante, embouteillages, clients pressés).</summary>
    public sealed class WeatherDay
    {
        private readonly RoadConfig _config;

        public bool Rains { get; }
        public int RainFromMinute { get; }
        public int RainToMinute { get; }

        /// <param name="month">Mois du calendrier (1 = janvier) : saisons des pluies de Yaoundé ; 0 = inconnu (chance fixe).</param>
        public WeatherDay(RoadConfig config, int startMinute, int endMinute, Random random, int month = 0)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));
            Rains = random.NextDouble() < RainChance(config, month);
            if (!Rains) return;
            var duration = config.rainMinMinutes + (int)(random.NextDouble() * Math.Max(0, config.rainMaxMinutes - config.rainMinMinutes));
            var span = Math.Max(1, endMinute - startMinute - duration);
            RainFromMinute = startMinute + (int)(random.NextDouble() * span);
            RainToMinute = RainFromMinute + duration;
        }

        /// <summary>Chance de pluie d'un jour de ce mois (saisons de Yaoundé), sinon la chance fixe.</summary>
        public static float RainChance(RoadConfig config, int month)
        {
            var byMonth = config.rainChanceByMonth;
            if (month >= 1 && month <= 12 && byMonth != null && byMonth.Length == 12) return byMonth[month - 1];
            return config.rainChancePerDay;
        }

        /// <summary>Le mois est-il dans une saison des pluies de Yaoundé (mars-juin, septembre-novembre) ?</summary>
        public static bool RainySeason(int month) => (month >= 3 && month <= 6) || (month >= 9 && month <= 11);

        public bool RainingAt(int minute) => Rains && minute >= RainFromMinute && minute < RainToMinute;

        public float Grip(int minute) => RainingAt(minute) ? _config.rainGrip : 1f;
        public float TrafficFactor(int minute) => RainingAt(minute) ? _config.rainTraffic : 1f;
        public float ClientsFactor(int minute) => RainingAt(minute) ? _config.rainClients : 1f;
        public float PatienceFactor(int minute) => RainingAt(minute) ? _config.rainPatience : 1f;
    }
}
