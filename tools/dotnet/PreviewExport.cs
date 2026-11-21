// Export d'une partie simulée (vue du ciel) pour l'aperçu web : lancé à la main seulement.
//   dotnet test tools/dotnet --filter PreviewExport   →  build/preview/replay.json
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.DotnetTests
{
    [Explicit("Export manuel de l'aperçu web")]
    public class PreviewExport
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { IncludeFields = true };
        private static T Load<T>(string name) =>
            JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", name)), Opts);

        private static string F(float v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);

        [Test]
        public void ExportRushHourAroundMessassi()
        {
            var world = Load<WorldData>("world_messassi_etoudi.json");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            var shift = new Shift(Load<DayConfig>("day.json"), Load<SeatingConfig>("seating.json"));
            var route = new TrafficPath(world.route.xz);
            var street = new Street(Load<StreetConfig>("street.json"), stops, new ClientFactory(stops, FareConfig.Default(), 9), shift, route.Length);
            var city = new CityLife(Load<CityConfig>("traffic.json"), world, street, 9, 5.5f);

            // Le joueur : part à 250 m, roule à 28 km/h sur la voie de droite, s'arrête 20 s au marché de Messassi (631 m).
            const float dt = 1f / 30f, hour = 17.5f, duration = 150f, frameEvery = 0.25f;
            float along = 250f, stopTimer = 0f;
            PlayerCar Player(float a, float speed)
            {
                route.Sample(a, 1.7f, out var x, out var z, out var dx, out var dz);
                return new PlayerCar { X = x, Z = z, DirX = dx, DirZ = dz, Speed = speed, Length = 4.4f, Width = 1.72f };
            }

            var frames = new StringBuilder();
            var minX = float.MaxValue; var maxX = float.MinValue; var minZ = float.MaxValue; var maxZ = float.MinValue;
            var events = new List<string>();
            city.Populate(hour, Player(along, 0f));
            var nextFrame = 0f;
            for (var t = 0f; t < duration; t += dt)
            {
                var speed = 7.8f;
                if (along >= 600f && stopTimer < 20f) { speed = 0f; stopTimer += dt; }
                along += speed * dt;
                var player = Player(along, speed);
                street.Update(dt, new TaxiOnRoute { Along = along, Direction = 1, Lateral = 1.7f, SpeedKmh = speed * 3.6f });
                city.Update(dt, hour, player);
                foreach (var l in street.Lost)
                    if (l.Reason == ClientLostReason.TookOtherTaxi)
                        events.Add($"[{F(t)},\"{(l.ByMoto ? "Un bendskin" : "Un taxi")} prend un client pour {street.FindStop(l.Client.Client.DestinationStopId).name}\"]");
                if (t < nextFrame) continue;
                nextFrame += frameEvery;

                minX = Math.Min(minX, player.X); maxX = Math.Max(maxX, player.X);
                minZ = Math.Min(minZ, player.Z); maxZ = Math.Max(maxZ, player.Z);
                var near = 260f;
                bool Near(float x, float z) => Math.Abs(x - player.X) < near && Math.Abs(z - player.Z) < near;

                frames.Append(frames.Length == 0 ? "" : ",").Append("{\"t\":").Append(F(t))
                    .Append(",\"p\":[").Append(F(player.X)).Append(',').Append(F(player.Z)).Append(',').Append(F(player.DirX * 100)).Append(',').Append(F(player.DirZ * 100)).Append(']');
                frames.Append(",\"v\":[");
                var first = true;
                foreach (var v in city.Traffic.Vehicles)
                {
                    if (!Near(v.X, v.Z)) continue;
                    var k = v.Kind == VehicleKind.Taxi ? 1 : v.Kind == VehicleKind.Moto ? 2 : 0;
                    var st = v.State == VehicleState.Boarding || v.State == VehicleState.StoppingForClient ? 1 : v.OnSideRoad ? 2 : v.State == VehicleState.Overtaking ? 3 : 0;
                    frames.Append(first ? "" : ",").Append('[').Append(F(v.X)).Append(',').Append(F(v.Z)).Append(',')
                        .Append(F(v.DirX * 100)).Append(',').Append(F(v.DirZ * 100)).Append(',').Append(k).Append(',').Append(st).Append(',').Append(v.Id % 8).Append(']');
                    first = false;
                }
                frames.Append("],\"h\":[");
                first = true;
                foreach (var p in city.Pedestrians.People)
                {
                    if (!Near(p.X, p.Z)) continue;
                    frames.Append(first ? "" : ",").Append('[').Append(F(p.X)).Append(',').Append(F(p.Z)).Append(',').Append(p.State == PedestrianState.Crossing ? 1 : 0).Append(']');
                    first = false;
                }
                frames.Append("],\"c\":[");
                first = true;
                foreach (var c in street.Waiting)
                {
                    var pl = city.Place(c);
                    if (!Near(pl.X, pl.Z)) continue;
                    frames.Append(first ? "" : ",").Append('[').Append(F(pl.X)).Append(',').Append(F(pl.Z)).Append(']');
                    first = false;
                }
                frames.Append("],\"l\":[");
                frames.Append(string.Join(",", world.traffic.lights.Select(l => ((int)city.Traffic.LightFor(l.id)).ToString())));
                frames.Append("]}");
            }

            // Carte : routes et bâtiments dans la zone parcourue (+ 300 m).
            var pad = 300f;
            bool InBox(float[] xz)
            {
                for (var i = 0; i < xz.Length; i += 2)
                    if (xz[i] > minX - pad && xz[i] < maxX + pad && xz[i + 1] > minZ - pad && xz[i + 1] < maxZ + pad) return true;
                return false;
            }
            string Poly(float[] xz) => "[" + string.Join(",", xz.Select(F)) + "]";
            var roads = world.roads.Where(r => InBox(r.xz)).Select(r => $"[{F(r.width)},{(r.highway.StartsWith("trunk") || r.highway.StartsWith("secondary") ? 1 : 0)},{Poly(r.xz)}]");
            var buildings = world.buildings.Where(b => InBox(b.xz)).Select(b => Poly(b.xz));
            var lights = world.traffic.lights.Select(l => $"[{F(l.x)},{F(l.z)},\"{l.name}\"]");
            var stopsJson = stops.stops.Select(s =>
            {
                route.Sample(s.distanceAlongRouteMeters, 0f, out var x, out var z, out _, out _);
                return $"[{F(x)},{F(z)},\"{s.name}\"]";
            });

            var json = $"{{\"hour\":{F(hour)},\"frameEvery\":{F(frameEvery)},\"roads\":[{string.Join(",", roads)}],\"buildings\":[{string.Join(",", buildings)}],\"lights\":[{string.Join(",", lights)}],\"stops\":[{string.Join(",", stopsJson)}],\"events\":[{string.Join(",", events)}],\"frames\":[{frames}]}}";
            var outDir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../build/preview"));
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "replay.json"), json);
            TestContext.WriteLine($"{outDir}/replay.json : {json.Length / 1024} Ko, {events.Count} clients pris par la concurrence");
        }
    }
}
