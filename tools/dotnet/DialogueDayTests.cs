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
            var chats = new System.Collections.Generic.HashSet<string>();
            int chatSaid = 0, chatRepeats = 0;
            Bubble last = null;
            for (var t = 0f; t < 2100f; t += 0.5f)
            {
                var gone = d.Update(0.5f, 3);
                // Discussions entre passagers (déclenchées par Update) : comptées à part.
                if (d.Current != null && d.Current != last && d.Current.Line.trigger == TalkTriggers.Chat)
                {
                    chatSaid++;
                    if (!chats.Add(d.Current.Line.id)) chatRepeats++;
                }
                last = d.Current;
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
                        last = d.Current;
                        var line = d.Current.Line;
                        StringAssert.DoesNotContain("{", d.Current.Text, $"{line.id} : valeur non remplie");
                        Assert.IsTrue(string.IsNullOrEmpty(line.character) || line.character == d.CharacterOf(id), $"{line.id} dite par {d.CharacterOf(id)}");
                    }
                }
            }
            TestContext.WriteLine($"{d.Said} bulles, {d.Repeats} répétées ; discussions : {chatSaid} dont {chatRepeats} répétées");
            Assert.Greater(d.Said, 40);
            // 3e lot (22/11, ≈ 170 répliques, décision de Kevin) : 135 bulles dont 62 déjà dites dans la journée (46 %),
            // contre 109 dont 71 (65 %) avec 85 répliques ; les discussions faisaient 28 des 62 répétitions (46 discussions
            // pour 18 répliques). 25 discussions de plus (décision de Kevin, 22/11) : 37 répétées sur 135 (27 %), dont
            // 3 discussions répétées sur 46.
            Assert.Less(d.Repeats * 10, d.Said * 3, "Plus de 30 % des bulles répétées dans la journée");
            Assert.Greater(chatSaid, 20, "Des discussions entre passagers dans la journée");
            Assert.LessOrEqual(chatRepeats * 10, chatSaid, "Plus d'une discussion sur dix répétée dans la journée");
        }
    }
}
