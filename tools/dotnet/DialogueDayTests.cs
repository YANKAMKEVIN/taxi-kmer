// Journée de dialogues sur les vraies répliques (dotnet seulement : lit dialogues.json).
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using TaxiKmer.Core.Talk;

namespace TaxiKmer.Core.DotnetTests
{
    public class DialogueDayTests
    {
        /// <summary>
        /// Une journée de 35 min avec les répliques proposées (jalon 5, 21/11) : des bulles s'affichent, chaque personnage
        /// ne dit que ses répliques ou les répliques générales, et on mesure les répétitions (rapport pour Kevin).
        /// </summary>
        [Test]
        public void ProposedLines_ADay_Talks_RespectsCharacters()
        {
            var opts = new JsonSerializerOptions { IncludeFields = true };
            var config = JsonSerializer.Deserialize<DialogueConfig>(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "dialogues.json")), opts);
            var d = new DialogueDirector(config, new Random(3));
            var rng = new Random(4);
            var triggers = new[] { TalkTriggers.Boarding, TalkTriggers.Destination, TalkTriggers.Price, TalkTriggers.Paid, TalkTriggers.Detour, TalkTriggers.Rain };
            var n = 0;
            for (var t = 0f; t < 2100f; t += 0.5f)
            {
                var gone = d.Update(0.5f, 3);
                if (t % 15f < 0.5f)
                {
                    var id = $"p{n++ / 3}";
                    d.SetPassengers(new[] { id, $"p{n}" });
                    // Valeurs fournies par le jeu (22/11) : destination et prix de la course.
                    var values = new System.Collections.Generic.Dictionary<string, string>
                    {
                        [TalkValues.Destination] = "Emana",
                        [TalkValues.Price] = TalkValues.Amount(800),
                    };
                    if (d.Trigger(triggers[rng.Next(triggers.Length)], id, "", values))
                    {
                        var line = d.Current.Line;
                        StringAssert.DoesNotContain("{", d.Current.Text, $"{line.id} : valeur non remplie");
                        Assert.IsTrue(string.IsNullOrEmpty(line.character) || line.character == d.CharacterOf(id), $"{line.id} dite par {d.CharacterOf(id)}");
                    }
                }
            }
            TestContext.WriteLine($"{d.Said} bulles, {d.Repeats} répétées");
            Assert.Greater(d.Said, 40);
            // 3e lot (22/11, ≈ 170 répliques, décision de Kevin) : 135 bulles dont 62 déjà dites dans la journée (46 %),
            // contre 109 dont 71 (65 %) avec 85 répliques. Moins d'une bulle sur deux répétée.
            Assert.Less(d.Repeats * 2, d.Said, "Plus d'une bulle sur deux répétée dans la journée");
        }
    }
}
