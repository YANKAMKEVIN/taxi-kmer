using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class KeyMapTests
    {
        /// <summary>Décision de Kevin (23/11) : une touche différente pour chaque action (avant : N, P et W en double).</summary>
        [Test]
        public void EveryKey_DoesOneThing()
        {
            CollectionAssert.IsEmpty(KeyMap.Conflicts(), string.Join(" ; ", KeyMap.Conflicts()));
            var all = KeyMap.All.SelectMany(b => b.Keys).ToList();
            Assert.AreEqual(all.Count, all.Distinct().Count(), "Aucune touche en double dans la table.");
        }

        [Test]
        public void EveryAction_HasAKey_ValidUnityName_AndIsInTheHelp()
        {
            // Noms de KeyCode d'Unity utilisés (Enum.Parse côté présentation).
            var unity = new HashSet<string> { "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "Space", "Return", "Escape", "F1", "F2", "F3" };
            for (var c = 'A'; c <= 'Z'; c++) unity.Add(c.ToString());
            for (var d = 0; d <= 9; d++) unity.Add("Alpha" + d);
            foreach (KeyAction a in System.Enum.GetValues(typeof(KeyAction)))
            {
                var keys = KeyMap.KeysFor(a);
                Assert.IsNotEmpty(keys, $"{a} : sans touche");
                foreach (var k in keys) Assert.IsTrue(unity.Contains(k), $"{a} : « {k} » n'est pas un KeyCode");
                Assert.AreEqual(1, KeyMap.All.Count(b => b.Action == a), $"{a} : une seule ligne dans la table");
            }
            var help = KeyMap.Help();
            foreach (var b in KeyMap.All.Where(b => b.Action < KeyAction.Reply1)) StringAssert.Contains($"{b.Shown} {b.Label}", help);
            StringAssert.Contains("B radio", help);
            StringAssert.Contains("U pousser le taxi", help);
            StringAssert.Contains("J changer la roue", help);
            StringAssert.Contains("F1-F3", help);
            StringAssert.Contains("1-5", help);
        }
    }
}
