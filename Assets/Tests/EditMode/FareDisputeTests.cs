using System;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Talk;

namespace TaxiKmer.Core.Tests
{
    /// <summary>
    /// Jalon 5, décisions de Kevin du 21/11 : négociation (option A), refus de payer (option C : appeler la police),
    /// effet des réponses du chauffeur (réputation et pourboire), petits sons de voix, silence.
    /// </summary>
    public class FareDisputeTests
    {
        private static StopsConfig Stops() => new StopsConfig
        {
            stops = new[]
            {
                new Stop { id = "a", name = "A", distanceAlongRouteMeters = 0 },
                new Stop { id = "b", name = "B", distanceAlongRouteMeters = 500 },
                new Stop { id = "c", name = "C", distanceAlongRouteMeters = 1000 },
            },
        };

        private static Street NewStreet(FareDisputeRules rules, int seed = 3)
        {
            var stops = Stops();
            var shift = new Shift(new DayConfig(), new SeatingConfig());
            return new Street(new StreetConfig(), stops, new ClientFactory(stops, FareConfig.Default(), seed), shift, 1000f)
            {
                Reputation = new DriverReputation(new ReputationConfig()),
                FareDisputes = rules,
                DisputeRandom = new Random(5),
            };
        }

        private static RoadsideClient AcceptOne(Street street, out TaxiOnRoute stopped)
        {
            var c = street.Waiting.First(x => x.Pickup.id == "a" && x.Direction == 1);
            var at = new TaxiOnRoute { Along = c.Along - 30f, Direction = 1, Lateral = 2f };
            street.Update(0.1f, at);
            street.Honk(at, out _);
            stopped = new TaxiOnRoute { Along = c.Along, Direction = 1, Lateral = 2f };
            return c;
        }

        [Test]
        public void MaybeHaggle_OffersLess_RoundedAndFloored_ScaledByReputation()
        {
            var rules = new FareDisputeRules { HaggleChance = 1f };
            for (var seed = 0; seed < 50; seed++)
            {
                var offer = FareDisputes.MaybeHaggle(rules, 400, 1f, new Random(seed));
                Assert.That(offer, Is.InRange(250, 350), "15 à 35 % de moins, arrondi à 50.");
                Assert.AreEqual(0, offer % 50);
            }
            Assert.AreEqual(0, FareDisputes.MaybeHaggle(new FareDisputeRules { HaggleChance = 0f }, 400, 1f, new Random(1)));
            Assert.AreEqual(0, FareDisputes.MaybeHaggle(rules, 400, 0f, new Random(1)), "Facteur 0 : jamais.");
            Assert.AreEqual(100, FareDisputes.MaybeHaggle(new FareDisputeRules { HaggleChance = 1f, HaggleDiscountMin = 0.9f, HaggleDiscountMax = 0.9f }, 300, 1f, new Random(1)),
                "Jamais sous le plancher.");
            int Count(float factor)
            {
                var n = 0;
                var r = new Random(7);
                for (var i = 0; i < 4000; i++) if (FareDisputes.MaybeHaggle(new FareDisputeRules { HaggleChance = 0.2f }, 400, factor, r) > 0) n++;
                return n;
            }
            Assert.AreEqual(0.2 * 0.4, Count(ReputationEffects.Haggle(new ReputationEffectsConfig(), 100)) / 4000.0, 0.02, "Bonne réputation : prix mieux acceptés.");
            Assert.AreEqual(0.2 * 1.6, Count(ReputationEffects.Haggle(new ReputationEffectsConfig(), 0)) / 4000.0, 0.03);
        }

        [Test]
        public void Haggle_Accept_ClientPaysTheOffer()
        {
            var street = NewStreet(new FareDisputeRules { HaggleChance = 1f });
            var c = AcceptOne(street, out var stopped);
            var h = street.PendingHaggle;
            Assert.IsNotNull(h, "Le client propose moins.");
            Assert.AreSame(c, h.Client);
            Assert.Less(h.Offer, h.Asked);
            Assert.AreNotEqual(StreetActionKind.Board, street.AvailableAction(stopped).Kind, "Il attend la réponse avant de monter.");
            Assert.IsTrue(street.AnswerHaggle(true));
            Assert.IsNull(street.PendingHaggle);
            Assert.AreEqual(h.Offer, c.Client.ProposedFare);
            Assert.AreEqual(BoardResult.Boarded, street.Perform(stopped).Board);
            var dest = street.FindStop(c.Client.DestinationStopId).distanceAlongRouteMeters;
            Assert.AreEqual(h.Offer, street.Perform(new TaxiOnRoute { Along = dest, Direction = 1, Lateral = 2f }).Paid);
            Assert.IsFalse(street.AnswerHaggle(true), "Plus rien à répondre.");
        }

