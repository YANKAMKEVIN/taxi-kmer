// Cohérence de toutes les données (dotnet seulement : lit les fichiers JSON de Assets/Resources/Data).
// Chaque fichier : clés connues du code (une faute de frappe serait ignorée sans bruit par JsonUtility), bornes
// des valeurs, et invariants entre fichiers (carburant, arrêts, prix, versement).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Quality;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;

namespace TaxiKmer.Core.DotnetTests
{
    public class DataInvariantsTests
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { IncludeFields = true };

        private static string DataDir => Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");

        /// <summary>Dossier source des données (pour les .meta d'Unity) : tools/dotnet/bin/…/net8.0 → racine du dépôt.</summary>
        private static string SourceDataDir
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "Data"))) dir = dir.Parent;
                Assert.IsNotNull(dir, "Racine du dépôt introuvable.");
                return Path.Combine(dir.FullName, "Assets", "Resources", "Data");
            }
        }

        private static T Load<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(DataDir, name)), Opts);

        /// <summary>Chaque fichier de données et la classe qui le lit (null : lu hors de la logique pure, clés vérifiées à part).</summary>
        public static readonly Dictionary<string, Type> Files = new Dictionary<string, Type>
        {
            ["accidents.json"] = typeof(AccidentConfig),
            ["calendar.json"] = typeof(CalendarConfig),
            ["day.json"] = typeof(DayConfig),
            ["driving.json"] = typeof(TaxiKmer.Core.Driving.DriveConfig),
            ["fares.json"] = typeof(FareConfig),
            ["fuel.json"] = typeof(FuelConfig),
            ["owner.json"] = typeof(OwnerConfig),
            ["quality.json"] = typeof(QualityConfig),
            ["reputation.json"] = typeof(ReputationConfig),
            ["save.json"] = null,
            ["seating.json"] = typeof(SeatingConfig),
            ["stops_messassi_etoudi.json"] = typeof(StopsConfig),
            ["street.json"] = typeof(StreetConfig),
            ["traffic.json"] = typeof(CityConfig),
            ["world_messassi_etoudi.json"] = typeof(TaxiKmer.Core.World.WorldData),
            ["police.json"] = typeof(TaxiKmer.Core.Police.PoliceConfig),
            ["dialogues.json"] = typeof(TaxiKmer.Core.Talk.DialogueConfig),
            ["road.json"] = typeof(TaxiKmer.Core.Road.RoadConfig),
            ["home.json"] = typeof(TaxiKmer.Core.Home.HomeConfig),
            ["ride_types.json"] = typeof(RideTypesConfig),
            ["taxi_market.json"] = typeof(TaxiMarketConfig),
            ["radio.json"] = typeof(TaxiKmer.Core.Radio.RadioConfig),
            ["ambience.json"] = typeof(TaxiKmer.Core.Ux.AmbienceConfig),
            ["daylight.json"] = typeof(TaxiKmer.Core.Ux.DayLightConfig),
            ["minimap.json"] = typeof(TaxiKmer.Core.Ux.MiniMapConfig),
            ["phone.json"] = typeof(TaxiKmer.Core.Phone.PhoneConfig),
            ["rain.json"] = typeof(TaxiKmer.Core.Road.RainLookConfig),
            ["city_events.json"] = typeof(TaxiKmer.Core.Day.CityEventsConfig),
            ["buildings.json"] = typeof(TaxiKmer.Core.World.BuildingLookConfig),
            ["vendors.json"] = typeof(TaxiKmer.Core.Traffic.VendorsConfig),
            ["goals.json"] = typeof(TaxiKmer.Core.Day.DailyGoalsConfig),
        };

        [Test]
        public void EveryDataFile_IsKnown_HasAUnityMeta_AndParses()
        {
            var onDisk = Directory.GetFiles(SourceDataDir, "*.json").Select(Path.GetFileName).OrderBy(n => n).ToList();
            CollectionAssert.AreEquivalent(Files.Keys.OrderBy(n => n).ToList(), onDisk,
                "Nouveau fichier de données : l'ajouter à DataInvariantsTests.Files (et à ses invariants).");
            foreach (var name in onDisk)
            {
                Assert.IsTrue(File.Exists(Path.Combine(SourceDataDir, name + ".meta")), $"{name} : .meta Unity manquant");
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDir, name)));
                Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind, name);
            }
        }

        [Test]
        public void EveryKey_IsAFieldOfTheCode()
        {
            foreach (var pair in Files)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDir, pair.Key)));
                if (pair.Value == null)
                {
                    // save.json est lu par SaveService (côté Unity) : SaveConfig.
                    var known = new HashSet<string> { "_note", "autosaveSeconds", "saveOnPause", "resumeOnStart" };
                    foreach (var p in doc.RootElement.EnumerateObject()) Assert.IsTrue(known.Contains(p.Name), $"{pair.Key} : clé inconnue « {p.Name} »");
                    continue;
                }
                var unknown = new List<string>();
                CheckKeys(doc.RootElement, pair.Value, pair.Key, unknown);
                CollectionAssert.IsEmpty(unknown, "Clés ignorées par le jeu (faute de frappe ?)");
            }
        }

        private static void CheckKeys(JsonElement e, Type type, string where, List<string> unknown)
        {
            if (e.ValueKind == JsonValueKind.Array)
            {
                var item = type.IsArray ? type.GetElementType() : null;
                if (item == null) return;
                var i = 0;
                foreach (var x in e.EnumerateArray()) CheckKeys(x, item, $"{where}[{i++}]", unknown);
                return;
            }
            if (e.ValueKind != JsonValueKind.Object) return;
            foreach (var p in e.EnumerateObject())
            {
                if (p.Name.StartsWith("_")) continue; // _note : commentaire pour Kevin
                var field = type.GetField(p.Name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) { unknown.Add($"{where}.{p.Name}"); continue; }
                CheckKeys(p.Value, field.FieldType, $"{where}.{p.Name}", unknown);
            }
        }

        [Test]
        public void Day_Fares_Seating_AreSane()
        {
            var d = Load<DayConfig>("day.json");
            Assert.That(d.startHour, Is.InRange(0, 23));
            Assert.That(d.endHour, Is.InRange(d.startHour + 1, 24));
            Assert.Greater(d.dayLengthRealSeconds, 0f);
            Assert.Greater(d.versement, 0);
            Assert.Greater(d.fuelPricePerLitre, 0);
            Assert.Greater(d.fuelLitresPer100Km, 0f);

            var f = Load<FareConfig>("fares.json");
            Assert.IsNotEmpty(f.bands);
            Assert.GreaterOrEqual(f.longDistanceFare, f.bands[f.bands.Length - 1].fare);
            foreach (var b in f.bands) Assert.Greater(b.fare, 0);

            var s = Load<SeatingConfig>("seating.json");
            Assert.AreEqual(5, s.frontMaxPassengers + s.backMaxPassengers, "5 places (décision de Kevin) : 3 derrière, 2 devant.");
            Assert.AreEqual(2, s.frontMaxPassengers);
            Assert.LessOrEqual(s.costaudWidth + s.minceWidth, s.frontBenchWidth, "Devant : un costaud et un mince.");
            Assert.Greater(s.costaudWidth * 2, s.frontBenchWidth, "Devant : pas deux costauds.");
            Assert.LessOrEqual(s.normalWidth * 3, s.backBenchWidth, "Derrière : trois clients normaux.");
            Assert.That(s.minceWidth, Is.LessThan(s.normalWidth));
            Assert.That(s.normalWidth, Is.LessThan(s.costaudWidth));
        }

        [Test]
        public void Stops_Street_AreSane_AndEveryTripHasAFare()
        {
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            var fares = Load<FareConfig>("fares.json");
            var street = Load<StreetConfig>("street.json");
            Assert.AreEqual(stops.stops.Length, stops.stops.Select(x => x.id).Distinct().Count(), "Identifiants uniques.");
            foreach (var a in stops.stops)
            {
                Assert.IsNotEmpty(a.name, a.id);
                foreach (var b in stops.stops)
                {
                    if (a == b) continue;
                    var gap = Math.Abs(a.distanceAlongRouteMeters - b.distanceAlongRouteMeters);
                    Assert.Greater(gap, 2 * street.stopRadiusMeters, $"{a.id} et {b.id} : zones de dépose qui se chevauchent");
                    Assert.Greater(fares.FareFor((int)gap), 0);
                }
            }
            Assert.Greater(street.maxWaitingPerStop, 0);
            Assert.Greater(street.spawnIntervalSeconds, 0f);
            Assert.Less(street.boardDistanceMeters, street.announceDistanceMeters, "On entend le client avant de pouvoir le faire monter.");
            Assert.Less(street.boardDistanceMeters, street.missedDistanceMeters);
            Assert.Greater(street.acceptedPatienceSeconds, 0f);
            Assert.Greater(street.stoppedKmh, 0f);
        }

        [Test]
        public void Traffic_Quality_Accidents_AreSane()
        {
            var t = Load<CityConfig>("traffic.json");
            var v = t.vehicles;
            Assert.AreEqual(1f, v.car.share + v.taxi.share + v.moto.share, 1e-3f, "Les parts du trafic font 100 %.");
            Assert.That(v.competitorPickupChance, Is.InRange(0f, 1f));
            Assert.That(v.exitChance, Is.InRange(0f, 1f));
            Assert.That(v.motoRunsRedChance, Is.InRange(0f, 1f));
            Assert.Less(v.minSpawnDistanceMeters, v.activeRadiusMeters);
            Assert.Less(t.pedestrians.minSpawnDistanceMeters, t.pedestrians.activeRadiusMeters);
            Assert.LessOrEqual(t.pedestrians.walkSpeedMin, t.pedestrians.walkSpeedMax);
            foreach (var x in v.vehiclesPerKmByHour) Assert.GreaterOrEqual(x, 0f);
            foreach (var x in t.pedestrians.perKmBySideByHour) Assert.GreaterOrEqual(x, 0f);

            var q = Load<QualityConfig>("quality.json");
            Assert.AreEqual(3, q.levels.Length, "Bas, Moyen, Haut.");
            Assert.That(q.defaultLevel, Is.InRange(0, q.levels.Length - 1));
            foreach (var l in q.levels)
            {
                Assert.Less(l.fogStart, l.fogEnd, l.name);
                Assert.LessOrEqual(l.fogEnd, l.drawDistance, l.name);
                Assert.That(l.trafficScale, Is.InRange(0f, 1f), l.name);
                Assert.GreaterOrEqual(l.targetFrameRate, 30, l.name);
            }

            var a = Load<AccidentConfig>("accidents.json");
            Assert.Less(a.minImpactKmh, a.seriousImpactKmh);
            Assert.LessOrEqual(a.lightCareMin, a.lightCareMax);
            Assert.LessOrEqual(a.seriousCareMin, a.seriousCareMax);
            Assert.LessOrEqual(a.lightCareMax, a.seriousCareMin, "Un blessé grave coûte plus qu'un blessé léger.");
            Assert.That(a.seriousArrestChance, Is.InRange(0f, 1f));
            Assert.That(a.seizureChance, Is.InRange(0f, 1f));
        }

        [Test]
        public void Career_Calendar_Reputation_AreSane()
        {
            var o = Load<OwnerConfig>("owner.json");
            Assert.That(o.missesBeforeFired, Is.InRange(1, o.windowDays), "Le renvoi doit pouvoir arriver dans la fenêtre.");

            var c = Load<CalendarConfig>("calendar.json");
            Assert.That(c.firstWeekday, Is.InRange(0, c.weekdays.Length - 1));
            Assert.AreEqual(c.weekdays.Length, c.weekdays.Select(w => w.name).Distinct().Count());
            foreach (var w in c.weekdays)
            {
                Assert.Greater(w.clients, 0f, w.name);
                Assert.Greater(w.traffic, 0f, w.name);
            }

            var r = Load<ReputationConfig>("reputation.json");
            Assert.Less(r.min, r.max);
            for (var i = 1; i < r.levels.Length; i++) Assert.Greater(r.levels[i].min, r.levels[i - 1].min, "Paliers croissants.");
            Assert.LessOrEqual(r.levels[r.levels.Length - 1].min, r.max);
            Assert.Greater(r.ridePaid, 0);
            Assert.Less(r.acceptedClientGaveUp, 0);
            Assert.Less(r.pedestrianSerious, r.pedestrianLight);
        }

        /// <summary>Décisions de Kevin du 2026-11-21 : elles doivent rester vraies dans les données.</summary>
        [Test]
        public void KevinDecisions_2026_11_21_AreInTheData()
        {
            var d = Load<DayConfig>("day.json");
            Assert.That(d.dayLengthRealSeconds, Is.InRange(1800f, 2400f), "Piste B : journée de 30 à 40 min réelles.");
            var f = Load<FuelConfig>("fuel.json");
            Assert.AreEqual(8f, d.fuelLitresPer100Km, "Carburant : réalisme actuel gardé.");
            Assert.AreEqual(1f, f.consumptionScale, "Équilibrage (Kevin, 21/11) : carburant ×1, consommation réelle.");
            Assert.AreEqual(6000, d.versement, "Équilibrage (Kevin, 21/11) : versement de 6 000 FCFA.");
            var fares = Load<FareConfig>("fares.json");
            Assert.AreEqual(600, fares.bands.Min(b => b.fare), "Équilibrage (Kevin, 21/11) : prix ×2, de 600…");
            Assert.AreEqual(1600, fares.longDistanceFare, "… à 1 600 FCFA.");
            Assert.Greater(f.pushKmh, 0f, "Panne sèche : on peut pousser…");
            Assert.Less(f.pushKmh, 8f, "… très lentement.");
            Assert.Greater(f.technicianDelayMinutes, 0, "Le technicien arrive après un délai…");
            Assert.Greater(f.technicianFeeFcfa + f.technicianLitres * d.fuelPricePerLitre, 0f, "… et coûte un prix, carburant compris.");
            Assert.Greater(f.technicianLitres, 0f, "… puis on repart.");

            Assert.IsTrue(Load<ReputationConfig>("reputation.json").visible, "Réputation visible.");

            var c = Load<CalendarConfig>("calendar.json");
            var markets = c.weekdays.Where(w => w.market).Select(w => w.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "mercredi", "samedi" }, markets, "Marché le mercredi et le samedi.");
            var sunday = c.weekdays.Single(w => w.name == "dimanche");
            Assert.Less(sunday.clients, 1f, "Dimanche calme…");
            Assert.Greater(sunday.clients, 0f, "… mais travaillé.");
        }

        [Test]
        public void Police_Texts_RealisticTone_NoCrudeWords()
        {
            // Ton réaliste (décision de Kevin, 2026-11-21) : on suggère la corruption sans la montrer crûment.
            var p = Load<TaxiKmer.Core.Police.PoliceConfig>("police.json");
            foreach (var t in p.pretexts) Assert.IsFalse(TaxiKmer.Core.Police.PoliceTexts.IsCrude(t.label), t.label);
            foreach (var c in p.checkpoints) Assert.IsFalse(TaxiKmer.Core.Police.PoliceTexts.IsCrude(c.name), c.name);
            foreach (var paper in p.papers) Assert.IsFalse(TaxiKmer.Core.Police.PoliceTexts.IsCrude(paper.name), paper.name);
        }

        [Test]
        public void Police_MatchesDefaults_OnSinceKevinDecided()
        {
            var p = Load<TaxiKmer.Core.Police.PoliceConfig>("police.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Police.PoliceConfig(), Opts), JsonSerializer.Serialize(p, Opts));
            Assert.IsTrue(p.enabled, "Jalon 4 activé le 21/11 : ce qui reste est en valeurs par défaut (jalon-04.md).");
            Assert.IsFalse(p.contactsEnabled, "Contacts : crochet du jalon 7, coupé tant qu'il n'existe pas.");
            Assert.AreEqual(0, p.reputationPaid, "Payer ne change pas la réputation (Kevin, 21/11).");
            foreach (var o in p.contestOutcomes.Concat(p.callOutcomes)) Assert.AreEqual(0, o.reputation, $"{o.id} : refuser ou contester ne change pas la réputation");
            Assert.IsTrue(p.contestOutcomes.Any(o => o.id == "gagne" && o.requiresInvented && o.weight > 0f), "Contester : gagne parfois, seulement si inventé.");
            Assert.IsTrue(p.contestOutcomes.Any(o => o.impound && o.weight > 0f), "Contester : fourrière possible.");
            Assert.IsTrue(p.callOutcomes.Any(o => o.requiresContact && o.weight > 0f), "Appeler : marche avec un contact.");
            Assert.IsTrue(p.fleeEnabled && p.fleeOutcomes.Any(o => o.wanted) && p.fleeOutcomes.Any(o => o.impound), "Fuir : poursuite, fourrière ou recherché.");
            Assert.IsTrue(p.wantedOutcomes.Any(o => o.impound) && p.wantedOutcomes.Any(o => o.arrest), "Recherché : fourrière ou arrestation au prochain poste.");
            Assert.AreEqual(2, Load<OwnerConfig>("owner.json").impoundsBeforeFired, "Fourrière : renvoyé à la 2ᵉ fois.");
            Assert.IsTrue(p.autoRenewPapers, "Papiers renouvelés automatiquement à l'échéance, payés sur l'épargne (Kevin, 21/11).");
            foreach (var x in p.papers) Assert.GreaterOrEqual(x.renewalCost, 0, x.id);
            // Montants bas (Kevin, 22/11 : « rester vers 60 à 70 % ») : chaque papier qui expire coûte quelque chose, peu.
            foreach (var x in p.papers.Where(x => x.validityDays > 0))
                Assert.That(x.renewalCost, Is.InRange(1, 5000), $"{x.id} : montant bas (equilibrage.md, Papers_Report).");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            var end = stops.stops[stops.stops.Length - 1].distanceAlongRouteMeters;
            foreach (var c in p.checkpoints) Assert.That(c.distanceAlongRouteMeters, Is.InRange(0f, end), c.id);
            Assert.AreEqual(p.papers.Length, p.papers.Select(x => x.id).Distinct().Count());
            foreach (var x in p.papers) Assert.LessOrEqual(x.startDaysLeft, x.validityDays, x.id);
            foreach (var x in new[] { p.checkpointStopChance, p.flyingStopChance, p.inventChanceWhenInOrder, p.negotiateFailChance, p.negotiateDiscount })
                Assert.That(x, Is.InRange(0f, 1f));
            Assert.LessOrEqual(p.demandInventedMin, p.demandInventedMax);
            foreach (var o in p.contestOutcomes.Concat(p.callOutcomes)) Assert.GreaterOrEqual(o.weight, 0f, o.id);
            Assert.Greater(p.contestOutcomes.Sum(o => o.weight), 0f);
            Assert.Greater(p.callOutcomes.Sum(o => o.weight), 0f);
        }

        /// <summary>
        /// Jalon 5 activé le 21/11 : répliques proposées par Claude (statut « proposée »), personnages de Kevin, situations
        /// des jalons 3 à 6, trois langues, petits sons « à fournir », négociation et refus de payer branchés.
        /// </summary>
        [Test]
        public void Dialogues_Proposed_Readable_CoverKevinsCharactersAndSituations()
        {
            var d = Load<TaxiKmer.Core.Talk.DialogueConfig>("dialogues.json");
            Assert.IsTrue(d.enabled, "Jalon 5 activé (Kevin, 21/11).");
            Assert.That(d.haggleChance, Is.InRange(0.01f, 1f), "Négociation, option A : branchée.");
            Assert.That(d.refusesToPayChance, Is.InRange(0.001f, 1f), "Refus de payer, option C : branché.");
            Assert.That(d.refusalPolicePayChance, Is.InRange(0f, 1f));
            Assert.LessOrEqual(d.haggleDiscountMin, d.haggleDiscountMax);
            Assert.AreEqual(d.lines.Length, d.lines.Select(l => l.id).Distinct().Count());
            Assert.That(d.lines.Length, Is.InRange(185, 230), "Trois lots proposés (43 le 21/11, 42 puis 85 le 22/11 : ≈ 170 décidé par Kevin) et 25 discussions de plus (Kevin, 22/11).");
            foreach (var t in TaxiKmer.Core.Talk.TalkTriggers.All)
                Assert.GreaterOrEqual(d.lines.Count(l => l.trigger == t), 9, $"« {t} » : au moins 9 répliques (3e lot, 22/11)");
            // Discussions entre passagers : toutes les 45 s dès deux passagers, sans passager désigné ; ≈ 25 de plus (Kevin, 22/11).
            Assert.GreaterOrEqual(d.lines.Count(l => l.trigger == TaxiKmer.Core.Talk.TalkTriggers.Chat && string.IsNullOrEmpty(l.character)), 40,
                "Au moins 40 discussions sans personnage imposé (elles faisaient la moitié des répétitions)");
            var characters = new HashSet<string>(d.characters.Select(c => c.id));
            CollectionAssert.IsSupersetOf(characters, new[] { "commercante", "ecolier", "etudiant", "fonctionnaire", "maman-bebe", "professeur" },
                "Personnages de Kevin.");
            var voices = new HashSet<string>(d.voiceSounds.Select(v => v.id));
            foreach (var v in d.voiceSounds)
            {
                Assert.AreEqual("", v.clip, $"{v.id} : aucun son fourni (emplacement).");
                Assert.AreEqual("à fournir", v.statut, v.id);
            }
            foreach (var c in d.characters)
            {
                Assert.IsTrue(string.IsNullOrEmpty(c.voice) || voices.Contains(c.voice), $"{c.id} : son inconnu");
                Assert.IsTrue(string.IsNullOrEmpty(c.register) || TaxiKmer.Core.Talk.TalkRegisters.All.Contains(c.register), $"{c.id} : registre inconnu");
            }
            foreach (var l in d.lines)
            {
                CollectionAssert.Contains(TaxiKmer.Core.Talk.TalkTriggers.All, l.trigger, l.id);
                Assert.LessOrEqual(l.text.Length, d.maxChars, $"{l.id} : trop long pour être lu en conduisant");
                Assert.IsTrue(string.IsNullOrEmpty(l.character) || characters.Contains(l.character), $"{l.id} : personnage inconnu");
                Assert.IsTrue(l.statut == "proposée" || l.statut == "validée" || l.statut == "à revoir", $"{l.id} : statut « proposée » tant que Kevin n'a pas validé (page tools/texts)");
                Assert.IsTrue(l.traduction == "" || l.traduction == "à traduire", l.id);
                Assert.IsTrue(string.IsNullOrEmpty(l.voice) || voices.Contains(l.voice), $"{l.id} : son inconnu");
                Assert.IsTrue(TaxiKmer.Core.Talk.TalkRegisters.All.Contains(l.register) || l.register == "", $"{l.id} : registre inconnu « {l.register} »");
                StringAssert.DoesNotContain("exemple", l.text, $"{l.id} : plus d'exemple à remplacer");
                // Plage horaire (décision de Kevin, 23/11) : « HH:MM » lisible, et toute salutation liée à l'heure en a une.
                Assert.IsTrue(l.fromTime == "" || TaxiKmer.Core.Talk.LineData.ParseTime(l.fromTime) >= 0, $"{l.id} : fromTime « {l.fromTime} »");
                Assert.IsTrue(l.untilTime == "" || TaxiKmer.Core.Talk.LineData.ParseTime(l.untilTime) >= 0, $"{l.id} : untilTime « {l.untilTime} »");
                if (l.text.StartsWith("Bonjour") || l.text.StartsWith("Bonsoir") || l.text.Contains("ce matin") || l.text.Contains("ce soir"))
                    Assert.IsFalse(l.fromTime == "" && l.untilTime == "", $"{l.id} : réplique liée à l'heure sans plage horaire");
                foreach (var r in l.replies)
                {
                    Assert.That(r.reputation, Is.InRange(-3, 3), $"{l.id}/{r.id} : effet modéré");
                    // Pourboires rares et petits (décision de Kevin, 23/11) : 100 à 200 FCFA.
                    Assert.IsTrue(r.tip == 0 || (r.tip >= 100 && r.tip <= 200), $"{l.id}/{r.id} : pourboire {r.tip} hors de 100 à 200 FCFA");
                    Assert.LessOrEqual(r.text.Length, 40, $"{l.id}/{r.id} : bouton lisible");
                }
            }
            // Pourboires rares (décision de Kevin, 23/11) : quelques réponses en donnent, sur des répliques dites pendant le
            // trajet (une réponse au paiement arrive quand le passager est déjà descendu : pourboire perdu).
            var replies = d.lines.SelectMany(l => l.replies).Count();
            // Les histoires de passagers réguliers (idée n° 13) donnent au plus un pourboire chacune, en fin d'histoire :
            // rares par construction (une fois par carrière), comptés à part.
            var tipped = d.lines.Where(l => string.IsNullOrEmpty(l.arc)).SelectMany(l => l.replies.Where(r => r.tip > 0).Select(r => l)).ToList();
            Assert.That(tipped.Count, Is.InRange(4, 10), "Quelques réponses à pourboire");
            Assert.LessOrEqual(tipped.Count * 20, replies, "Pourboires rares : au plus une réponse sur vingt");
            foreach (var arc in d.arcs)
            {
                var arcLines = d.lines.Where(l => l.arc == arc.id).ToList();
                CollectionAssert.AreEquivalent(Enumerable.Range(1, arc.steps), arcLines.Select(l => l.arcStep), $"{arc.id} : une réplique par rencontre");
                Assert.That(arc.steps, Is.InRange(3, 5), arc.id);
                Assert.IsTrue(characters.Contains(arc.character), arc.id);
                Assert.IsTrue(arcLines.All(l => l.character == arc.character), $"{arc.id} : dites par son personnage");
                Assert.LessOrEqual(arcLines.Count(l => l.replies.Any(r => r.tip > 0)), 1, $"{arc.id} : un pourboire au plus");
            }
            foreach (var l in tipped)
                Assert.IsFalse(l.trigger == "paiement" || l.trigger == "refus-paiement" || l.trigger == "discussion" || l.trigger == "negociation",
                    $"{l.id} : pourboire jamais payé dans cette situation");
            foreach (var t in new[] { "montee", "destination", "prix", "negociation", "refus-paiement", "panne", "controle", "pluie", "silence" })
                Assert.IsTrue(d.lines.Any(l => l.trigger == t), $"Situation « {t} » couverte.");
            foreach (var c in characters)
                Assert.IsTrue(d.lines.Any(l => l.character == c), $"{c} : au moins une réplique");
            // Langues décidées par Kevin (2026-11-21) : français, camfranglais et pidgin.
            CollectionAssert.AreEquivalent(TaxiKmer.Core.Talk.TalkRegisters.All, d.registers.Select(r => r.id).ToArray());
            foreach (var r in d.registers) Assert.Greater(r.weight, 0f, $"{r.id} : chaque langue décidée est parlée par des passagers");
            Assert.IsTrue(string.IsNullOrEmpty(d.fallbackRegister) || TaxiKmer.Core.Talk.TalkRegisters.All.Contains(d.fallbackRegister));
            foreach (var reg in TaxiKmer.Core.Talk.TalkRegisters.All)
                Assert.GreaterOrEqual(d.lines.Count(l => l.register == reg && l.traduction == ""), 3, $"Au moins 3 répliques sûres en {reg}");
            Assert.Greater(d.minShowSeconds, 0f);
            Assert.Greater(d.charsPerSecond, 0f);
        }

        [Test]
        public void Dialogues_NeverContradictTheGame_PlacesAndAmountsAreValues()
        {
            // 22/11 : « Trois cents pour Emana ? » alors que la course coûte 600 à 1 600 (prix ×2, Kevin) et va peut-être à
            // Étoudi. Lieux et sommes passent par {destination}, {prix}, {offre}, fournis par la situation.
            var d = Load<TaxiKmer.Core.Talk.DialogueConfig>("dialogues.json");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            // Formes parlées les plus longues de tous les lieux (« au dispensaire de Messassi », décision de Kevin du 23/11).
            var longest = new Dictionary<string, string>
            {
                [TaxiKmer.Core.Talk.TalkValues.Price] = TaxiKmer.Core.Talk.TalkValues.Amount(9999),
                [TaxiKmer.Core.Talk.TalkValues.Offer] = TaxiKmer.Core.Talk.TalkValues.Amount(9999),
            };
            foreach (var st in stops.stops)
                foreach (var kv in TaxiKmer.Core.Talk.TalkValues.ForRide(st, st.id, 0))
                    if (!longest.TryGetValue(kv.Key, out var cur) || kv.Value.Length > cur.Length) longest[kv.Key] = kv.Value;
            foreach (var st in stops.stops)
            {
                var f = TaxiKmer.Core.Talk.TalkValues.PlaceForms(st);
                Assert.IsFalse(string.IsNullOrEmpty(st.spoken?.name) || string.IsNullOrEmpty(st.spoken.at) || string.IsNullOrEmpty(st.spoken.of) || string.IsNullOrEmpty(st.spoken.bare),
                    $"{st.id} : forme parlée complète dans les données (décision de Kevin du 23/11)");
                Assert.IsTrue(f.at.StartsWith("à ") || f.at.StartsWith("au ") || f.at.StartsWith("aux "), $"{st.id} : « {f.at} »");
                Assert.IsTrue(f.of.StartsWith("de ") || f.of.StartsWith("du ") || f.of.StartsWith("des ") || f.of.StartsWith("d'"), $"{st.id} : « {f.of} »");
            }
            var numberWords = new[] { "cent", "mille", "hundred", "thousand", "fcfa", "francs" };
            var places = stops.stops.Select(st => TaxiKmer.Core.Ux.StopNames.Short(st.name)).Where(n => n.Length >= 4).Distinct().ToArray();
            foreach (var l in d.lines)
            {
                var allowed = TaxiKmer.Core.Talk.DialogueText.ValuesFor(l.trigger);
                foreach (var name in TaxiKmer.Core.Talk.DialogueText.Placeholders(l.text))
                    CollectionAssert.Contains(allowed, name, $"{l.id} : « {{{name}}} » n'est pas fourni pour « {l.trigger} »");
                var bare = l.text;
                foreach (var name in TaxiKmer.Core.Talk.TalkValues.All) bare = bare.Replace("{" + name + "}", "");
                Assert.IsFalse(bare.Any(char.IsDigit), $"{l.id} : somme ou nombre écrit en dur");
                var words = bare.ToLowerInvariant().Split(new[] { ' ', ',', '.', '!', '?', '…', '\'', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in numberWords) Assert.IsFalse(words.Any(x => x == w || x == w + "s"), $"{l.id} : « {w} » écrit en dur, utiliser {{prix}} / {{offre}}");
                foreach (var place in places) StringAssert.DoesNotContain(place, bare, $"{l.id} : lieu écrit en dur, utiliser {{destination}}");
                Assert.LessOrEqual(TaxiKmer.Core.Talk.DialogueText.Fill(l.text, longest).Length, d.maxChars, $"{l.id} : trop long une fois rempli");
            }
        }

        /// <summary>
        /// Décision de Kevin (23/11, QA « à Dispensaire de Messassi ») : chaque lieu a une forme parlée dans les données
        /// (« au dispensaire de Messassi », « du pont d'Emana », « d'Étoudi ») et les répliques l'utilisent. Aucune réplique
        /// remplie, sur aucun lieu, ne donne « à Dispensaire », « de Le », « à Le », « for le », un article majuscule au
        /// milieu d'une phrase, ni « de Emana » sans élision.
        /// </summary>
        [Test]
        public void Dialogues_PlacesReadNaturally_OnEveryStop()
        {
            var d = Load<TaxiKmer.Core.Talk.DialogueConfig>("dialogues.json");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            var bad = new[]
            {
                new System.Text.RegularExpressions.Regex(@"(?<![\p{L}'])[àÀ] (le|la|les|Le|La|Les|L')(?![\p{L}])"),
                new System.Text.RegularExpressions.Regex(@"(?<![\p{L}'])[dD]e (le|les|Le|Les)(?![\p{L}])"),
                new System.Text.RegularExpressions.Regex(@"(?<![\p{L}'])([àÀ]|[dD]e|pour|vers|que|Que) (Dispensaire|Carrefour|Pont|Marché|Le|La|Les|Au|Du)(?![\p{L}])"),
                new System.Text.RegularExpressions.Regex(@"(?<![\p{L}'])(for|Na) (le|la|les|au|du|Le|La|Les|Au|Du)(?![\p{L}])"),
                new System.Text.RegularExpressions.Regex(@"(?<![\p{L}'])([dD]e|que|Que) [AEIOUYÉÈaeiouyéè]"),
                new System.Text.RegularExpressions.Regex(@"(?<![.!?…]) (Le|La|Les|Au|Aux|Du) [a-zé]"),
                new System.Text.RegularExpressions.Regex(@"(^|[.!?…] )(le|la|les|au|aux|du) "),
            };
            var errors = new List<string>();
            foreach (var stop in stops.stops)
            {
                var values = RideValues(stop);
                foreach (var l in d.lines)
                {
                    if (!TaxiKmer.Core.Talk.DialogueText.Placeholders(l.text).Any(n => n.Contains("destination"))) continue;
                    if (!TaxiKmer.Core.Talk.DialogueText.CanFill(l.text, values)) { errors.Add($"{l.id} / {stop.id} : valeur manquante"); continue; }
                    var text = TaxiKmer.Core.Talk.DialogueText.Fill(l.text, values);
                    foreach (var re in bad)
                        if (re.IsMatch(text)) { errors.Add($"{l.id} / {stop.id} : « {text} »"); break; }
                }
            }
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
        }

        /// <summary>Valeurs d'une course vers ce lieu, comme les passe le jeu (TalkView.RideValues).</summary>
        private static Dictionary<string, string> RideValues(Stop stop) => TaxiKmer.Core.Talk.TalkValues.ForRide(stop, stop.id, 600);

        [Test]
        public void Ambience_MatchesDefaults_MarketStopsExist()
        {
            var a = Load<TaxiKmer.Core.Ux.AmbienceConfig>("ambience.json");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            Assert.IsTrue(a.enabled);
            foreach (var m in a.marketStops) Assert.IsTrue(stops.stops.Any(st => st.id == m), $"Lieu à marché inconnu : {m}");
            foreach (var v in new[] { a.murmurVolume, a.marketVolume, a.marketDayVolume, a.duckVolume }) Assert.That(v, Is.InRange(0f, 1f));
            Assert.GreaterOrEqual(a.marketDayVolume, a.marketVolume, "Plus de monde les jours de marché.");
            Assert.That(a.maxHornsPerMinute, Is.InRange(1f, 30f), "Klaxons : de l'ambiance, pas un vacarme.");
            var d = new TaxiKmer.Core.Ux.AmbienceConfig();
            Assert.AreEqual(d.hornsPerMinutePerVehicle, a.hornsPerMinutePerVehicle, "Valeurs par défaut du code alignées sur les données.");
            Assert.AreEqual(d.clientCallMeters, a.clientCallMeters);
            Assert.AreEqual(d.marketRadiusMeters, a.marketRadiusMeters);
        }

        /// <summary>Vrais noms de stations et de marques à ne jamais reprendre (stations fictives, décision de Kevin du 21/11).</summary>
        private static readonly string[] RealRadioNames =
        {
            "crtv", "équinoxe", "equinoxe", "magic", "balafon", "siantou", "amplitude", "sweet fm", "nostalgie", "rfi", "africa n",
            "abk", "satellite", "royal fm", "tiemeni", "lord", "veritas", "bonne nouvelle", "radio france", "bbc", "voa", "trace",
        };

        /// <summary>
        /// Radio activée le 21/11 avec des pistes vides : stations fictives proposées, aucun son ni vrai nom, textes proposés
        /// dans les trois langues, flashs pour chaque événement du jeu, musique CC0 ou CC-BY seulement.
        /// </summary>
        [Test]
        public void Radio_On_FictionalStations_ProposedTexts_NoSound_FreeLicencesOnly()
        {
            var r = Load<TaxiKmer.Core.Radio.RadioConfig>("radio.json");
            Assert.IsTrue(r.enabled, "Radio activée avec des pistes vides (Kevin, 21/11).");
            Assert.IsTrue(r.skipSilentProgrammes, "Pistes vides : seulement les annonces et les flashs, en texte.");
            Assert.That(r.duckVolume, Is.InRange(0.05f, 0.9f), "Quand un passager parle, le volume baisse (pas de silence total).");
            Assert.That(r.volume, Is.InRange(0f, 1f));
            Assert.Greater(r.defaultProgramSeconds, 0f);
            Assert.That(r.stations.Length, Is.InRange(3, 4), "3 ou 4 stations proposées.");
            Assert.AreEqual(r.stations.Length, r.stations.Select(s => s.id).Distinct().Count());
            foreach (var s in r.stations)
            {
                Assert.AreEqual("proposé", s.statut, s.id);
                Assert.That(s.frequencyMHz, Is.InRange(87.5f, 108f), $"{s.id} : fréquence FM crédible");
                foreach (var real in RealRadioNames) StringAssert.DoesNotContain(real, s.name.ToLowerInvariant(), $"{s.id} : vrai nom de station");
                Assert.IsNotEmpty(s.programmes, s.id);
                Assert.AreEqual(s.programmes.Length, s.programmes.Select(p => p.id).Distinct().Count(), s.id);
                Assert.IsTrue(s.programmes.Any(p => p.text.Length > 0), $"{s.id} : au moins une annonce en texte");
                foreach (var p in s.programmes)
                {
                    CollectionAssert.Contains(new[] { "musique", "emission", "publicite", "flash" }, p.kind, p.id);
                    Assert.GreaterOrEqual(p.seconds, 0f);
                    if (p.text.Length > 0)
                    {
                        CollectionAssert.Contains(new[] { "proposée", "validée", "à revoir" }, p.statut, $"{s.id}/{p.id}");
                        CollectionAssert.Contains(TaxiKmer.Core.Talk.TalkRegisters.All, p.register, $"{s.id}/{p.id}");
                        Assert.IsTrue(p.traduction == "" || p.traduction == "à traduire", p.id);
                        Assert.LessOrEqual(p.text.Length, 110, $"{s.id}/{p.id} : lisible en conduisant");
                    }
                    if (p.clip.Length > 0)
                    {
                        CollectionAssert.Contains(new[] { "CC0", "CC-BY" }, p.licence, $"{s.id}/{p.id} : CC0 ou CC-BY seulement (radio.md)");
                        Assert.IsNotEmpty(p.credit, $"{s.id}/{p.id} : crédit obligatoire");
                        Assert.IsNotEmpty(p.source, $"{s.id}/{p.id} : source obligatoire");
                    }
                    else Assert.AreEqual("", p.licence, $"{s.id}/{p.id} : pas de licence sans son");
                }
            }
            var all = r.stations.SelectMany(s => s.programmes).Where(p => p.text.Length > 0).ToList();
            Assert.IsTrue(all.Any(p => p.kind == "emission") && all.Any(p => p.kind == "publicite"), "Animateurs et publicités.");
            foreach (var reg in TaxiKmer.Core.Talk.TalkRegisters.All)
                Assert.IsTrue(all.Any(p => p.register == reg && p.traduction == ""), $"Animateur ou publicité en {reg}");
            foreach (var e in TaxiKmer.Core.Radio.RadioEvents.All)
                Assert.IsTrue(r.flashes.Any(f => f.@event == e), $"Flash pour « {e} »");
            foreach (var f in r.flashes)
            {
                CollectionAssert.Contains(TaxiKmer.Core.Radio.RadioEvents.All, f.@event, f.id);
                CollectionAssert.Contains(new[] { "proposée", "validée", "à revoir" }, f.statut, f.id);
                Assert.IsFalse(TaxiKmer.Core.Police.PoliceTexts.IsCrude(f.text), $"{f.id} : ton réaliste");
            }
            // Aucun fichier son ni musique dans le dépôt.
            var assets = Path.Combine(Path.GetDirectoryName(SourceDataDir), "..");
            var audio = Directory.GetFiles(assets, "*.*", SearchOption.AllDirectories)
                .Where(f => new[] { ".mp3", ".ogg", ".wav", ".aiff", ".flac", ".m4a" }.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
            CollectionAssert.IsEmpty(audio, "Aucun son ajouté par Claude.");
        }

        [Test]
        public void Road_MatchesDefaults_On_KevinDecisions()
        {
            var r = Load<TaxiKmer.Core.Road.RoadConfig>("road.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Road.RoadConfig(), Opts), JsonSerializer.Serialize(r, Opts));
            Assert.IsTrue(r.enabled, "Jalon 6 activé le 21/11 (jalon-06.md).");
            Assert.AreEqual(1f, r.driverShareOfRepairs, "Le chauffeur paie les réparations (Kevin, 21/11).");
            Assert.AreEqual(12, r.rainChanceByMonth.Length, "Pluie selon les mois (saisons de Yaoundé).");
            foreach (var x in r.rainChanceByMonth) Assert.That(x, Is.InRange(0f, 1f));
            Assert.IsTrue(r.playerCanFlee, "Fuite du joueur : option B.");
            Assert.Less(r.playerFleeReputation, 0, "… avec perte de réputation.");
            Assert.That(r.playerFleeFoundChance, Is.InRange(0.01f, 1f), "… et risque d'être retrouvé.");
            Assert.That(r.chaseCatchChance, Is.InRange(0.01f, 1f), "Délit de fuite subi : on peut poursuivre.");
            foreach (var x in new[] { r.potholePunctureChance, r.potholeHitChance, r.rainChancePerDay, r.driverShareOfRepairs, r.rainGrip })
                Assert.That(x, Is.InRange(0f, 1f));
            Assert.LessOrEqual(r.rainMinMinutes, r.rainMaxMinutes);
            Assert.Less(r.flatMaxKmh, r.damagedMaxKmh);
            Assert.That(r.damagedThreshold, Is.InRange(0f, 100f));
            Assert.AreEqual(r.otherDriverAtFault.Length, r.otherDriverAtFault.Select(o => o.id).Distinct().Count());
            Assert.IsTrue(r.otherDriverAtFault.Any(o => o.id == "fuit"), "Délit de fuite : feuille de route.");
        }

        [Test]
        public void Home_MatchesDefaults_Off_NoAmountInvented()
        {
            var h = Load<TaxiKmer.Core.Home.HomeConfig>("home.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiKmer.Core.Home.HomeConfig(), Opts), JsonSerializer.Serialize(h, Opts));
            Assert.IsFalse(h.enabled);
            Assert.IsFalse(h.tontine.join);
            Assert.IsTrue(h.situations.Any(s => s.id == h.startSituation));
            foreach (var s in h.situations)
                foreach (var c in s.charges)
                {
                    Assert.AreEqual(0, c.amount, $"{s.id}/{c.id} : montant à fixer par Kevin (question 3 du jalon 7)");
                    Assert.GreaterOrEqual(c.everyDays, 1);
                }
            foreach (var e in h.lifeEvents) Assert.AreEqual(0f, e.chancePerDay, e.id);
        }

        [Test]
        public void RideTypes_MatchDefaults_OnlySharedUntilKevinDecides()
        {
            var r = Load<RideTypesConfig>("ride_types.json");
            Assert.AreEqual(JsonSerializer.Serialize(new RideTypesConfig(), Opts), JsonSerializer.Serialize(r, Opts));
            Assert.IsFalse(r.enabled);
            Assert.AreEqual(0f, r.app.commission, "Commission : question 2 du jalon 8.");
            Assert.AreEqual(0f, r.app.ordersPerHour);
            Assert.AreEqual(0f, r.depot.depotClientChance);
            Assert.AreEqual(Load<SeatingConfig>("seating.json").frontMaxPassengers + Load<SeatingConfig>("seating.json").backMaxPassengers, r.depot.seatsPaid,
                "Un dépôt paie toutes les places du taxi.");
        }

        [Test]
        public void TaxiMarket_MatchesDefaults_NoOfferInvented()
        {
            var m = Load<TaxiMarketConfig>("taxi_market.json");
            Assert.AreEqual(JsonSerializer.Serialize(new TaxiMarketConfig(), Opts), JsonSerializer.Serialize(m, Opts));
            Assert.IsFalse(m.enabled);
            Assert.IsEmpty(m.offers, "Modèles et prix : question 2 du jalon 9.");
            Assert.That(m.loan.minDownPayment, Is.InRange(0f, 1f));
            Assert.Greater(m.loan.days, 0);
        }

        [Test]
        public void Fuel_FitsTheRoute_AndTheVersement()
        {
            var f = Load<FuelConfig>("fuel.json");
            var d = Load<DayConfig>("day.json");
            var stops = Load<StopsConfig>("stops_messassi_etoudi.json");
            Assert.That(f.lowFraction, Is.InRange(0.01f, 0.99f));
            Assert.Greater(f.refuelStepFcfa, 0);
            Assert.Less(f.refuelStepFcfa, d.versement);
            Assert.AreEqual(f.stations.Length, f.stations.Select(s => s.id).Distinct().Count());
            var litresPerKm = d.fuelLitresPer100Km / 100f * f.consumptionScale;
            var route = stops.stops[stops.stops.Length - 1].distanceAlongRouteMeters / 1000f;
            Assert.Less(litresPerKm * route * 2f, f.tankLitres, "Un plein fait au moins un aller-retour.");
            var firstStation = f.stations.Min(s => s.distanceAlongRouteMeters) / 1000f;
            Assert.Less(litresPerKm * firstStation, f.startLitres, "Le réservoir de départ atteint la station.");
            Assert.Greater(f.refuelStepFcfa / (float)d.fuelPricePerLitre, 0.5f, "Un appui à la station achète de quoi rouler.");
        }
    }
}
