using System.Collections.Generic;
using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Driving;
using TaxiKmer.Core.Rides;

namespace TaxiKmer.Core.Tests
{
    public class DayReplayTests
    {
        private static Street NewStreet(int seed)
        {
            var stops = new StopsConfig
            {
                stops = new[]
                {
                    new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                    new Stop { id = "b", name = "B", distanceAlongRouteMeters = 500 },
                    new Stop { id = "c", name = "C", distanceAlongRouteMeters = 1000 },
                },
            };
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            return new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), seed), shift, 1000f);
        }

        /// <summary>Une journée jouée « à la main » : aller-retour, klaxon et bouton d'action à chaque arrêt.</summary>
        private static ReplayData Play(Street street, DayRecorder recorder)
        {
            const float dt = 1f / 30f;
            float along = 0f;
            var direction = 1;
            for (var f = 0; f < 30 * 600 + 60 && !street.DayFinished; f++)
            {
                var frame = f - 60;
                var title = frame < 0; // deux secondes d'écran titre : la rue vit, l'horloge attend
                if (title)
                {
                    var still = new TaxiOnRoute { Along = 0f, Direction = 1, Lateral = 2f };
                    street.Update(dt, still);
                    recorder.Frame(dt, still, default, clockRuns: false);
                    continue;
                }
                var stopped = frame % 900 < 150; // arrêt de 5 s toutes les 30 s
                var speed = stopped ? 0f : 30f;
                along += direction * speed / 3.6f * dt;
                if (along > 1000f) { along = 1000f; direction = -1; }
                if (along < 0f) { along = 0f; direction = 1; }
                var taxi = new TaxiOnRoute { Along = along, Direction = direction, Lateral = 2f * direction, SpeedKmh = speed };
                var input = new DriveInput { Accelerate = !stopped, Brake = stopped };
                street.Shift.Clock.Advance(dt);
                street.Shift.Drive(speed / 3.6f * dt);
                street.Update(dt, taxi);
                recorder.Frame(dt, taxi, input);
                if (frame % 900 == 10) { street.Honk(taxi, out _); recorder.Event(ReplayEvents.Honk); }
                if (frame % 900 == 120) { street.Perform(taxi); recorder.Event(ReplayEvents.Action); }
            }
            return recorder.Data();
        }

        [Test]
        public void ReplayingTheRecording_ReproducesTheDay()
        {
            var original = NewStreet(11);
            var data = Play(original, new DayRecorder(11, 3));
            Assert.Greater(original.Shift.Ledger.Revenue, 0, "la journée d'essai gagne de l'argent");

            var again = NewStreet(data.seed);
            var others = DayReplayer.RunStreet(data, again);
            CollectionAssert.IsEmpty(others);
            Assert.AreEqual(original.Shift.Ledger.Revenue, again.Shift.Ledger.Revenue);
            Assert.AreEqual(original.Shift.Ledger.RideCount, again.Shift.Ledger.RideCount);
            Assert.AreEqual(original.Shift.Cabin.PassengerCount, again.Shift.Cabin.PassengerCount);
            Assert.AreEqual(original.Waiting.Count, again.Waiting.Count);
            Assert.AreEqual(original.Shift.Clock.GameMinutes, again.Shift.Clock.GameMinutes);
        }

        [Test]
        public void InputsAndDirection_RoundTripThroughBits()
        {
            var input = new DriveInput { Left = true, Brake = true, Handbrake = true };
            var back = ReplayInput.Unpack(ReplayInput.Pack(input, -1), out var dir);
            Assert.AreEqual(-1, dir);
            Assert.IsTrue(back.Left && back.Brake && back.Handbrake);
            Assert.IsFalse(back.Right || back.Accelerate || back.Horn);
        }

        [Test]
        public void Replayer_GivesEventsOnTheirFrame_AndSurvivesJson()
        {
            var rec = new DayRecorder(5, 2);
            var t = new TaxiOnRoute { Along = 10f, Direction = 1 };
            rec.Frame(0.1f, t, default);
            rec.Frame(0.1f, t, default);
            rec.Event(ReplayEvents.Police, "Pay");
            rec.Frame(0.1f, t, default);
            var json = TestSaveSerializer.Instance.ToJson(rec.Data()); // JsonUtility dans Unity, System.Text.Json hors Unity
            var data = TestSaveSerializer.Instance.FromJson<ReplayData>(json);
            var r = new DayReplayer(data);
            var events = new List<ReplayEventData>();
            r.Next(out _, out _, out _, events);
            Assert.AreEqual(0, events.Count);
            r.Next(out _, out _, out _, events);
            Assert.AreEqual("Pay", events[0].arg);
            Assert.IsTrue(r.Next(out _, out _, out _, events));
            Assert.IsFalse(r.Next(out _, out _, out _, events));
        }
    }
}