        [Test]
        public void Haggle_Refuse_ClientStaysOnTheRoadside()
        {
            var street = NewStreet(new FareDisputeRules { HaggleChance = 1f });
            var c = AcceptOne(street, out var stopped);
            var asked = c.Client.ProposedFare;
            Assert.IsTrue(street.AnswerHaggle(false));
            Assert.AreEqual(RoadsideState.Waiting, c.State);
            Assert.AreEqual(asked, c.Client.ProposedFare);
            Assert.AreNotEqual(StreetActionKind.Board, street.AvailableAction(stopped).Kind);
            Assert.Contains(c, street.Waiting.ToList());
        }

        [Test]
        public void NoDisputeRules_NobodyHaggles_OrRefuses()
        {
            var street = NewStreet(null);
            var c = AcceptOne(street, out var stopped);
            Assert.IsNull(street.PendingHaggle);
            street.Perform(stopped);
            var dest = street.FindStop(c.Client.DestinationStopId).distanceAlongRouteMeters;
            var o = street.Perform(new TaxiOnRoute { Along = dest, Direction = 1, Lateral = 2f });
            Assert.IsNull(o.Refusal);
            Assert.AreEqual(c.Client.ProposedFare, o.Paid);
        }

        [Test]
        public void RefusesToPay_LetGo_OrCallThePolice()
        {
            foreach (var (callPolice, payChance, expectPaid) in new[] { (false, 1f, false), (true, 1f, true), (true, 0f, false) })
            {
                var street = NewStreet(new FareDisputeRules { RefusesToPayChance = 1f, PolicePayChance = payChance, PoliceMinutes = 30 });
                var c = AcceptOne(street, out var stopped);
                street.Perform(stopped);
                var fare = c.Client.ProposedFare;
                var dest = street.FindStop(c.Client.DestinationStopId).distanceAlongRouteMeters;
                var o = street.Perform(new TaxiOnRoute { Along = dest, Direction = 1, Lateral = 2f });
                Assert.IsNotNull(o.Refusal, "Il refuse de payer.");
                Assert.AreEqual(0, o.Paid);
                Assert.AreEqual(0, street.Shift.Cabin.PassengerCount, "Il est descendu.");
                Assert.AreSame(o.Refusal, street.PendingRefusal);
                var before = street.Shift.Clock.GameMinutes;
                var r = street.ResolveRefusal(callPolice);
                Assert.IsNull(street.PendingRefusal);
                Assert.AreEqual(callPolice, r.PoliceCalled);
                Assert.AreEqual(expectPaid, r.Paid, $"police {callPolice}, chance {payChance}");
                Assert.AreEqual(callPolice ? before + 30 : before, street.Shift.Clock.GameMinutes, 1, "Appeler la police prend du temps.");
                Assert.AreEqual(expectPaid ? fare : 0, street.Shift.Ledger.Revenue);
                Assert.AreEqual(expectPaid ? 0 : fare, street.LeftUnpaidFares);
            }
        }

