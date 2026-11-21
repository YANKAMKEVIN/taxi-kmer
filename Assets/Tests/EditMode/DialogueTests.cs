using System;
using System.Collections.Generic;
using NUnit.Framework;
using TaxiKmer.Core.Talk;

namespace TaxiKmer.Core.Tests
{
    public class DialogueTests
    {
        private static DialogueConfig Config(int linesPerTrigger, params string[] triggers)
        {
            var lines = new List<LineData>();
            foreach (var t in triggers)
                for (var i = 0; i < linesPerTrigger; i++)
                    lines.Add(new LineData
                    {
                        id = $"{t}-{i}",
                        trigger = t,
                        text = $"Réplique d'essai {i} pour {t}",
                        replies = new[] { new ReplyData { id = "oui", text = "Oui" }, new ReplyData { id = "non", text = "Non" } },
                    });
            return new DialogueConfig { enabled = true, lines = lines.ToArray() };
        }

        private static void Wait(DialogueDirector d, float seconds, int passengers = 1)
        {
            for (var t = 0f; t < seconds; t += 0.1f) d.Update(0.1f, passengers);
        }

        [Test]
        public void Disabled_NobodyTalks()
        {
            var d = new DialogueDirector(new DialogueConfig { lines = Config(3, TalkTriggers.Boarding).lines }, new Random(1));
            Assert.IsFalse(d.Trigger(TalkTriggers.Boarding, "c1"));
            Assert.IsNull(d.Current);
        }

        [Test]
        public void OneBubbleAtATime_SilenceBetween_ReadableWhileDriving()
        {
            var config = Config(5, TalkTriggers.Boarding);
            var d = new DialogueDirector(config, new Random(1));
            Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "c1"));
            Assert.AreEqual("c1", d.Current.SpeakerId);
            Assert.IsFalse(d.Trigger(TalkTriggers.Boarding, "c2"), "Une seule bulle à la fois.");
            Assert.GreaterOrEqual(d.Current.ShowSeconds, config.minShowSeconds);
            Assert.AreEqual(Math.Max(2.5f, d.Current.Line.text.Length / 14f), d.Current.ShowSeconds, 1e-4f);
            Wait(d, config.replySeconds + 0.2f);
            Assert.IsNull(d.Current, "Sans réponse, la bulle part toute seule.");
            Assert.IsFalse(d.Trigger(TalkTriggers.Boarding, "c2"), "Silence entre deux bulles.");
            Wait(d, config.minGapSeconds);
            Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "c2"));
        }

        [Test]
        public void ShowSeconds_GrowsWithText()
        {
            var c = new DialogueConfig();
            Assert.AreEqual(2.5f, DialogueDirector.ShowSecondsFor(c, "Oui"), 1e-6f);
            Assert.AreEqual(70f / 14f, DialogueDirector.ShowSecondsFor(c, new string('a', 70)), 1e-4f);
        }

        [Test]
        public void QuickReply_WhileOpen_ThenGone()
        {
            var config = Config(2, TalkTriggers.Detour);
            var d = new DialogueDirector(config, new Random(1));
            d.Trigger(TalkTriggers.Detour, "c1");
            Assert.IsTrue(d.Current.RepliesOpen(config.replySeconds));
            Assert.IsFalse(d.Answer("peut-etre"));
            Assert.IsTrue(d.Answer("oui"));
            Assert.IsFalse(d.Answer("non"), "Une seule réponse.");
            Bubble gone = null;
            for (var t = 0f; t < 10f && gone == null; t += 0.1f) gone = d.Update(0.1f, 1);
            Assert.AreEqual("oui", gone.Reply.id, "La réponse choisie part avec la bulle (pour en appliquer l'effet).");
        }

        [Test]
        public void NoRepeat_PrefersSilence()
        {
            var config = Config(3, TalkTriggers.Boarding);
            config.noRepeatWithin = 3;
            var d = new DialogueDirector(config, new Random(2));
            var seen = new HashSet<string>();
            for (var i = 0; i < 3; i++)
            {
                Wait(d, 20f);
                Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "c"));
                Assert.IsTrue(seen.Add(d.Current.Line.id), "Pas deux fois la même tant qu'il en reste d'autres.");
            }
            Wait(d, 20f);
            Assert.IsFalse(d.Trigger(TalkTriggers.Boarding, "c"), "Toutes dites récemment : le passager se tait.");
        }

        [Test]
        public void Character_OnlyTheirLines()
        {
            var config = Config(0);
            config.lines = new[]
            {
                new LineData { id = "a", trigger = TalkTriggers.Boarding, character = "etudiant", text = "A" },
                new LineData { id = "b", trigger = TalkTriggers.Boarding, character = "commercante", text = "B" },
            };
            var d = new DialogueDirector(config, new Random(1));
            Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "c", "commercante"));
            Assert.AreEqual("b", d.Current.Line.id);
        }

        [Test]
        public void Chat_StartsOnlyWithTwoPassengers()
        {
            var config = Config(4, TalkTriggers.Chat);
            var d = new DialogueDirector(config, new Random(1));
            Wait(d, 100f, passengers: 1);
            Assert.AreEqual(0, d.Said);
            Wait(d, config.chatEverySeconds + 1f, passengers: 2);
            Assert.AreEqual(1, d.Said);
        }

        [Test]
        public void Day_WithEnoughLines_FewRepeats()
        {
            // Critère du jalon 5 : sur une journée (durée de day.json), les dialogues ne se répètent pas trop.
            // Journée de 35 min : environ 160 bulles à ce rythme ; il faut alors deux douzaines de répliques par situation
            // (8 suffisaient pour 10 min) : chiffre donné à Kevin dans jalon-05.md.
            var config = Config(24, TalkTriggers.All);
            var d = new DialogueDirector(config, new Random(4));
            var rng = new Random(5);
            var dayLength = new TaxiKmer.Core.Day.DayConfig().dayLengthRealSeconds;
            for (var t = 0f; t < dayLength; t += 0.1f)
            {
                d.Update(0.1f, 3);
                if (rng.NextDouble() < 0.02) d.Trigger(TalkTriggers.All[rng.Next(TalkTriggers.All.Length)], "c");
            }
            Assert.Greater(d.Said, 20);
            Assert.LessOrEqual(d.Repeats, d.Said / 4, $"{d.Repeats} répétitions sur {d.Said}");
        }

        [Test]
        public void Detour_ComplainsWhenDrivingAway()
        {
            var w = new DetourWatch(150f);
            Assert.IsFalse(w.Update("p", 3000f, 1000f));
            Assert.IsFalse(w.Update("p", 3000f, 2000f), "On s'approche.");
            Assert.IsFalse(w.Update("p", 3000f, 1900f), "Un peu en arrière : pas encore.");
            Assert.IsTrue(w.Update("p", 3000f, 1840f), "150 m de détour : il se plaint.");
            Assert.IsFalse(w.Update("p", 3000f, 1800f), "Pas à chaque mètre.");
            Assert.IsTrue(w.Update("p", 3000f, 1690f), "Encore 150 m : encore une plainte.");
            w.Forget("p");
            Assert.IsFalse(w.Update("p", 3000f, 1000f));
        }
    }
}
