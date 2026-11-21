using System;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Save;

namespace TaxiKmer.Core.Tests
{
    public class ReputationTests
    {
        private static StopsConfig Stops() => new StopsConfig
        {
            stops = new[]
            {
                new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                new Stop { id = "b", name = "B", distanceAlongRouteMeters = 500 },
                new Stop { id = "c", name = "C", distanceAlongRouteMeters = 1000 },
            },
        };

        private static Street NewStreet(out StreetConfig cfg, int seed = 3)
        {
            cfg = new StreetConfig();
            var stops = Stops();
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            return new Street(cfg, stops, new ClientFactory(stops, FareConfig.Default(), seed), shift, 1000f)
            {
                Reputation = new DriverReputation(new ReputationConfig()),
            };
        }

        private static RoadsideClient BoardOne(Street street, string pickup, int dir)
        {
            var c = street.Waiting.First(x => x.Pickup.id == pickup && x.Direction == dir);
            var at = new TaxiOnRoute { Along = c.Along - 30f * dir, Direction = dir, Lateral = 2f * dir };
            street.Update(0.1f, at);
            street.Honk(at, out _);
            Assert.AreEqual(BoardResult.Boarded, street.Perform(new TaxiOnRoute { Along = c.Along, Direction = dir, Lateral = 2f * dir }).Board);
            return c;
        }

        [Test]
        public void Score_IsBounded_AndLabelled()
        {
            var r = new DriverReputation(new ReputationConfig());
            Assert.AreEqual(50, r.Score);
            Assert.AreEqual("correcte", r.Label);
            r.Add(500);
            Assert.AreEqual(100, r.Score);
            Assert.AreEqual("excellente", r.Label);
            r.Add(-500);
            Assert.AreEqual(0, r.Score);
            Assert.AreEqual("très mauvaise", r.Label);
            Assert.AreEqual(-50, r.ChangeToday);
        }

        [Test]
        public void Texts()
        {
            var cfg = new ReputationConfig();
            Assert.AreEqual("Réputation : bonne (62, +4 aujourd'hui)", DriverReputation.SummaryLine(cfg, 62, 4));
            Assert.AreEqual("Réputation : mauvaise (30, -2 aujourd'hui)", DriverReputation.SummaryLine(cfg, 30, -2));
            Assert.AreEqual("Réputation : correcte (50, ±0 aujourd'hui)", DriverReputation.SummaryLine(cfg, 50, 0));
            Assert.AreEqual("Réputation 50", new DriverReputation(cfg).HudLabel);
        }

        [Test]
        public void RoughDriving_WithPassengers_CostsPoints()
        {
            var r = new DriverReputation(new ReputationConfig());
            r.Drive(30f, 90f, 0);
            Assert.AreEqual(50, r.Score, "Sans passager, pas de plainte.");
            r.Drive(30f, 50f, 2);
            Assert.AreEqual(50, r.Score, "Allure raisonnable.");
            r.Drive(25f, 80f, 2);
            Assert.AreEqual(48, r.Score, "Un point toutes les 10 s.");
            r.Drive(5f, 80f, 2);
            Assert.AreEqual(47, r.Score, "Le reste s'accumule.");
        }

        [Test]
        public void PaidRide_Raises_ClientLeftWaiting_Lowers()
        {
            var street = NewStreet(out var cfg);
            BoardOne(street, "a", 1);
            var dest = street.FindStop(street.Shift.Cabin.Passengers.Single().DestinationStopId);
            street.Perform(new TaxiOnRoute { Along = dest.distanceAlongRouteMeters, Direction = 1, Lateral = 2f });
            Assert.AreEqual(51, street.Reputation.Score);

            var c = street.Waiting.First(x => x.Pickup.id == "b" && x.Direction == 1);
            var at = new TaxiOnRoute { Along = c.Along - 30f, Direction = 1, Lateral = 2f };
            street.Update(0.1f, at);
            Assert.AreNotEqual(HonkResult.NobodyCalling, street.Honk(at, out _));
            street.Update(cfg.acceptedPatienceSeconds + 1f, new TaxiOnRoute { Along = c.Along - 20f, Direction = 1, Lateral = 2f });
            Assert.AreEqual(49, street.Reputation.Score, "Client accepté puis laissé à attendre.");
        }

        [Test]
        public void Accident_AndEndingWithPassengers_Lower()
        {
            var street = NewStreet(out _);
            BoardOne(street, "a", 1);
            BoardOne(street, "a", 1);
            street.PedestrianHit(new AccidentConfig(), 15f, new Random(1)); // blessé léger
            Assert.AreEqual(50 - 10 - 2 * 2, street.Reputation.Score);

            var other = NewStreet(out _);
            BoardOne(other, "a", 1);
            other.EndDayNow();
            Assert.AreEqual(48, other.Reputation.Score);
            Assert.AreEqual(48, other.Summary.Reputation);
            Assert.AreEqual(-2, other.Summary.ReputationChange);
        }

