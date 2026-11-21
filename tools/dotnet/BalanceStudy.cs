// Étude d'équilibrage (dotnet seulement) : un « joueur moyen » automatique enchaîne des journées et des carrières avec
// TOUTE la logique active du jeu (clients, places, carburant et station, panne sèche, réputation et ses effets,
// disputes d'argent, contrôles de police, nids-de-poule, crevaisons, pannes, pluie, accrochages, calendrier,
// propriétaire), puis balaye une grille de réglages (consommation, prix, versement).
// Ne change AUCUNE valeur de jeu : les réglages sont appliqués à des copies des données, en mémoire.
// Résultat et méthode : docs/tech/equilibrage.md. Grille complète : tools/test_core.sh "Category=Equilibrage" (≈ 5 min).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Police;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Road;
using TaxiKmer.Core.Talk;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.DotnetTests
{
    /// <summary>Un réglage de la grille : seuls ces trois nombres changent par rapport aux données du jeu.</summary>
    public struct BalanceSetting
    {
        /// <summary>Remplace fuel.json → consumptionScale.</summary>
        public float ConsumptionScale;
        /// <summary>Multiplie chaque prix de fares.json (arrondi à 50 FCFA).</summary>
        public float PriceMultiplier;
        /// <summary>Remplace day.json → versement.</summary>
        public int Versement;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "conso ×{0:0.##}, prix ×{1:0.##}, versement {2}", ConsumptionScale, PriceMultiplier, Versement);
    }

    /// <summary>
    /// Comportement du « joueur moyen » (hypothèses de l'étude, pas des valeurs de jeu) : vitesse de croisière, choix aux
    /// contrôles et aux disputes, fréquence des accrochages (la logique des accrochages est celle du jeu, mais le choc
    /// lui-même vient de la physique d'Unity : on le tire au hasard au kilomètre).
    /// </summary>
    public sealed class AveragePlayer
    {
        /// <summary>Vitesse de croisière entre deux arrêts (feux et ralentissements compris).</summary>
        public float CruiseKmh = 35f;
        public float StopSeconds = 4f;
        public float UTurnSeconds = 8f;
        /// <summary>Passe à la station sous cette part du réservoir, remplit jusqu'à celle-ci.</summary>
        public float RefuelBelow = 0.25f;
        public float RefuelUpTo = 0.5f;
        /// <summary>Accrochages avec le trafic par km parcouru (≈ 1 tous les 3 jours à 20 km par jour).</summary>
        public float CrashesPerKm = 1f / 60f;
        public float CrashMinKmh = 8f;
        public float CrashMaxKmh = 20f;
        /// <summary>Part des accrochages où le joueur est fautif.</summary>
        public float CrashAtFaultShare = 0.5f;
        /// <summary>Accepte la contre-offre d'un client qui négocie.</summary>
        public bool AcceptHaggle = true;
        /// <summary>Contrôle : négocie au plus ce nombre de fois, puis paie (ne conteste pas, ne fuit pas).</summary>
        public int PoliceNegotiations = 1;
        /// <summary>Joue avec le trafic simulé (taxis concurrents et mototaxis qui prennent des clients) : ≈ 50 fois plus lent.</summary>
        public bool WithCity;
    }

    /// <summary>Ce qui passe d'un jour à l'autre (comme la sauvegarde du jeu), hors argent.</summary>
    public sealed class CareerCarry
    {
        public int DayNumber = 1;
        public int Reputation = -1;
        public float TankLitres = -1f;
        public float Damage;
        /// <summary>Papiers de la carrière (null : ceux du début de carrière), renouvelés au début de chaque journée.</summary>
        public PaperSet Papers;
    }

    /// <summary>Une journée jouée, avec les compteurs utiles à l'étude.</summary>
    public sealed class StudyDay
    {
        public DaySummary Summary;
        public float LitresBurned;
        public int Controls;
        public int PolicePaid;
        public int Punctures;
        public int Breakdowns;
        public int Crashes;
        public int Haggles;
        public int Refusals;
        public int ClientsLostToOthers;
        public int Reputation;
        /// <summary>Papiers renouvelés au début de la journée et leur coût (payé sur l'épargne, avant la journée).</summary>
        public int PapersRenewed;
        public int RenewalCost;
        /// <summary>Contrôles où un papier expiré a été trouvé.</summary>
        public int ExpiredPaperControls;
    }

    /// <summary>Les données du jeu, lues une fois (chaque journée en fait des copies avant de les régler).</summary>
    public sealed class GameData
    {
        public string DayJson, FuelJson, FaresJson;
        public SeatingConfig Seating;
        public StopsConfig Stops;
        public StreetConfig Street;
        public CalendarConfig Calendar;
        public ReputationConfig Reputation;
        public DialogueConfig Dialogues;
        public RoadConfig Road;
        public PoliceConfig Police;
        public OwnerConfig Owner;
        public WorldData World;
        public CityConfig City;

        public static GameData Load(bool withWorld = false)
        {
            var dir = System.IO.Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "Data");
            string Read(string n) => System.IO.File.ReadAllText(System.IO.Path.Combine(dir, n));
            return new GameData
            {
                DayJson = Read("day.json"),
                FuelJson = Read("fuel.json"),
                FaresJson = Read("fares.json"),
                Seating = BalanceBot.Load<SeatingConfig>("seating.json"),
                Stops = BalanceBot.Load<StopsConfig>("stops_messassi_etoudi.json"),
                Street = BalanceBot.Load<StreetConfig>("street.json"),
                Calendar = BalanceBot.Load<CalendarConfig>("calendar.json"),
                Reputation = BalanceBot.Load<ReputationConfig>("reputation.json"),
                Dialogues = BalanceBot.Load<DialogueConfig>("dialogues.json"),
                Road = BalanceBot.Load<RoadConfig>("road.json"),
                Police = BalanceBot.Load<PoliceConfig>("police.json"),
                Owner = BalanceBot.Load<OwnerConfig>("owner.json"),
                World = withWorld ? BalanceBot.Load<WorldData>("world_messassi_etoudi.json") : null,
                City = withWorld ? BalanceBot.Load<CityConfig>("traffic.json") : null,
            };
        }

        private static readonly System.Text.Json.JsonSerializerOptions Opts = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        public DayConfig DayFor(BalanceSetting s)
        {
            var d = System.Text.Json.JsonSerializer.Deserialize<DayConfig>(DayJson, Opts);
            d.versement = s.Versement;
            return d;
        }
        public FuelConfig FuelFor(BalanceSetting s)
        {
            var f = System.Text.Json.JsonSerializer.Deserialize<FuelConfig>(FuelJson, Opts);
            f.consumptionScale = s.ConsumptionScale;
            return f;
        }
        public FareConfig FaresFor(BalanceSetting s) =>
            BalanceStudy.ScaleFares(System.Text.Json.JsonSerializer.Deserialize<FareConfig>(FaresJson, Opts), s.PriceMultiplier);
        public float RouteLength => Stops.stops[Stops.stops.Length - 1].distanceAlongRouteMeters;
    }

    /// <summary>Le joueur moyen joue une journée, branchée comme dans StreetView (mêmes objets, même ordre).</summary>
    public static class StudyDayPlayer
    {
        public static StudyDay Play(GameData data, BalanceSetting setting, AveragePlayer player, CareerCarry carry, int seed)
        {
            var day = data.DayFor(setting);
            var fuel = data.FuelFor(setting);
            var stops = data.Stops;
            var routeLength = data.RouteLength;
            var shift = new Shift(day, data.Seating, fuel, carry.TankLitres >= 0f ? carry.TankLitres : fuel.startLitres);
            var street = new Street(data.Street, stops, new ClientFactory(stops, data.FaresFor(setting), seed), shift, routeLength);
            var today = new TaxiKmer.Core.Day.Calendar(data.Calendar).For(carry.DayNumber);
            street.SpawnRateScale = today.ClientsFactor;
            street.Reputation = new DriverReputation(data.Reputation, carry.Reputation >= 0 ? carry.Reputation : (int?)null);
            if (data.Dialogues.enabled)
            {
                street.FareDisputes = data.Dialogues.FareRules();
                street.DisputeRandom = new Random(seed + 59);
            }
            int start = day.startHour * 60, end = day.endHour * 60;
            var road = new RoadDay(data.Road, routeLength, 20261121, carry.Damage, start, end, today.Month, new Random(seed + 61));
            var crash = new TrafficCrash(data.Road, new Random(seed + 62));
            // Papiers : renouvelés à l'échéance au début de la journée, payés sur l'épargne (décision de Kevin du 21/11).
            // L'étude suppose l'épargne suffisante (exact tant que les coûts sont à 0, jalon-04.md) ; le coût est retiré
            // de l'épargne par RunCareers, avant la clôture de la journée.
            if (carry.Papers == null) carry.Papers = new PaperSet(data.Police);
            var result = new StudyDay();
            if (data.Police.autoRenewPapers)
            {
                var renewed = PaperRenewal.Renew(carry.Papers, carry.DayNumber, int.MaxValue);
                result.PapersRenewed = renewed.Count;
                result.RenewalCost = PaperRenewal.TotalPaid(renewed);
            }
            var police = new PoliceDay(data.Police, carry.Papers, new PoliceContacts(data.Police), carry.DayNumber, routeLength, start, end, new Random(seed + 47));
            var luck = new Random(seed + 71); // accrochages (physique d'Unity remplacée par un tirage)

            CityLife city = null;
            TrafficPath routePath = null;
            if (player.WithCity && data.World != null)
            {
                city = new CityLife(data.City, data.World, street, seed + 17, 5.5f);
                routePath = new TrafficPath(data.World.route.xz);
            }

            const float dt = 0.25f;
            float along = 0f, wait = 0f, seconds = 0f;
            var dir = 1;
            var vehicleId = 0;
            var guard = 0;
            var populated = false;

            PlayerCar Car(float speedKmh)
            {
                routePath.Sample(along, dir * 1.7f, out var x, out var z, out var dx, out var dz);
                return new PlayerCar { X = x, Z = z, DirX = dir * dx, DirZ = dir * dz, Speed = speedKmh / 3.6f, Length = 4.4f, Width = 1.72f };
            }

            while (!street.DayFinished && guard++ < 1000000)
            {
                var here = new TaxiOnRoute { Along = along, Direction = dir, Lateral = dir * 1.7f, SpeedKmh = 0f };
                // Choix immédiats du joueur (boutons qui bloquent ou attendent une réponse).
                if (police.Current != null) ResolveControl(police, street, player, data.Police.impoundFee);
                if (crash.Pending == CrashChoice.StopOrFlee) crash.Stop(street);
                else if (crash.Pending == CrashChoice.ChaseOrLetGo) crash.LetGo(street);
                if (road.Car.BrokenDown) road.CallMechanic(shift);
                else if (road.Car.FlatTyre) road.FitSpareWheel(shift);
                if (street.PendingHaggle != null) { result.Haggles++; street.AnswerHaggle(player.AcceptHaggle); }
                if (street.PendingRefusal != null) { result.Refusals++; street.ResolveRefusal(false); }
                if (street.DryTankChoice(here)) street.CallTechnician(out _);
                if (street.DayFinished) break;

                var moving = wait <= 0f;
                var speed = moving ? Math.Min(player.CruiseKmh, Math.Min(shift.Tank.SpeedCapKmh, road.SpeedCapKmh)) : 0f;
                var taxi = new TaxiOnRoute { Along = along, Direction = dir, Lateral = dir * 1.7f, SpeedKmh = speed };
                shift.Clock.Advance(dt);
                var meters = speed / 3.6f * dt;
                var before = shift.Tank.Litres;
                shift.Drive(meters);
                result.LitresBurned += before - shift.Tank.Litres;
                street.Update(dt, taxi);
                var minute = shift.Clock.GameMinutes;
                street.PatienceScale = road.Weather.PatienceFactor(minute);
                street.SpawnRateScale = today.ClientsFactor * road.Weather.ClientsFactor(minute);
                if (city != null)
                {
                    var hour = minute / 60f;
                    if (!populated) { city.Populate(hour, Car(speed)); populated = true; }
                    city.Update(dt, hour, Car(speed));
                }
                result.ClientsLostToOthers += CountTaken(street);
                seconds += dt;
                if (street.DayFinished) break;

                if (!moving) { wait -= dt; continue; }
                street.Honk(taxi, out _);

                var action = street.AvailableAction(here);
                var wantsFuel = action.Kind == StreetActionKind.Refuel && shift.Tank.Fraction < player.RefuelBelow;
                if (action.Kind == StreetActionKind.Drop || action.Kind == StreetActionKind.Board
                    || action.Kind == StreetActionKind.EndDay || wantsFuel)
                {
                    if (wantsFuel)
                        while (shift.Tank.Fraction < player.RefuelUpTo && street.AvailableAction(here).Kind == StreetActionKind.Refuel)
                            street.Perform(here);
                    else street.Perform(here);
                    wait = player.StopSeconds;
                    continue;
                }

                var from = along;
                along += dir * meters;
                if (along >= routeLength) { along = routeLength; dir = -1; wait = player.UTurnSeconds; }
                else if (along <= 0f) { along = 0f; dir = 1; wait = player.UTurnSeconds; }

                // Ce qui arrive sur ce bout de route : contrôle, nids-de-poule, panne, accrochage.
                var cabin = shift.Cabin;
                var stopped = police.Update(from, along, minute, cabin.PassengerCount, cabin.Front.Passengers.Count, street.Reputation.PoliceStopFactor);
                if (stopped != null)
                {
                    result.Controls++;
                    if (stopped.Finding.Kind == ControlFindingKind.ExpiredPaper) result.ExpiredPaperControls++;
                }
                var e = road.Drive(from, along, speed);
                if (e.Punctured) result.Punctures++;
                if (e.BrokeDown) result.Breakdowns++;
                if (luck.NextDouble() < meters / 1000f * player.CrashesPerKm)
                {
                    var impact = player.CrashMinKmh + (float)luck.NextDouble() * (player.CrashMaxKmh - player.CrashMinKmh);
                    var atFault = luck.NextDouble() < player.CrashAtFaultShare;
                    if (crash.Report(street, road.Car, ++vehicleId, new CrashJudgement { ImpactKmh = impact, PlayerAtFault = atFault }, seconds).Length > 0)
                        result.Crashes++;
                }
            }

            foreach (var ex in street.Summary.Expenses)
                if (ex.Label == Street.PoliceExpenseLabel) result.PolicePaid += ex.Amount;
            result.Summary = street.Summary;
            result.Reputation = street.Reputation.Score;
            carry.Reputation = street.Reputation.Score;
            carry.TankLitres = shift.Tank.Litres;
            carry.Damage = road.Car.Damage;
            carry.DayNumber++;
            return result;
        }

        private static int CountTaken(Street street)
        {
            var n = 0;
            foreach (var l in street.Lost) if (l.Reason == ClientLostReason.TookOtherTaxi) n++;
            return n;
        }

        private static void ResolveControl(PoliceDay police, Street street, AveragePlayer player, int impoundFee)
        {
            var c = police.Current;
            while (!c.Closed)
            {
                if (c.Negotiations < player.PoliceNegotiations && c.CanNegotiate) c.Choose(ControlChoice.Negotiate);
                else c.Choose(ControlChoice.Pay);
            }
            street.ApplyPoliceControl(police.Close(), impoundFee);
        }
    }

    /// <summary>Mesures d'un réglage (moyennes sur toutes les carrières).</summary>
    public sealed class SettingResult
    {
        public BalanceSetting Setting;
        public int Careers;
        public int DaysCounted;
        /// <summary>Part des journées (tout l'horizon, renvoi ou non) où la journée seule couvre le versement (tampon du bilan).</summary>
        public double ReachedShare;
        /// <summary>Reste moyen pour le chauffeur par journée : recette − carburant acheté − frais − versement.</summary>
        public double MeanLeftover;
        /// <summary>Recette moyenne, carburant acheté, frais imprévus (par journée).</summary>
        public double MeanRevenue, MeanFuel, MeanExpenses;
        /// <summary>Part des carrières renvoyées au bout de 7 jours, et de l'horizon.</summary>
        public double FiredBy7, FiredByHorizon;
        /// <summary>Jour du renvoi : médiane (int.MaxValue si moins de la moitié des carrières est renvoyée).</summary>
        public int MedianFiredDay;
        public double MeanFiredDay;
        /// <summary>Épargne après 7 jours (ou au renvoi), moyenne et médiane.</summary>
        public double MeanSavings7;
        public int MedianSavings7;
        public int Horizon;
    }

    public static class BalanceStudy
    {
        /// <summary>Prix ×m arrondis à 50 FCFA (comme les vrais prix des taxis, ronds).</summary>
        public static FareConfig ScaleFares(FareConfig fares, float multiplier)
        {
            int R(int v) => (int)(Math.Round(v * multiplier / 50.0, MidpointRounding.AwayFromZero) * 50);
            var bands = new FareBand[fares.bands.Length];
            for (var i = 0; i < bands.Length; i++)
                bands[i] = new FareBand { maxDistanceMeters = fares.bands[i].maxDistanceMeters, fare = R(fares.bands[i].fare) };
            return new FareConfig { bands = bands, longDistanceFare = R(fares.longDistanceFare) };
        }

        private static readonly PropertyInfo VersementProperty = typeof(DaySummary).GetProperty(nameof(DaySummary.Versement));

        /// <summary>
        /// Le versement n'agit pas sur la journée elle-même (le joueur moyen joue pareil) : une même journée est close par
        /// le propriétaire avec chaque versement de la grille (bilan recopié avec ce versement).
        /// </summary>
        public static DaySummary WithVersement(DaySummary s, int versement)
        {
            VersementProperty.SetValue(s, versement);
            return s;
        }

        /// <summary>
        /// Joue <paramref name="careers"/> carrières de <paramref name="horizon"/> jours pour une consommation et des prix,
        /// et les clôt avec chaque versement. Graines fixes : carrière k, jour d → graine 1 000 × k + d. Parallèle mais
        /// déterministe (chaque carrière a ses graines, résultats rangés par index).
        /// </summary>
        public static List<SettingResult> RunCareers(GameData data, float scale, float price, int[] versements, int careers, int horizon,
            AveragePlayer player, int baseSeed = 1)
        {
            var days = new StudyDay[careers][];
            Parallel.For(0, careers, k =>
            {
                var carry = new CareerCarry();
                var list = new StudyDay[horizon];
                var s = new BalanceSetting { ConsumptionScale = scale, PriceMultiplier = price, Versement = versements[0] };
                for (var d = 0; d < horizon; d++)
                    list[d] = StudyDayPlayer.Play(data, s, player, carry, baseSeed * 1000003 + 1000 * k + d);
                days[k] = list;
            });

            var results = new List<SettingResult>();
            foreach (var v in versements)
            {
                var r = new SettingResult
                {
                    Setting = new BalanceSetting { ConsumptionScale = scale, PriceMultiplier = price, Versement = v },
                    Careers = careers,
                    Horizon = horizon,
                };
                var fired = new List<int>();
                var savings7 = new List<int>();
                int reached = 0, counted = 0, firedBy7 = 0;
                double left = 0, rev = 0, fuel = 0, exp = 0;
                for (var k = 0; k < careers; k++)
                {
                    var career = new DriverCareer(data.Owner);
                    var firedDay = int.MaxValue;
                    var saved7 = 0;
                    for (var d = 0; d < horizon; d++)
                    {
                        var s = WithVersement(days[k][d].Summary, v);
                        {
                            counted++;
                            if (s.VersementReached) reached++;
                            left += s.TowardVersement - v;
                            rev += s.Revenue; fuel += s.FuelCost; exp += s.ExpensesTotal;
                        }
                        if (firedDay == int.MaxValue)
                        {
                            career.PayFromSavings(days[k][d].RenewalCost); // papiers renouvelés au début de la journée
                            var c = career.CloseDay(s, data.Reputation.effects);
                            if (d < 7) saved7 = c.SavingsAfter;
                            if (c.Mood == OwnerMood.Fired) firedDay = d + 1;
                        }
                    }
                    if (firedDay <= 7) firedBy7++;
                    fired.Add(firedDay);
                    savings7.Add(saved7);
                }
                r.DaysCounted = counted;
                r.ReachedShare = (double)reached / counted;
                r.MeanLeftover = left / counted;
                r.MeanRevenue = rev / counted; r.MeanFuel = fuel / counted; r.MeanExpenses = exp / counted;
                r.FiredBy7 = (double)firedBy7 / careers;
                r.FiredByHorizon = fired.Count(f => f != int.MaxValue) / (double)careers;
                fired.Sort();
                r.MedianFiredDay = fired[careers / 2];
                var firedOnly = fired.Where(f => f != int.MaxValue).ToList();
                r.MeanFiredDay = firedOnly.Count > 0 ? firedOnly.Average() : 0;
                r.MeanSavings7 = savings7.Average();
                savings7.Sort();
                r.MedianSavings7 = savings7[careers / 2];
                results.Add(r);
            }
            return results;
        }

        /// <summary>Statistiques brutes des journées d'une carrière type (contrôles, incidents…), pour la méthode.</summary>
        public static StudyDay[] PlayCareer(GameData data, BalanceSetting setting, AveragePlayer player, int days, int seed)
        {
            var carry = new CareerCarry();
            var list = new StudyDay[days];
            for (var d = 0; d < days; d++) list[d] = StudyDayPlayer.Play(data, setting, player, carry, seed * 1000 + d);
            return list;
        }

        public static string Money(double v) => DaySummary.Money((int)Math.Round(v)).Replace(" FCFA", "");
        public static string Pct(double v) => $"{Math.Round(v * 100):0} %";
        public static string Num(double v, string fmt = "0.#") => v.ToString(fmt, CultureInfo.GetCultureInfo("fr-FR"));
        public static string FiredDay(SettingResult r) => r.MedianFiredDay == int.MaxValue ? $"> {r.Horizon}" : r.MedianFiredDay.ToString();

        public const string TableHeader =
            "| Conso | Prix | Versement | Versement atteint | Reste moyen / jour | Recette | Carburant acheté | Frais imprévus | Renvoyé en 7 j | Renvoyé en {H} j | Jour du renvoi (médiane) | Épargne à 7 j (moy. / méd.) |\n" +
            "|---|---|---|---|---|---|---|---|---|---|---|---|";

        public static string Row(SettingResult r) =>
            $"| ×{Num(r.Setting.ConsumptionScale)} | ×{Num(r.Setting.PriceMultiplier, "0.##")} | {Money(r.Setting.Versement)} | {Pct(r.ReachedShare)} | {Money(r.MeanLeftover)} | " +
            $"{Money(r.MeanRevenue)} | {Money(r.MeanFuel)} | {Money(r.MeanExpenses)} | {Pct(r.FiredBy7)} | {Pct(r.FiredByHorizon)} | {FiredDay(r)} | {Money(r.MeanSavings7)} / {Money(r.MedianSavings7)} |";
    }
}
