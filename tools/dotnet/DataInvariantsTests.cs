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
            Assert.AreEqual(8f, f.consumptionScale, "Carburant : réalisme actuel gardé.");
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
            Assert.That(d.lines.Length, Is.InRange(35, 60), "Une quarantaine de répliques de qualité.");
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
                Assert.IsTrue(l.statut == "proposée" || l.statut == "validée", $"{l.id} : statut « proposée » tant que Kevin n'a pas validé");
                Assert.IsTrue(l.traduction == "" || l.traduction == "à traduire", l.id);
                Assert.IsTrue(string.IsNullOrEmpty(l.voice) || voices.Contains(l.voice), $"{l.id} : son inconnu");
                Assert.IsTrue(TaxiKmer.Core.Talk.TalkRegisters.All.Contains(l.register) || l.register == "", $"{l.id} : registre inconnu « {l.register} »");
                StringAssert.DoesNotContain("exemple", l.text, $"{l.id} : plus d'exemple à remplacer");
                foreach (var r in l.replies)
                {
                    Assert.That(r.reputation, Is.InRange(-3, 3), $"{l.id}/{r.id} : effet modéré");
                    Assert.That(r.tip, Is.InRange(0, 500), $"{l.id}/{r.id}");
                    Assert.LessOrEqual(r.text.Length, 40, $"{l.id}/{r.id} : bouton lisible");
                }
            }
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
        public void Radio_Off_EmptySlots_ToBeProvidedByKevin()
        {
            var r = Load<TaxiKmer.Core.Radio.RadioConfig>("radio.json");
            Assert.IsFalse(r.enabled, "Coupée tant que Kevin n'a pas fourni stations et musiques (radio.md).");
            Assert.IsNotEmpty(r.stations);
            Assert.That(r.volume, Is.InRange(0f, 1f));
            Assert.Greater(r.defaultProgramSeconds, 0f);
            Assert.AreEqual(r.stations.Length, r.stations.Select(s => s.id).Distinct().Count());
            foreach (var s in r.stations)
            {
                StringAssert.Contains("à fournir par Kevin", s.name, "Noms des stations : question pour Kevin.");
                Assert.GreaterOrEqual(s.frequencyMHz, 0f);
                Assert.IsNotEmpty(s.programmes, s.id);
                Assert.AreEqual(s.programmes.Length, s.programmes.Select(p => p.id).Distinct().Count(), s.id);
                foreach (var p in s.programmes)
                {
                    Assert.AreEqual("", p.clip, $"{s.id}/{p.id} : aucun son fourni par Claude (emplacement vide).");
                    StringAssert.Contains("à fournir par Kevin", p.title);
                    CollectionAssert.Contains(new[] { "musique", "emission", "publicite", "flash" }, p.kind, p.id);
                    Assert.GreaterOrEqual(p.seconds, 0f);
                }
            }
            // Aucun fichier son ni musique dans le dépôt.
            var assets = Path.Combine(Path.GetDirectoryName(SourceDataDir), "..");
            var audio = Directory.GetFiles(assets, "*.*", SearchOption.AllDirectories)
                .Where(f => new[] { ".mp3", ".ogg", ".wav", ".aiff", ".flac", ".m4a" }.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
            CollectionAssert.IsEmpty(audio, "Les musiques sont à fournir par Kevin.");
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
