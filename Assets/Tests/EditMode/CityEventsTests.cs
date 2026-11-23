using System;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Talk;

namespace TaxiKmer.Core.Tests
{
    public class CityEventsTests
    {
        private static CalendarDay Day(int number, int weekday) => new CalendarDay { DayNumber = number, Weekday = weekday };

        [Test]
        public void Draw_IsRare_RespectsWeekdays_AndIsStable()
        {
            var config = new CityEventsConfig();
            int cortege = 0, match = 0;
            for (var n = 1; n <= 2000; n++)
            {
                var weekday = (n - 1) % 7;
                var e = CityEventDay.Draw(config, Day(n, weekday), 42);
                Assert.AreEqual(e.Event?.id, CityEventDay.Draw(config, Day(n, weekday), 42).Event?.id, "même jour, même événement");
                if (!e.Any) continue;
                if (e.Event.id == "cortege") { cortege++; Assert.Less(weekday, 5, "en semaine"); }
                if (e.Event.id == "match") { match++; CollectionAssert.Contains(new[] { 2, 5, 6 }, weekday); }
            }
            Assert.That(cortege / (2000.0 * 5 / 7), Is.InRange(0.02, 0.09), "≈ 5 % des jours de semaine");
            Assert.That(match, Is.GreaterThan(0));
            config.enabled = false;
            Assert.IsFalse(CityEventDay.Draw(config, Day(3, 2), 1).Any);
        }

        [Test]
        public void Cortege_ClosesThePalaceRoad_OnlyDuringItsHours()
        {
            var e = new CityEventDay(new CityEventsConfig().events[0]);
            Assert.IsTrue(e.HasClosure);
            Assert.IsFalse(e.ClosedAt(6000f, 7 * 60 + 59));
            Assert.IsTrue(e.ClosedAt(6000f, 8 * 60));
            Assert.IsFalse(e.ClosedAt(3000f, 8 * 60), "seulement devant le palais");
            Assert.IsFalse(e.ClosedAt(6000f, 9 * 60 + 30));
            Assert.AreEqual(1.6f, e.TrafficFactor(8 * 60 + 30));
            Assert.AreEqual(1f, e.TrafficFactor(12 * 60));
            Assert.AreEqual("cortege", e.ActiveId(8 * 60));
            Assert.AreEqual("", e.ActiveId(10 * 60));
        }

        [Test]
        public void EventLines_AreSaidOnlyDuringTheEvent()
        {
            var config = new DialogueConfig
            {
                enabled = true, minGapSeconds = 0f, noRepeatWithin = 0,
                lines = new[] { new LineData { id = "match", trigger = TalkTriggers.Chat, text = "Allez les Lions !", cityEvent = "match" } },
            };
            var d = new DialogueDirector(config, new Random(1));
            Assert.IsFalse(d.Trigger(TalkTriggers.Chat, ""));
            d.CityEvent = "match";
            Assert.IsTrue(d.Trigger(TalkTriggers.Chat, ""));
        }
    }
}
