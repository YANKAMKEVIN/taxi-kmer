using System;
using System.Text;
using NUnit.Framework;
using TaxiKmer.Core.Save;

namespace TaxiKmer.Core.Tests
{
    /// <summary>
    /// Sauvegardes abîmées au hasard (idée n° 34) : des milliers de fichiers tronqués, modifiés, vidés ou remplacés ;
    /// SaveStore relit la copie saine sans planter et ne perd jamais plus d'une journée.
    /// </summary>
    public class SaveFuzzTests
    {
        private static SaveData Day(int n)
        {
            var d = new SaveData { gameVersion = "0.1.0" };
            d.career.dayNumber = n;
            d.career.savings = 1000 * n;
            d.career.history = new[] { new DayRecordData { dayNumber = n - 1, revenue = 9000 } };
            return d;
        }

        private static string Damage(string text, Random r, out string how)
        {
            switch (r.Next(6))
            {
                case 0:
                    how = "tronqué";
                    return text.Substring(0, r.Next(text.Length));
                case 1:
                {
                    how = "caractère changé";
                    var chars = text.ToCharArray();
                    var i = r.Next(chars.Length);
                    var c = (char)r.Next(32, 127);
                    if (c == chars[i]) c = c == 'x' ? 'y' : 'x';
                    chars[i] = c;
                    return new string(chars);
                }
                case 2:
                    how = "vidé";
                    return "";
                case 3:
                {
                    how = "octets au hasard";
                    var sb = new StringBuilder();
                    for (var i = 0; i < r.Next(1, 200); i++) sb.Append((char)r.Next(0, 256));
                    return sb.ToString();
                }
                case 4:
                {
                    how = "morceau effacé";
                    var start = r.Next(text.Length);
                    var length = Math.Min(text.Length - start, r.Next(1, 40));
                    return text.Remove(start, length);
                }
                default:
                {
                    how = "chiffre changé";
                    var chars = text.ToCharArray();
                    for (var tries = 0; tries < 50; tries++)
                    {
                        var i = r.Next(chars.Length);
                        if (!char.IsDigit(chars[i])) continue;
                        chars[i] = (char)('0' + (chars[i] - '0' + 1 + r.Next(9)) % 10);
                        break;
                    }
                    return new string(chars);
                }
            }
        }

        [Test]
        public void ThousandsOfDamagedSaves_LoadTheHealthyCopy_LosingAtMostOneDay()
        {
            var r = new Random(20261123);
            for (var run = 0; run < 3000; run++)
            {
                var storage = new MemoryStorage();
                var store = new SaveStore(storage, SaveStoreTests.Serializer);
                Assert.IsTrue(store.Save(Day(5)));
                Assert.IsTrue(store.Save(Day(6))); // principal = jour 6, copie de secours = jour 5
                var damageBackupToo = r.Next(4) == 0;
                storage.Files[SaveStore.MainFile] = Damage(storage.Files[SaveStore.MainFile], r, out var how);
                if (damageBackupToo) storage.Files[SaveStore.BackupFile] = Damage(storage.Files[SaveStore.BackupFile], r, out _);

                LoadResult loaded = default;
                Assert.DoesNotThrow(() => loaded = new SaveStore(storage, SaveStoreTests.Serializer).Load(), $"essai {run} ({how})");
                if (loaded.Data != null)
                {
                    Assert.That(loaded.Data.career.dayNumber, Is.InRange(5, 6), $"essai {run} ({how}) : au plus un jour perdu");
                    Assert.AreEqual(1000 * loaded.Data.career.dayNumber, loaded.Data.career.savings, $"essai {run} ({how}) : contenu cohérent");
                }
                else if (!damageBackupToo)
                    Assert.Fail($"essai {run} ({how}) : la copie de secours était saine, statut {loaded.Status} ({loaded.Detail})");
            }
        }

        [Test]
        public void DamagedSave_IsSetAside_AndTheGameCanSaveAgain()
        {
            var r = new Random(7);
            for (var run = 0; run < 300; run++)
            {
                var storage = new MemoryStorage();
                var store = new SaveStore(storage, SaveStoreTests.Serializer);
                store.Save(Day(2));
                store.Save(Day(3));
                storage.Files[SaveStore.MainFile] = Damage(storage.Files[SaveStore.MainFile], r, out var how);
                var again = new SaveStore(storage, SaveStoreTests.Serializer);
                again.Load();
                Assert.IsTrue(again.Save(Day(4)), $"essai {run} ({how}) : on peut de nouveau sauvegarder");
                Assert.AreEqual(4, new SaveStore(storage, SaveStoreTests.Serializer).Load().Data.career.dayNumber, $"essai {run} ({how})");
            }
        }

        [Test]
        public void DamagedVersionNumber_OutsideTheChecksum_DoesNotBlockTheSave()
        {
            // Le numéro de version de l'enveloppe n'est pas couvert par l'empreinte : un « 6 » devenu « 9 » faisait
            // croire à une sauvegarde d'un jeu plus récent, et la partie restait bloquée (ni chargée, ni réécrite).
            var storage = new MemoryStorage();
            var store = new SaveStore(storage, SaveStoreTests.Serializer);
            store.Save(Day(5));
            store.Save(Day(6));
            var text = storage.Files[SaveStore.MainFile];
            var key = "\"version\":" + SaveMigrations.CurrentVersion;
            Assert.IsTrue(text.Contains(key), text.Substring(0, Math.Min(80, text.Length)));
            storage.Files[SaveStore.MainFile] = ReplaceFirst(text, key, "\"version\":9");
            var loaded = new SaveStore(storage, SaveStoreTests.Serializer).Load();
            Assert.IsNotNull(loaded.Data, loaded.Status + " " + loaded.Detail);
            Assert.AreEqual(6, loaded.Data.career.dayNumber);
        }

        private static string ReplaceFirst(string text, string a, string b)
        {
            var i = text.IndexOf(a, StringComparison.Ordinal);
            return text.Substring(0, i) + b + text.Substring(i + a.Length);
        }
    }
}