        /// <summary>
        /// QA (22/11) : le dernier passager déposé après l'heure de fin refuse de payer. La journée ne doit pas se clore à
        /// l'image suivante avant la réponse du joueur (sinon le panneau disparaît, la police ne peut plus être appelée
        /// et le bilan oublie la course impayée) ; terminer depuis la pause compte la course comme impayée.
        /// </summary>
        [Test]
        public void RefusesToPay_AfterEndOfDay_DayWaitsForTheAnswer()
        {
            var street = NewStreet(new FareDisputeRules { RefusesToPayChance = 1f, PolicePayChance = 1f, PoliceMinutes = 30 });
            var c = AcceptOne(street, out var stopped);
            street.Perform(stopped);
            var fare = c.Client.ProposedFare;
            street.Shift.Clock.Advance(1e6f); // l'heure de fin est passée, le passager est encore à bord
            var dest = new TaxiOnRoute { Along = street.FindStop(c.Client.DestinationStopId).distanceAlongRouteMeters, Direction = 1, Lateral = 2f };
            Assert.IsNotNull(street.Perform(dest).Refusal);
            street.Update(0.1f, dest);
            Assert.IsFalse(street.DayFinished, "La journée attend la réponse au refus de payer.");
            Assert.IsNotNull(street.PendingRefusal);
            var r = street.ResolveRefusal(true);
            Assert.IsTrue(r.Paid);
            Assert.IsTrue(street.DayFinished, "Réponse donnée, taxi vide : la journée se termine.");
            Assert.AreEqual(fare, street.Summary.Revenue, "La course payée grâce à la police est dans le bilan.");

            var other = NewStreet(new FareDisputeRules { RefusesToPayChance = 1f });
            var c2 = AcceptOne(other, out var stopped2);
            other.Perform(stopped2);
            other.Perform(new TaxiOnRoute { Along = other.FindStop(c2.Client.DestinationStopId).distanceAlongRouteMeters, Direction = 1, Lateral = 2f });
            other.EndDayNow();
            Assert.IsNull(other.PendingRefusal, "Journée terminée : plus de refus en attente.");
            Assert.AreEqual(1, other.Summary.UnpaidPassengers, "Le passager qui refusait compte comme impayé.");
            Assert.AreEqual(c2.Client.ProposedFare, other.Summary.UnpaidFares);
        }

        /// <summary>
        /// Décision de Kevin (23/11) : « Terminer la journée » depuis la pause est refusé tant qu'un choix attend (ici le
        /// passager qui refuse de payer) ; une fois la réponse donnée, la fin de journée est possible.
        /// </summary>
        [Test]
        public void EndDay_RefusedWhileAPassengerRefusesToPay()
        {
            var street = NewStreet(new FareDisputeRules { RefusesToPayChance = 1f, PolicePayChance = 1f, PoliceMinutes = 30 });
            var c = AcceptOne(street, out var stopped);
            street.Perform(stopped);
            var dest = new TaxiOnRoute { Along = street.FindStop(c.Client.DestinationStopId).distanceAlongRouteMeters, Direction = 1, Lateral = 2f };
            Assert.IsNotNull(street.Perform(dest).Refusal);
            var menu = new TaxiKmer.Core.Ux.GameMenu(false);
            menu.Pause();
            var pending = TaxiKmer.Core.Ux.PendingChoices.Of(street, dest);
            Assert.IsTrue(pending.Refusal);
            Assert.IsFalse(menu.EndDay(pending), "Refus de payer en attente : la journée ne se termine pas.");
            Assert.AreEqual(TaxiKmer.Core.Ux.MenuScreen.Pause, menu.Screen, "La pause reste ouverte.");
            Assert.AreEqual("Règle d'abord le passager qui refuse de payer avant de terminer la journée", TaxiKmer.Core.Ux.GameMenu.EndDayLabel(0, pending));
            street.ResolveRefusal(true);
            pending = TaxiKmer.Core.Ux.PendingChoices.Of(street, dest);
            Assert.IsFalse(pending.Any);
            Assert.IsTrue(menu.EndDay(pending));
            street.EndDayNow();
            Assert.AreEqual(c.Client.ProposedFare, street.Summary.Revenue, "La course payée grâce à la police est dans le bilan.");
        }

        [Test]
        public void Replies_ChangeReputation_AndPromiseATipPaidWithTheFare()
        {
            var street = NewStreet(null);
            var c = AcceptOne(street, out var stopped);
            street.Perform(stopped);
            street.ApplyReply(c.Client.Id, 2, 100);
            street.ApplyReply(c.Client.Id, -1, 50);
            Assert.AreEqual(51, street.Reputation.Score);
            Assert.AreEqual(150, street.PendingTip(c.Client.Id));
            street.ApplyReply("pas-a-bord", 0, 500);
            Assert.AreEqual(0, street.PendingTip("pas-a-bord"));
            var dest = street.FindStop(c.Client.DestinationStopId).distanceAlongRouteMeters;
            var o = street.Perform(new TaxiOnRoute { Along = dest, Direction = 1, Lateral = 2f });
            Assert.AreEqual(150, o.Tips);
            Assert.AreEqual(c.Client.ProposedFare + 150, o.Paid);
            Assert.AreEqual(150, street.Shift.Ledger.Tips);
            Assert.AreEqual(1, street.Shift.Ledger.RideCount, "Le pourboire ne compte pas de course.");
            Assert.AreEqual(0, street.PendingTip(c.Client.Id));
        }

