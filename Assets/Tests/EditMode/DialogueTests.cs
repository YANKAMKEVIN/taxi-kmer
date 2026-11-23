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

        private static LineData L(string id, string trigger, string register) =>
            new LineData { id = id, trigger = trigger, register = register, text = $"Réplique {id} (exemple)" };

        [Test]
        public void Registers_FrenchCamfranglaisPidgin_ByDefault()
        {
            var c = new DialogueConfig();
            CollectionAssert.AreEqual(TalkRegisters.All, Array.ConvertAll(c.registers, r => r.id), "Décision de Kevin : français, camfranglais, pidgin.");
            Assert.AreEqual(TalkRegisters.French, c.fallbackRegister);
        }

        [Test]
        public void Register_PerPassenger_Stable_AndSharedByWeights()
        {
            var c = new DialogueConfig();
            Assert.AreEqual(DialogueDirector.RegisterFor(c, "client-42"), DialogueDirector.RegisterFor(c, "client-42"), "Toujours le même pour un passager.");
            var counts = new Dictionary<string, int>();
            const int n = 6000;
            for (var i = 0; i < n; i++)
            {
                var r = DialogueDirector.RegisterFor(c, "c" + i);
                counts[r] = counts.TryGetValue(r, out var k) ? k + 1 : 1;
            }
            Assert.AreEqual(0.5, counts[TalkRegisters.French] / (double)n, 0.04);
            Assert.AreEqual(0.35, counts[TalkRegisters.Camfranglais] / (double)n, 0.04);
            Assert.AreEqual(0.15, counts[TalkRegisters.Pidgin] / (double)n, 0.04);

            c.registers[2].weight = 0f;
            for (var i = 0; i < 500; i++) Assert.AreNotEqual(TalkRegisters.Pidgin, DialogueDirector.RegisterFor(c, "c" + i), "Poids 0 : personne.");
            Assert.AreEqual("", DialogueDirector.RegisterFor(new DialogueConfig { registers = Array.Empty<RegisterData>() }, "c1"));
        }

        [Test]
        public void Lines_FollowTheSpeakersRegister_NeutralLinesForEveryone()
        {
            var config = new DialogueConfig
            {
                enabled = true,
                minGapSeconds = 0f,
                noRepeatWithin = 0,
                lines = new[]
                {
                    L("fr", TalkTriggers.Boarding, TalkRegisters.French),
                    L("cf", TalkTriggers.Boarding, TalkRegisters.Camfranglais),
                    L("pg", TalkTriggers.Boarding, TalkRegisters.Pidgin),
                    L("neutre", TalkTriggers.Paid, ""),
                },
            };
            var d = new DialogueDirector(config, new Random(1));
            d.AssignRegister("p", TalkRegisters.Pidgin);
            for (var i = 0; i < 20; i++)
            {
                Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "p"));
                Assert.AreEqual("pg", d.Current.Line.id, "Un passager pidgin ne dit que ses répliques pidgin.");
                Assert.AreEqual(TalkRegisters.Pidgin, d.Current.Register);
                Wait(d, 10f);
            }
            Assert.IsTrue(d.Trigger(TalkTriggers.Paid, "p"));
            Assert.AreEqual("neutre", d.Current.Line.id, "Une réplique sans registre convient à tous.");
            Wait(d, 10f);
            Assert.AreEqual(TalkRegisters.Pidgin, d.RegisterOf("p"));
            d.AssignRegister("p", "");
            Assert.AreEqual(DialogueDirector.RegisterFor(config, "p"), d.RegisterOf("p"), "Sans registre imposé : celui tiré pour lui.");
        }

        [Test]
        public void Register_FallsBackToFrench_OrStaysSilent()
        {
            var config = new DialogueConfig
            {
                enabled = true,
                minGapSeconds = 0f,
                noRepeatWithin = 0,
                lines = new[] { L("fr", TalkTriggers.Detour, TalkRegisters.French) },
            };
            var d = new DialogueDirector(config, new Random(1));
            d.AssignRegister("p", TalkRegisters.Camfranglais);
            Assert.IsTrue(d.Trigger(TalkTriggers.Detour, "p"), "Pas de réplique camfranglais : il parle français (registre de secours).");
            Assert.AreEqual("fr", d.Current.Line.id);
            Wait(d, 10f);
            config.fallbackRegister = "";
            Assert.IsFalse(d.Trigger(TalkTriggers.Detour, "p"), "Sans registre de secours : il se tait.");
        }

        [Test]
        public void Register_ImposedByCharacter_AndAnyRegisterInGroupChat()
        {
            var config = new DialogueConfig
            {
                enabled = true,
                minGapSeconds = 0f,
                noRepeatWithin = 0,
                characters = new[] { new CharacterData { id = "anglo", name = "exemple", register = TalkRegisters.Pidgin } },
                lines = new[] { L("pg", TalkTriggers.Chat, TalkRegisters.Pidgin), L("fr", TalkTriggers.Boarding, TalkRegisters.French), L("pg2", TalkTriggers.Boarding, TalkRegisters.Pidgin) },
            };
            var d = new DialogueDirector(config, new Random(1));
            Assert.AreEqual(TalkRegisters.Pidgin, d.RegisterOf("x", "anglo"));
            Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "x", "anglo"));
            Assert.AreEqual("pg2", d.Current.Line.id);
            Wait(d, 10f);
            Assert.IsTrue(d.Trigger(TalkTriggers.Chat, ""), "Discussion sans passager désigné : tous les registres.");
            Assert.AreEqual("", d.Current.Register);
        }
        [Test]
        public void Values_FilledInText_LineSkippedWhenAValueIsMissing()
        {
            // 22/11 : lieux et sommes passent par {destination}, {prix}, {offre} pour ne jamais contredire le jeu.
            Assert.AreEqual(new List<string> { "prix", "destination" }, DialogueText.Placeholders("{prix} pour {destination} ?"));
            Assert.AreEqual("600 francs", TalkValues.Amount(600));
            Assert.AreEqual("1 200 francs", TalkValues.Amount(1200));
            var values = new Dictionary<string, string> { [TalkValues.Destination] = "Emana", [TalkValues.Price] = TalkValues.Amount(800) };
            Assert.AreEqual("800 francs pour Emana ?", DialogueText.Fill("{prix} pour {destination} ?", values));
            Assert.IsFalse(DialogueText.CanFill("Je te donne {offre}.", values));
            Assert.IsTrue(DialogueText.CanFill("Sans valeur.", null));
            // QA (22/11) : élision devant un lieu qui commence par une voyelle (Emana, Étoudi), comme dans les vraies répliques.
            Assert.AreEqual("c'est pas la route d'Emana, ça.", DialogueText.Fill("c'est pas la route de {destination}, ça.", values));
            Assert.AreEqual("Maman dit qu'Étoudi, c'est de l'autre côté.",
                DialogueText.Fill("Maman dit que {destination}, c'est de l'autre côté.", new Dictionary<string, string> { [TalkValues.Destination] = "Étoudi" }));
            Assert.AreEqual("On s'éloigne de Borne Fontaine.",
                DialogueText.Fill("On s'éloigne de {destination}.", new Dictionary<string, string> { [TalkValues.Destination] = "Borne Fontaine" }));
            Assert.AreEqual("Qu'Emana ?", DialogueText.Fill("Que {destination} ?", values));
            Assert.AreEqual("La route de Emanuel", DialogueText.Fill("La route de Emanuel", values), "Seulement devant une valeur remplie.");
            CollectionAssert.Contains(DialogueText.ValuesFor(TalkTriggers.Haggle), TalkValues.Offer);
            CollectionAssert.IsEmpty(DialogueText.ValuesFor(TalkTriggers.Rain));

            var config = new DialogueConfig
            {
                enabled = true,
                lines = new[]
                {
                    new LineData { id = "offre", trigger = TalkTriggers.Haggle, text = "Je te donne {offre}." },
                    new LineData { id = "prix", trigger = TalkTriggers.Price, text = "{prix} pour {destination} ? Bon." },
                },
            };
            var d = new DialogueDirector(config, new Random(1));
            Wait(d, 10f);
            Assert.IsFalse(d.Trigger(TalkTriggers.Haggle, "c1", "", values), "Sans contre-offre, la réplique n'est pas dite.");
            Assert.IsNull(d.Current);
            Assert.IsTrue(d.Trigger(TalkTriggers.Price, "c1", "", values));
            Assert.AreEqual("800 francs pour Emana ? Bon.", d.Current.Text);
            Assert.AreEqual(Math.Max(2.5f, d.Current.Text.Length / 14f), d.Current.ShowSeconds, 1e-4f, "Temps de lecture sur le texte rempli.");
        }
        [Test]
        public void Places_SpokenForms_FromDataOrName_CapitalizedAtSentenceStart()
        {
            // Décision de Kevin (23/11) : « je descends au dispensaire de Messassi », jamais « à Dispensaire de Messassi ».
            var dispensaire = new Rides.Stop
            {
                id = "dispensaire", name = "Dispensaire de Messassi",
                spoken = new Rides.SpokenPlace { name = "le dispensaire de Messassi", at = "au dispensaire de Messassi", of = "du dispensaire de Messassi", bare = "Dispensaire Messassi" },
            };
            var v = TalkValues.ForRide(dispensaire, dispensaire.id, 600);
            Assert.AreEqual("Je descends au dispensaire de Messassi.", DialogueText.Fill("Je descends {a_destination}.", v));
            Assert.AreEqual("C'est pas la route du dispensaire de Messassi.", DialogueText.Fill("C'est pas la route {de_destination}.", v));
            Assert.AreEqual("Le dispensaire de Messassi, s'il vous plaît.", DialogueText.Fill("{destination}, s'il vous plaît.", v));
            Assert.AreEqual("Eh ! Au dispensaire de Messassi, vite.", DialogueText.Fill("Eh ! {a_destination}, vite.", v));
            Assert.AreEqual("600 francs pour le dispensaire de Messassi ?", DialogueText.Fill("{prix} pour {destination} ?", v));
            Assert.AreEqual("Drop me for Dispensaire Messassi.", DialogueText.Fill("Drop me for {destination_nue}.", v));
            // Sans forme parlée dans les données : tirée du nom court, avec l'élision.
            var etoudi = TalkValues.PlaceForms(new Rides.Stop { id = "etoudi", name = "Étoudi (devant le palais)" });
            Assert.AreEqual("Étoudi", etoudi.name);
            Assert.AreEqual("à Étoudi", etoudi.at);
            Assert.AreEqual("d'Étoudi", etoudi.of);
            Assert.AreEqual("de Borne Fontaine", TalkValues.PlaceForms(new Rides.Stop { name = "Borne Fontaine" }).of);
            Assert.AreEqual("à Emana", TalkValues.PlaceForms(null, "Emana").at);
            CollectionAssert.IsEmpty(TalkValues.ForRide(null, "", 0), "Lieu inconnu : aucune valeur, la réplique n'est pas dite.");
            CollectionAssert.IsSupersetOf(DialogueText.ValuesFor(TalkTriggers.Detour), TalkValues.Place);
        }

        [Test]
        public void FollowUp_DestinationAfterBoarding_DroppedWhenLateOrPassengerGone()
        {
            // 22/11 : la situation « destination » n'était jamais déclenchée ; elle suit maintenant la montée.
            var config = Config(3, TalkTriggers.Boarding, TalkTriggers.Destination);
            var d = new DialogueDirector(config, new Random(2));
            d.SetPassengers(new[] { "c1" });
            Wait(d, 10f);
            Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "c1"));
            d.Follow(TalkTriggers.Destination, "c1");
            Assert.AreEqual(1, d.PendingFollowUps);
            for (var t = 0f; t < 15f && (d.Current == null || d.Current.Line.trigger != TalkTriggers.Destination); t += 0.1f) d.Update(0.1f, 1);
            Assert.AreEqual(TalkTriggers.Destination, d.Current?.Line.trigger, "La destination suit la montée, après le silence.");
            Assert.AreEqual(0, d.PendingFollowUps);

            // Trop tard : abandonnée.
            Wait(d, 20f);
            d.Follow(TalkTriggers.Destination, "c1", null, 1f);
            Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "c1"));
            Wait(d, 30f);
            Assert.AreEqual(0, d.PendingFollowUps);

            // Passager descendu : abandonnée.
            Wait(d, 20f);
            Assert.IsTrue(d.Trigger(TalkTriggers.Boarding, "c1"));
            d.Follow(TalkTriggers.Destination, "c1");
            d.SetPassengers(new string[0]);
            d.Update(0.1f, 0);
            Assert.AreEqual(0, d.PendingFollowUps);
        }
    }
}
