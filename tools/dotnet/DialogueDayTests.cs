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
                    // Formes parlées du lieu (décision de Kevin du 23/11) : « à Emana », « d'Emana ».
                    var values = TalkValues.ForRide(new TaxiKmer.Core.Rides.Stop { id = "emana", name = "Emana" }, "emana", 800);
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

        /// <summary>
        /// Décision de Kevin (23/11, QA « Bonsoir chauffeur » à 6 h) : une réplique peut avoir une plage horaire, et la
        /// sélection la respecte. Une journée de 06:00 à 20:00 sur les vraies répliques : « Bonsoir » seulement le soir,
        /// « Bonjour » seulement le matin, « ce matin » / « avant huit heures » à la bonne heure.
        /// </summary>
        [Test]
        public void ProposedLines_ADay_RespectTheirHours()
        {
            var opts = new JsonSerializerOptions { IncludeFields = true };
            var config = JsonSerializer.Deserialize<DialogueConfig>(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "dialogues.json")), opts);
            var errors = new System.Collections.Generic.List<string>();
            int bonsoir = 0, bonjour = 0;
            for (var seed = 1; seed <= 6; seed++)
            {
                var d = new DialogueDirector(config, new Random(seed));
                var n = 0;
                for (var minute = 6 * 60; minute < 20 * 60; minute += 2)
                {
                    SetTime(d, minute);
                    d.Update(30f, 2);
                    d.Update(10f, 2); // la bulle précédente est partie, le silence entre deux bulles est passé
                    var id = $"p{n++}";
                    var trigger = TalkTriggers.All[n % TalkTriggers.All.Length];
                    var values = TalkValues.ForRide(new TaxiKmer.Core.Rides.Stop { id = "emana", name = "Emana" }, "emana", 800, 600);
                    if (!d.Trigger(trigger, id, "", values)) continue;
                    var text = d.Current.Text;
                    var hhmm = $"{minute / 60:00}:{minute % 60:00}";
                    if (text.StartsWith("Bonsoir")) { bonsoir++; if (minute < 17 * 60) errors.Add($"{hhmm} {d.Current.Line.id} : {text}"); }
                    if (text.StartsWith("Bonjour")) { bonjour++; if (minute >= 12 * 60) errors.Add($"{hhmm} {d.Current.Line.id} : {text}"); }
                    if ((text.Contains("pressé ce matin") && minute >= 12 * 60)
                        || (text.Contains("on arrive à midi") && minute >= 12 * 60)
                        || (text.Contains("avant huit heures") && minute >= 8 * 60)
                        || (text.Contains("ce soir") && minute >= 19 * 60))
                        errors.Add($"{hhmm} {d.Current.Line.id} : {text}");
                }
            }
            TestContext.WriteLine($"« Bonjour » {bonjour} fois, « Bonsoir » {bonsoir} fois sur 6 journées");
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
            Assert.Greater(bonjour, 0, "Le matin, on dit bonjour.");
        }

        private static void SetTime(DialogueDirector d, int minuteOfDay) => d.MinuteOfDay = minuteOfDay;
    }
}