        [Test]
        public void Reputation_IsSaved_MidDay_AndCarriedToTheNextDay()
        {
            var street = NewStreet(out _);
            street.Reputation.Add(7);
            var data = new SaveData();
            SaveSnapshot.CaptureDay(data, street, 10f, 1, 3);
            var fresh = NewStreet(out _);
            SaveSnapshot.RestoreDay(data.day, fresh);
            Assert.AreEqual(57, fresh.Reputation.Score);
            Assert.AreEqual(7, fresh.Reputation.ChangeToday);

            fresh.EndDayNow();
            var career = SaveSnapshot.LoadCareer(data, new OwnerConfig());
            SaveSnapshot.RecordDay(data, fresh.Summary, career.CloseDay(fresh.Summary), career);
            Assert.AreEqual(57, data.career.reputation);
            Assert.AreEqual(7, data.career.history.Single().reputationChange);
            var next = SaveSnapshot.LoadReputation(data, new ReputationConfig());
            Assert.AreEqual(57, next.Score);
            Assert.AreEqual(0, next.ChangeToday);
            Assert.AreEqual(50, SaveSnapshot.LoadReputation(new SaveData(), new ReputationConfig()).Score, "Nouvelle carrière : réputation de départ.");
        }

        [Test]
        public void Version3File_ReputationUnknown_StartsAtTheStartValue()
        {
            var payload = "{\"version\":3,\"career\":{\"dayNumber\":4,\"reputation\":0,\"fuelLitres\":20}}";
            var storage = new MemoryStorage();
            storage.Files[SaveStore.MainFile] = SaveStoreTests.Serializer.ToJson(new SaveEnvelope { version = 3, checksum = SaveStore.Checksum(payload), payload = payload });
            var r = new SaveStore(storage, SaveStoreTests.Serializer).Load();
            Assert.AreEqual(LoadStatus.Loaded, r.Status);
            Assert.AreEqual(SaveMigrations.CurrentVersion, r.Data.version);
            Assert.AreEqual(-1, r.Data.career.reputation);
            Assert.AreEqual(20f, r.Data.career.fuelLitres, "Le réservoir de la version 3 est gardé.");
            Assert.AreEqual(50, SaveSnapshot.LoadReputation(r.Data, new ReputationConfig()).Score);
        }

        // Effets de la réputation (décision de Kevin du 21/11) : patience, prix acceptés, propriétaire, police.

        [TestCase(50, 1f, 1f, 1f, 0)]
        [TestCase(100, 1.5f, 0.4f, 0.7f, 1)]
        [TestCase(0, 0.5f, 1.6f, 1.3f, -1)]
        [TestCase(70, 1.2f, 0.76f, 0.88f, 0)]
        public void Effects_FollowTheScore_Bounded(int score, float patience, float haggle, float police, int owner)
        {
            var e = new ReputationEffectsConfig();
            Assert.AreEqual(patience, ReputationEffects.Patience(e, score), 1e-4);
            Assert.AreEqual(haggle, ReputationEffects.Haggle(e, score), 1e-4);
            Assert.AreEqual(police, ReputationEffects.PoliceStop(e, score), 1e-4);
            Assert.AreEqual(owner, ReputationEffects.OwnerTolerance(e, score));
            var r = new DriverReputation(new ReputationConfig(), score);
            Assert.AreEqual(patience, r.PatienceFactor, 1e-4);
            Assert.AreEqual(owner, r.OwnerTolerance);
        }

        [Test]
        public void Effects_Off_ChangeNothing()
        {
            var e = new ReputationEffectsConfig { enabled = false };
            Assert.AreEqual(1f, ReputationEffects.Patience(e, 0));
            Assert.AreEqual(1f, ReputationEffects.Haggle(e, 0));
            Assert.AreEqual(1f, ReputationEffects.PoliceStop(e, 100));
            Assert.AreEqual(0, ReputationEffects.OwnerTolerance(e, 100));
            Assert.AreEqual(1f, ReputationEffects.Patience(null, 0));
        }

        [Test]
        public void GoodReputation_ClientsWaitLonger_BadOnesGiveUpSooner()
        {
            foreach (var (score, gaveUp) in new[] { (100, false), (0, true) })
            {
                var street = NewStreet(out var cfg);
                street.Reputation.Restore(score, score);
                var c = street.Waiting.First(x => x.Pickup.id == "b" && x.Direction == 1);
                var at = new TaxiOnRoute { Along = c.Along - 30f, Direction = 1, Lateral = 2f };
                street.Update(0.1f, at);
                Assert.AreEqual(HonkResult.Accepted, street.Honk(at, out _));
                var lost = false;
                for (var t = 0f; t < cfg.acceptedPatienceSeconds; t += 0.5f)
                {
                    street.Update(0.5f, at);
                    lost |= street.Lost.Any(l => l.Client == c && l.Reason == ClientLostReason.GaveUp);
                }
                Assert.AreEqual(gaveUp, lost, $"score {score}");
                Assert.AreEqual(cfg.acceptedPatienceSeconds * (score == 100 ? 1.5f : 0.5f), street.AcceptedPatienceSeconds, 1e-3);
            }
        }

        [Test]
        public void OwnerAttitude_DependsOnReputation()
        {
            var effects = new ReputationEffectsConfig();
            var owner = new OwnerConfig(); // 3 manques sur 7 jours
            foreach (var (score, firedOn) in new[] { (50, 3), (90, 4), (10, 2) })
            {
                var career = new DriverCareer(new OwnerConfig { payShortfallFromSavings = false });
                var day = 0;
                while (!career.Fired && day < 10)
                {
                    day++;
                    var ledger = new DayLedger(new DayConfig { versement = 10000 });
                    var s = DaySummary.From(ledger, 0, 0, reputation: score, reputationChange: 0);
                    career.CloseDay(s, effects);
                }
                Assert.AreEqual(firedOn, day, $"réputation {score}");
            }
            Assert.AreEqual(owner.missesBeforeFired, new DriverCareer(owner).MissesBeforeFired(null, effects));
        }
    }
}
