using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Save;

namespace TaxiKmer.Core.Tests
{
    /// <summary>Disque en mémoire qui peut « couper le courant » après N opérations d'écriture.</summary>
    public sealed class MemoryStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        /// <summary>Nombre d'opérations d'écriture permises avant la coupure (-1 : jamais).</summary>
        public int FailAfter = -1;
        public int Operations;

        private void Tick()
        {
            if (FailAfter >= 0 && Operations >= FailAfter) throw new InvalidOperationException("coupure simulée");
            Operations++;
        }

        public bool Exists(string name) => Files.ContainsKey(name);
        public string Read(string name) => Files[name];
        public void Write(string name, string text) { Tick(); Files[name] = text; }
        public void Copy(string source, string destination) { Tick(); Files[destination] = Files[source]; }
        public void Move(string source, string destination)
        {
            Tick();
            if (Files.ContainsKey(destination)) throw new InvalidOperationException("destination existante");
            Files[destination] = Files[source];
            Files.Remove(source);
        }
        public void Delete(string name) { Tick(); Files.Remove(name); }
    }

    public class SaveStoreTests
    {
        internal static ISaveSerializer Serializer => TestSaveSerializer.Instance;

        private static SaveData Sample(int day = 3, int revenue = 4200)
        {
            var d = new SaveData { gameVersion = "0.1.0" };
            d.career.dayNumber = day;
            d.hasDayInProgress = true;
            d.day.revenue = revenue;
            d.day.elapsedRealSeconds = 123f;
            d.day.passengers = new[] { new PassengerData { id = "c7", destinationStopId = "b", proposedFare = 400, corpulence = 2, bench = 0 } };
            return d;
        }

        [Test]
        public void NoSave_ThenSaveAndLoad_RoundTrip()
        {
            var storage = new MemoryStorage();
            var store = new SaveStore(storage, Serializer);
            Assert.AreEqual(LoadStatus.NoSave, store.Load().Status);
            Assert.IsTrue(store.Save(Sample(), "2026-11-15T10:00:00Z"));
            var r = new SaveStore(storage, Serializer).Load();
            Assert.AreEqual(LoadStatus.Loaded, r.Status);
            Assert.AreEqual(3, r.Data.career.dayNumber);
            Assert.AreEqual(4200, r.Data.day.revenue);
            Assert.AreEqual("c7", r.Data.day.passengers.Single().id);
            Assert.AreEqual("2026-11-15T10:00:00Z", r.Data.savedAtUtc);
            Assert.IsFalse(storage.Exists(SaveStore.TempFile));
        }

        [Test]
        public void SecondSave_KeepsPreviousAsBackup()
        {
            var storage = new MemoryStorage();
            var store = new SaveStore(storage, Serializer);
            store.Save(Sample(revenue: 100));
            store.Save(Sample(revenue: 200));
            Assert.IsTrue(storage.Exists(SaveStore.BackupFile));
            var backup = Serializer.FromJson<SaveEnvelope>(storage.Files[SaveStore.BackupFile]);
            Assert.AreEqual(100, Serializer.FromJson<SaveData>(backup.payload).day.revenue);
        }

        [Test]
        public void PowerCut_AtEveryStepOfASave_NeverLosesEverything()
        {
            // Une sauvegarde fait jusqu'à 4 écritures (temporaire, copie de secours, effacement, remplacement).
            for (var cut = 0; cut <= 4; cut++)
            {
                var storage = new MemoryStorage();
                new SaveStore(storage, Serializer).Save(Sample(revenue: 100));
                storage.FailAfter = storage.Operations + cut;
                try { new SaveStore(storage, Serializer).Save(Sample(revenue: 200)); }
                catch (InvalidOperationException) { }
                storage.FailAfter = -1;

                var r = new SaveStore(storage, Serializer).Load();
                Assert.IsNotNull(r.Data, $"coupure après {cut} écritures : {r.Status} {r.Detail}");
                Assert.That(r.Data.day.revenue, Is.EqualTo(100).Or.EqualTo(200), $"coupure après {cut} écritures");
                // Après la reprise, le fichier principal est sain et les sauvegardes suivantes marchent.
                Assert.IsTrue(new SaveStore(storage, Serializer).Save(Sample(revenue: 300)));
                Assert.AreEqual(300, new SaveStore(storage, Serializer).Load().Data.day.revenue);
            }
        }

        [Test]
        public void TruncatedOrTamperedFile_RecoversFromBackup_AndKeepsTheBadFile()
        {
            foreach (var damage in new Func<string, string>[]
            {
                s => s.Substring(0, s.Length / 2),          // fichier tronqué
                s => s.Replace("4200", "9999"),              // contenu modifié : empreinte fausse
                s => "",                                      // fichier vide
                s => "\u0000\u0001 n'importe quoi",           // octets illisibles
            })
            {
                var storage = new MemoryStorage();
                var store = new SaveStore(storage, Serializer);
                store.Save(Sample(revenue: 100));
                store.Save(Sample(revenue: 4200));
                storage.Files[SaveStore.MainFile] = damage(storage.Files[SaveStore.MainFile]);

                var r = new SaveStore(storage, Serializer).Load();
                Assert.AreEqual(LoadStatus.RecoveredFromBackup, r.Status);
                Assert.AreEqual(100, r.Data.day.revenue);
                Assert.IsTrue(storage.Files.Keys.Any(k => k.StartsWith("partie-abimee-")), "Le fichier abîmé est gardé à part.");
                Assert.AreEqual(LoadStatus.Loaded, new SaveStore(storage, Serializer).Load().Status, "Le principal est réparé.");
            }
        }

        [Test]
        public void CorruptWithoutBackup_StartsFresh_ButKeepsTheFile()
        {
            var storage = new MemoryStorage();
            storage.Files[SaveStore.MainFile] = "{ pas du json";
            var store = new SaveStore(storage, Serializer);
            var r = store.Load();
            Assert.AreEqual(LoadStatus.Corrupted, r.Status);
            Assert.IsNull(r.Data);
            Assert.IsTrue(storage.Files.ContainsKey("partie-abimee-1.json"));
            Assert.IsTrue(store.Save(Sample()), "On peut repartir sur une nouvelle partie.");
        }

        [Test]
        public void SaveFromNewerGame_IsNeverOverwritten()
        {
            var storage = new MemoryStorage();
            var payload = Serializer.ToJson(Sample());
            var newer = new SaveEnvelope { version = SaveMigrations.CurrentVersion + 1, checksum = SaveStore.Checksum(payload), payload = payload };
            storage.Files[SaveStore.MainFile] = Serializer.ToJson(newer);
            var before = storage.Files[SaveStore.MainFile];

            var store = new SaveStore(storage, Serializer);
            var r = store.Load();
            Assert.AreEqual(LoadStatus.TooNew, r.Status);
            Assert.IsNull(r.Data);
            Assert.IsFalse(store.Save(Sample(revenue: 1)), "Refuse d'écraser.");
            Assert.AreEqual(before, storage.Files[SaveStore.MainFile]);
        }

        [Test]
        public void OlderFile_MissingFields_GetsDefaults_AndIsRepaired()
        {
            // Fichier « version 0 » minimal : ni historique, ni journée, numéro de jour invalide.
            var payload = "{\"version\":0,\"career\":{\"dayNumber\":0}}";
            var storage = new MemoryStorage();
            storage.Files[SaveStore.MainFile] = Serializer.ToJson(new SaveEnvelope { version = 0, checksum = SaveStore.Checksum(payload), payload = payload });
            var r = new SaveStore(storage, Serializer).Load();
            Assert.AreEqual(LoadStatus.Loaded, r.Status);
            Assert.AreEqual(SaveMigrations.CurrentVersion, r.Data.version);
            Assert.AreEqual(1, r.Data.career.dayNumber);
            Assert.IsNotNull(r.Data.career.missedVersementDays);
            Assert.IsNotNull(r.Data.career.history);
            Assert.IsNotNull(r.Data.day.passengers);
            Assert.IsFalse(r.Data.hasDayInProgress);
        }

        [Test]
        public void Version1File_UpgradesToCurrentVersion()
        {
            // Sauvegarde écrite par la version 1 : un bilan, pas encore d'épargne ni de versements manqués.
            var payload = "{\"version\":1,\"career\":{\"dayNumber\":2,\"history\":[{\"dayNumber\":1,\"revenue\":12000,\"versement\":10000,\"remaining\":1500}]}}";
            var storage = new MemoryStorage();
            storage.Files[SaveStore.MainFile] = Serializer.ToJson(new SaveEnvelope { version = 1, checksum = SaveStore.Checksum(payload), payload = payload });
            var r = new SaveStore(storage, Serializer).Load();
            Assert.AreEqual(LoadStatus.Loaded, r.Status);
            Assert.AreEqual(SaveMigrations.CurrentVersion, r.Data.version);
            Assert.AreEqual(2, r.Data.career.dayNumber);
            Assert.AreEqual(0, r.Data.career.savings, "L'argent ne passait pas d'un jour à l'autre en version 1.");
            Assert.AreEqual(10000, r.Data.career.history.Single().versementPaid, "Ancien bilan : versement compté comme payé.");
            Assert.IsEmpty(r.Data.career.missedVersementDays);
        }

        [Test]
        public void Version4File_UpgradesTo5_NoImpound_NoDamage()
        {
            // Version 5 (décisions de Kevin du 21/11) : fourrières comptées, dégâts du taxi qui passent d'un jour à l'autre.
            var payload = "{\"version\":4,\"career\":{\"dayNumber\":3,\"impounds\":7,\"vehicleDamage\":-4}}";
            var storage = new MemoryStorage();
            storage.Files[SaveStore.MainFile] = Serializer.ToJson(new SaveEnvelope { version = 4, checksum = SaveStore.Checksum(payload), payload = payload });
            var r = new SaveStore(storage, Serializer).Load();
            Assert.AreEqual(LoadStatus.Loaded, r.Status);
            Assert.AreEqual(5, SaveMigrations.CurrentVersion);
            Assert.AreEqual(0, r.Data.career.impounds, "Avant la version 5 : aucune fourrière comptée.");
            Assert.AreEqual(0f, r.Data.career.vehicleDamage);
            Assert.AreEqual(3, r.Data.career.dayNumber);
        }

        [Test]
        public void Version2File_TankUnknown_StartsWithTheStartingTank()
        {
            // Version 2 : pas de réservoir ; le champ absent ne doit pas devenir « réservoir vide ».
            var payload = "{\"version\":2,\"career\":{\"dayNumber\":3,\"savings\":5000,\"fuelLitres\":0},\"hasDayInProgress\":true,\"day\":{\"revenue\":900,\"tankLitres\":0}}";
            var storage = new MemoryStorage();
            storage.Files[SaveStore.MainFile] = Serializer.ToJson(new SaveEnvelope { version = 2, checksum = SaveStore.Checksum(payload), payload = payload });
            var r = new SaveStore(storage, Serializer).Load();
            Assert.AreEqual(LoadStatus.Loaded, r.Status);
            Assert.AreEqual(-1f, r.Data.career.fuelLitres);
            Assert.AreEqual(-1f, r.Data.day.tankLitres);
            var fuel = new TaxiKmer.Core.Fuel.FuelConfig();
            Assert.AreEqual(fuel.startLitres, SaveSnapshot.TankAtDayStart(r.Data, fuel));
            Assert.AreEqual(fuel.startLitres, SaveSnapshot.TankAtDayStart(new SaveData(), fuel), "Nouvelle carrière : réservoir de départ.");
        }

        [Test]
        public void Checksum_IsStableAndSensitive()
        {
            Assert.AreEqual("cbf29ce484222325", SaveStore.Checksum(""), "FNV-1a 64 de la chaîne vide.");
            Assert.AreNotEqual(SaveStore.Checksum("taxi"), SaveStore.Checksum("taxI"));
            Assert.AreEqual(16, SaveStore.Checksum("Étoudi").Length);
        }

        [Test]
        public void DeleteSave_KeepsABackup()
        {
            var storage = new MemoryStorage();
            var store = new SaveStore(storage, Serializer);
            store.Save(Sample());
            store.DeleteSave();
            Assert.AreEqual(LoadStatus.RecoveredFromBackup, new SaveStore(storage, Serializer).Load().Status,
                "Sans fichier principal, la copie de secours est reprise (filet de sécurité).");
        }
    }

    public class SaveSnapshotTests
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

        private static Street NewStreet(int seed = 3)
        {
            var stops = Stops();
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            return new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), seed), shift, 1000f);
        }

        private static void BoardOne(Street street, string pickup, int dir)
        {
            var c = street.Waiting.First(x => x.Pickup.id == pickup && x.Direction == dir);
            var at = new TaxiOnRoute { Along = c.Along - 30f * dir, Direction = dir, Lateral = 2f * dir };
            street.Update(0.1f, at);
            street.Honk(at, out _);
            Assert.AreEqual(BoardResult.Boarded, street.Perform(new TaxiOnRoute { Along = c.Along, Direction = dir, Lateral = 2f * dir }).Board);
        }

        [Test]
        public void DayInProgress_SurvivesSaveAndReload()
        {
            var street = NewStreet();
            BoardOne(street, "b", 1);
            BoardOne(street, "b", 1);
            street.Shift.Clock.Advance(200f);
            street.Shift.Ledger.AddFare(500);
            street.Shift.Ledger.AddDistance(3200f);
            street.Shift.Ledger.AddExpense("Premiers soins d'un piéton renversé", 12000);
            street.RestoreLeftUnpaid(1, 300);

            var data = new SaveData();
            SaveSnapshot.CaptureDay(data, street, 742f, -1, 3);
            var storage = new MemoryStorage();
            new SaveStore(storage, SaveStoreTests.Serializer).Save(data);
            var loaded = new SaveStore(storage, SaveStoreTests.Serializer).Load().Data;

            Assert.IsTrue(loaded.hasDayInProgress);
            Assert.AreEqual(742f, loaded.day.taxiAlong);
            Assert.AreEqual(-1, loaded.day.taxiDirection);
            var fresh = NewStreet();
            Assert.AreEqual(0, SaveSnapshot.RestoreDay(loaded.day, fresh));
            var a = street.Shift;
            var b = fresh.Shift;
            Assert.AreEqual(a.Clock.GameMinutes, b.Clock.GameMinutes);
            Assert.AreEqual(a.Ledger.Revenue, b.Ledger.Revenue);
            Assert.AreEqual(a.Ledger.RideCount, b.Ledger.RideCount);
            Assert.AreEqual(a.Ledger.MetersDriven, b.Ledger.MetersDriven, 1e-3f);
            Assert.AreEqual(a.Ledger.Remaining, b.Ledger.Remaining);
            Assert.AreEqual(a.Cabin.Passengers.Select(p => (p.Id, p.DestinationStopId, p.ProposedFare, p.Corpulence)),
                            b.Cabin.Passengers.Select(p => (p.Id, p.DestinationStopId, p.ProposedFare, p.Corpulence)));
            foreach (var p in a.Cabin.Passengers)
                Assert.AreEqual(a.Cabin.BenchOf(p).Side, b.Cabin.BenchOf(b.Cabin.Find(p.Id)).Side, "Même banquette.");
            Assert.AreEqual(1, fresh.LeftUnpaid);

            // Les passagers remis paient toujours à leur destination.
            var dest = fresh.FindStop(b.Cabin.Passengers.First().DestinationStopId);
            var paid = fresh.Perform(new TaxiOnRoute { Along = dest.distanceAlongRouteMeters, Direction = 1, Lateral = 2f }).Paid;
            Assert.Greater(paid, 0);
        }

        [Test]
        public void Restore_SkipsPassengersWithUnknownDestination()
        {
            var day = new DayProgressData
            {
                passengers = new[]
                {
                    new PassengerData { id = "x", destinationStopId = "lieu-supprime", proposedFare = 300, corpulence = 1 },
                    new PassengerData { id = "y", destinationStopId = "c", proposedFare = 400, corpulence = 1 },
                },
            };
            var street = NewStreet();
            Assert.AreEqual(1, SaveSnapshot.RestoreDay(day, street));
            Assert.AreEqual("y", street.Shift.Cabin.Passengers.Single().Id);
        }

        [Test]
        public void RecordDay_AppendsSummary_AndAdvancesTheDay()
        {
            var street = NewStreet();
            street.Shift.Ledger.AddFare(800);
            street.PedestrianHit(new AccidentConfig { seriousArrestChance = 1f, seizureChance = 1f }, 60f, new Random(1));
            Assert.IsTrue(street.DayFinished);
            var data = new SaveData();
            data.career.dayNumber = 4;
            data.career.savings = 20000;
            data.hasDayInProgress = true;
            var career = SaveSnapshot.LoadCareer(data, new TaxiKmer.Core.Career.OwnerConfig());
            var closure = career.CloseDay(street.Summary);
            SaveSnapshot.RecordDay(data, street.Summary, closure, career);
            Assert.AreEqual(5, data.career.dayNumber);
            Assert.IsFalse(data.hasDayInProgress);
            var rec = data.career.history.Single();
            Assert.AreEqual(4, rec.dayNumber);
            Assert.AreEqual(800, rec.revenue);
            Assert.IsTrue(rec.arrested);
            Assert.IsTrue(rec.vehicleSeized);
            Assert.AreEqual(closure.SavingsAfter, data.career.savings);
            Assert.AreEqual(rec.savingsAfter, data.career.savings);
            Assert.IsTrue(data.career.fired, "Taxi saisi : le propriétaire le renvoie (choix provisoire).");
        }
    }
}
