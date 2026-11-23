// Sauvegardes de référence v1 à v6 (idée n° 35, dotnet seulement : fichiers de tools/dotnet/SaveFixtures).
using System.IO;
using NUnit.Framework;
using TaxiKmer.Core.Save;
using TaxiKmer.Core.Tests;

namespace TaxiKmer.Core.DotnetTests
{
    public class SaveFixturesTests
    {
        private static SaveData LoadVersion(int v, out LoadResult result)
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "SaveFixtures", $"partie-v{v}.json");
            var storage = new MemoryStorage();
            storage.Files[SaveStore.MainFile] = File.ReadAllText(path);
            result = new SaveStore(storage, TestSaveSerializer.Instance).Load();
            return result.Data;
        }

        [Test]
        public void EveryVersion_LoadsAndReachesTheCurrentOne_KeepingTheDayInProgress([Values(1, 2, 3, 4, 5, 6)] int v)
        {
            var d = LoadVersion(v, out var r);
            Assert.AreEqual(LoadStatus.Loaded, r.Status, r.Detail);
            Assert.AreEqual(SaveMigrations.CurrentVersion, d.version);
            Assert.AreEqual(9, d.career.dayNumber);
            Assert.AreEqual(2, d.career.history.Length);
            Assert.IsTrue(d.hasDayInProgress);
            Assert.AreEqual(3600, d.day.revenue);
            Assert.AreEqual("emana", d.day.passengers[0].destinationStopId);
            Assert.AreEqual(1000, d.day.expenses[0].amount);
            Assert.AreEqual(7000, d.career.history[0].versementPaid, "versement payé (reporté depuis la v1)");
            Assert.IsNotNull(d.career.papers);
            Assert.IsNotNull(d.career.missedVersementDays);
        }

        [Test]
        public void V1_SavingsStartAtZero_FuelReputationPapersUnknown()
        {
            var d = LoadVersion(1, out _);
            Assert.AreEqual(0, d.career.savings);
            Assert.AreEqual(-1f, d.career.fuelLitres, "réservoir de départ");
            Assert.AreEqual(-1f, d.day.tankLitres);
            Assert.AreEqual(-1, d.career.reputation, "réputation de départ");
            Assert.AreEqual(0, d.career.impounds);
            Assert.AreEqual(0f, d.career.vehicleDamage);
            CollectionAssert.IsEmpty(d.career.papers);
        }

        [Test]
        public void V2_KeepsSavingsAndMissedDays_FuelUnknown()
        {
            var d = LoadVersion(2, out _);
            Assert.AreEqual(12000, d.career.savings);
            CollectionAssert.AreEqual(new[] { 4 }, d.career.missedVersementDays);
            Assert.AreEqual(-1f, d.career.fuelLitres);
            Assert.AreEqual(0, d.day.fuelBoughtCost);
        }

        [Test]
        public void V3_KeepsFuel_ReputationUnknown()
        {
            var d = LoadVersion(3, out _);
            Assert.AreEqual(21.5f, d.career.fuelLitres);
            Assert.AreEqual(18f, d.day.tankLitres);
            Assert.AreEqual(8400, d.day.fuelBoughtCost);
            Assert.AreEqual(-1, d.career.reputation);
            Assert.AreEqual(-1, d.career.history[1].reputation);
        }

        [Test]
        public void V4_KeepsReputation_NoImpoundYet()
        {
            var d = LoadVersion(4, out _);
            Assert.AreEqual(63, d.career.reputation);
            Assert.AreEqual(64, d.day.reputation);
            Assert.AreEqual(61, d.career.history[0].reputation);
            Assert.AreEqual(0, d.career.impounds);
        }

        [Test]
        public void V5_KeepsImpounds_PapersFromTheStart()
        {
            var d = LoadVersion(5, out _);
            Assert.AreEqual(1, d.career.impounds);
            Assert.AreEqual(0f, d.career.vehicleDamage, "pas de dégâts dans ce format");
            CollectionAssert.IsEmpty(d.career.papers);
        }

        [Test]
        public void V6_KeepsEverything()
        {
            var d = LoadVersion(6, out _);
            Assert.AreEqual(35f, d.career.vehicleDamage);
            Assert.AreEqual(1, d.career.papers.Length);
            Assert.AreEqual("assurance", d.career.papers[0].id);
            Assert.AreEqual(40, d.career.papers[0].lastValidDay);
            Assert.AreEqual(1, d.career.impounds);
            CollectionAssert.IsEmpty(d.career.stories, "histoires de passagers : champ ajouté sans version (idée n° 13)");
        }
    }
}