        [Test]
        public void Voice_FromLineOrCharacter_SilenceTrigger()
        {
            var config = new DialogueConfig
            {
                enabled = true,
                silenceAfterSeconds = 20f,
                voiceSounds = new[] { new VoiceSoundData { id = "hmm", label = "hmm" }, new VoiceSoundData { id = "eh", label = "eh !" } },
                characters = new[] { new CharacterData { id = "prof", voice = "hmm" } },
                lines = new[]
                {
                    new LineData { id = "s1", trigger = TalkTriggers.Silence, text = "Tu es bien silencieux." },
                    new LineData { id = "m1", trigger = TalkTriggers.Boarding, character = "prof", text = "Bonjour, mon fils." },
                    new LineData { id = "m2", trigger = TalkTriggers.Boarding, character = "prof", voice = "eh", text = "Eh ! Bonjour." },
                },
            };
            Assert.AreEqual("hmm", config.VoiceFor(config.lines[1], "")?.id, "Son du personnage.");
            Assert.AreEqual("eh", config.VoiceFor(config.lines[2], "prof")?.id, "Son de la réplique d'abord.");
            Assert.IsNull(config.VoiceFor(config.lines[0], ""));

            var d = new DialogueDirector(config, new Random(1));
            for (var t = 0f; t < 19f; t += 0.5f) d.Update(0.5f, 1);
            Assert.IsNull(d.Current, "Pas encore assez de silence.");
            for (var t = 0f; t < 2f && d.Current == null; t += 0.5f) d.Update(0.5f, 1);
            Assert.AreEqual("s1", d.Current?.Line.id, "Un passager rompt le silence.");
            for (var t = 0f; t < 60f; t += 0.5f) d.Update(0.5f, 0);
            Assert.IsNull(d.Current, "Taxi vide : personne ne parle.");
            Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "c1", "prof"));
            Assert.IsNotNull(d.Current.Voice);
            Assert.AreEqual("", d.Current.Voice.clip, "Son à fournir : rien ne joue.");
            Assert.IsTrue(Array.IndexOf(TalkTriggers.All, TalkTriggers.Rain) >= 0 && Array.IndexOf(TalkTriggers.All, TalkTriggers.Control) >= 0);
        }

        [Test]
        public void FareRules_FromDialogueData()
        {
            var cfg = new DialogueConfig { haggleChance = 0.15f, refusesToPayChance = 0.03f };
            var rules = cfg.FareRules();
            Assert.AreEqual(0.15f, rules.HaggleChance);
            Assert.AreEqual(0.03f, rules.RefusesToPayChance);
            Assert.AreEqual(cfg.refusalPoliceMinutes, rules.PoliceMinutes);
        }

        [Test]
        public void Characters_StablePerPassenger_OnlyTheyUseTheirLines()
        {
            var config = new DialogueConfig
            {
                enabled = true,
                minGapSeconds = 0f,
                noRepeatWithin = 0,
                characterShare = 0.5f,
                characters = new[] { new CharacterData { id = "maman" }, new CharacterData { id = "prof" } },
                lines = new[]
                {
                    new LineData { id = "bebe", trigger = TalkTriggers.Boarding, character = "maman", text = "Le bébé dort." },
                    new LineData { id = "tous", trigger = TalkTriggers.Boarding, text = "Bonjour." },
                },
            };
            var d = new DialogueDirector(config, new Random(1));
            int maman = 0, prof = 0, none = 0;
            for (var i = 0; i < 400; i++)
            {
                var id = $"c{i}";
                var c = d.CharacterOf(id);
                Assert.AreEqual(c, d.CharacterOf(id), "Toujours le même personnage.");
                if (c == "maman") maman++; else if (c == "prof") prof++; else none++;
                Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, id));
                if (d.Current.Line.id == "bebe") Assert.AreEqual("maman", c, "Seule la maman parle du bébé.");
                d.Update(100f, 1);
            }
            Assert.AreEqual(0.5, none / 400.0, 0.08);
            Assert.Greater(maman, 50);
            Assert.Greater(prof, 50);
            d.AssignCharacter("x", "prof");
            Assert.AreEqual("prof", d.CharacterOf("x"));
        }
    }
}
