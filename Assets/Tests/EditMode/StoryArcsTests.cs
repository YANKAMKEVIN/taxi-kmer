using System;
using NUnit.Framework;
using TaxiKmer.Core.Talk;

namespace TaxiKmer.Core.Tests
{
    public class StoryArcsTests
    {
        private static readonly StoryArcData Arc = new StoryArcData { id = "examen", character = "etudiant", steps = 3, minDaysBetween = 1 };

        private static LineData Step(int n) => new LineData
        {
            id = "examen-" + n, trigger = TalkTriggers.Boarding, character = "etudiant", text = "Rencontre " + n, arc = "examen", arcStep = n,
        };

        [Test]
        public void Steps_ComeInOrder_AtMostOncePerDay()
        {
            var s = new StoryArcs(new[] { Arc }, null, 1);
            Assert.IsFalse(s.Eligible(Step(2)));
            Assert.IsTrue(s.Eligible(Step(1)));
            s.Said(Step(1));
            Assert.IsTrue(s.Changed);
            Assert.IsFalse(s.Eligible(Step(2)), "pas deux rencontres le même jour");
            s.Today = 2;
            Assert.IsTrue(s.Eligible(Step(2)));
            s.Said(Step(2));
            s.Today = 3;
            s.Said(Step(3));
            Assert.IsTrue(s.Finished("examen"));
            Assert.IsFalse(s.Eligible(Step(3)), "histoire finie");
            Assert.IsTrue(s.Eligible(new LineData { id = "x" }), "les autres répliques ne changent pas");
        }

        [Test]
        public void Progress_SurvivesTheSave_AndIgnoresUnknownArcs()
        {
            var s = new StoryArcs(new[] { Arc }, null, 4);
            s.Said(Step(1));
            var saved = s.Save();
            CollectionAssert.AreEqual(new[] { "examen:1:4" }, saved);
            var back = new StoryArcs(new[] { Arc }, new[] { saved[0], "inconnu:2:1", "abîmé" }, 5);
            Assert.AreEqual(1, back.Done("examen"));
            Assert.AreEqual(0, back.Done("inconnu"));
            Assert.IsTrue(back.Eligible(Step(2)));
        }

        [Test]
        public void Director_TellsTheStory_WhenTheRegularBoards()
        {
            var config = new DialogueConfig
            {
                enabled = true,
                minGapSeconds = 0f,
                noRepeatWithin = 0,
                characters = new[] { new CharacterData { id = "etudiant", name = "L'étudiant" } },
                arcs = new[] { Arc },
                lines = new[]
                {
                    new LineData { id = "banale", trigger = TalkTriggers.Boarding, text = "Bonjour." },
                    Step(1), Step(2), Step(3),
                },
            };
            var director = new DialogueDirector(config, new Random(1)) { Stories = new StoryArcs(config.arcs, null, 1) };
            director.AssignCharacter("p1", "etudiant");
            Assert.IsTrue(director.Trigger(TalkTriggers.Boarding, "p1"));
            Assert.AreEqual("Rencontre 1", director.Current.Text);

            // Un autre passager ne dit jamais la réplique d'histoire de l'étudiant.
            var other = new DialogueDirector(config, new Random(1)) { Stories = new StoryArcs(config.arcs, null, 1) };
            other.AssignCharacter("p2", "");
            for (var i = 0; i < 5; i++)
            {
                Assert.IsTrue(other.Trigger(TalkTriggers.Boarding, "p2"));
                Assert.AreEqual("Bonjour.", other.Current.Text);
                other.Update(100f, 1);
            }
        }
    }
}
