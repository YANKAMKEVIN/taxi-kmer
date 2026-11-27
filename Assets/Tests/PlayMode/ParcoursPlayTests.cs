using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;
using TaxiKmer.Core.World;
using TaxiKmer.Driving;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TaxiKmer.PlayModeTests
{
    /// <summary>
    /// Parcours complet joué par un pilote automatique, dans la vraie scène et avec la vraie physique : il conduit le
    /// taxi le long de l'itinéraire (aller puis retour), klaxonne les clients qui appellent, s'arrête, les fait monter
    /// et descendre, fait le plein, répond à la police, aux accrochages, aux pannes, au marchandage et aux passagers,
    /// change de station de radio, puis termine la journée, passe le bilan et vérifie que le jour suivant démarre.
    /// Toute erreur dans la console fait échouer. Des captures d'écran de chaque situation nouvelle sont écrites dans
    /// Logs/parcours/ (relues à la main pour la direction artistique et l'ergonomie).
    /// </summary>
    public class ParcoursPlayTests
    {
        private const float CruiseKmh = 30f;
        private const float LaneOffset = 1.8f;
        private string _saveDir;
        private string _shots;
        private readonly HashSet<string> _seen = new HashSet<string>();
        private readonly List<string> _journal = new List<string>();

        [SetUp]
        public void Isolate()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "taxikmer-parcours-" + System.Guid.NewGuid());
            SaveService.DirectoryForTests = _saveDir;
            _shots = Path.Combine(Application.dataPath, "..", "Logs", "parcours");
            Directory.CreateDirectory(_shots);
            foreach (var f in Directory.GetFiles(_shots, "*.png")) File.Delete(f);
            StreetView.TitleDone = false;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            BotInput.Clear();
            StreetView.TitleDone = false;
            SaveService.DirectoryForTests = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
            File.WriteAllLines(Path.Combine(_shots, "journal.txt"), _journal);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene("Vide " + System.Guid.NewGuid());
            SceneManager.SetActiveScene(empty);
            var monde = SceneManager.GetSceneByName("Monde");
            if (monde.isLoaded) yield return SceneManager.UnloadSceneAsync(monde);
        }

        [UnityTest, Timeout(900000), Category("Parcours")]
        public IEnumerator Parcours_UneJourneeComplete_PuisLeJourSuivant()
        {
            SceneManager.LoadScene("Monde");
            yield return Capture();
            yield return new WaitForSeconds(0.5f);
            var view = Object.FindFirstObjectByType<StreetView>();
            var taxi = Object.FindFirstObjectByType<TaxiCar>();
            var controls = Object.FindFirstObjectByType<DriverControls>();
            Shot("00_ecran_titre");
            yield return Capture();

            // Écran titre : « Commencer » (même touche que le bouton contextuel).
            BotInput.Press(KeyAction.Action);
            for (var t = 0f; t < 3f && view.MenuFrozen; t += Time.unscaledDeltaTime) yield return Capture();
            Assert.IsFalse(view.MenuFrozen, "L'écran titre se ferme avec « Commencer »");
            Shot("01_au_volant");
            yield return Capture();

            var street = view.Street;
            var day = view.Save.Career.DayNumber;
            var xz = taxi.world.Data.route.xz;
            var cum = WorldGeometry.CumulativeLengths(xz);
            var length = view.RouteLength;
            var direction = 1;
            var nextReply = 0f;
            var nextRadio = 20f;
            var nextShot = 0f;
            var blockedFor = 0f;
            var blockedTimes = 0;
            var blockedAt = -1f;
            var ignoreObstaclesUntil = 0f;
            var stuckFor = 0f;
            var lastAlong = 0f;
            var drops = 0;
            var boards = 0;
            var lastPax = 0;
            var stops = street.Shift.Cabin; // pour la lisibilité ci-dessous

            Time.timeScale = 4f;
            // Conduite pendant une bonne partie de la journée (temps réel accéléré), puis fin de journée forcée.
            var driveUntilGameMinute = street.Shift.Clock.GameMinutes + 240;
            var realStart = Time.realtimeSinceStartup;
            while (!street.DayFinished && Time.realtimeSinceStartup - realStart < 420f)
            {
                var s = view.TaxiOnRouteNow();
                var pending = view.Pending;
                Journal(s, pending, view);

                // 1) Les choix qui attendent passent avant la conduite.
                if (pending.PoliceControl) { Hold(false, false, false, true); Press(KeyAction.Police1, "police"); }
                else if (pending.Crash) Press(KeyAction.CrashYes, "accrochage");
                else if (pending.Haggle) Press(KeyAction.HaggleAccept, "marchandage");
                else if (pending.Refusal) Press(KeyAction.RefusalLetGo, "refus_de_payer");
                else if (pending.DryTank) Press(KeyAction.PushTaxi, "panne_seche");
                else if (pending.FlatTyre) Press(KeyAction.SpareWheel, "crevaison");
                else if (pending.BrokenDown) Press(KeyAction.CallMechanic, "panne");
                if (view.TalkView != null && view.TalkView.Speaking && Time.time > nextReply)
                {
                    if (_seen.Add("dialogue_" + view.TalkView.Director.Current.Line.id)) Shot("dialogue_" + view.TalkView.Director.Current.Line.id);
                    BotInput.Press(KeyAction.Reply1);
                    nextReply = Time.time + 3f;
                }
                if (view.RadioView != null && Time.time > nextRadio)
                {
                    BotInput.Press(KeyAction.Radio);
                    nextRadio = Time.time + 60f;
                    if (_seen.Add("radio")) Shot("radio");
                }

                // 2) Où s'arrêter : destination d'un passager, client accepté, station si le réservoir baisse.
                var stopAt = float.NaN;
                foreach (var p in street.Shift.Cabin.Passengers)
                {
                    var d = street.FindStop(p.DestinationStopId).distanceAlongRouteMeters;
                    if ((d - s.Along) * direction > -5f) stopAt = Nearest(stopAt, d, s.Along);
                }
                foreach (var c in street.Waiting)
                {
                    if (c.State == RoadsideState.Accepted && (c.Along - s.Along) * direction > -5f && c.Direction == direction)
                        stopAt = Nearest(stopAt, c.Along, s.Along);
                    else if (c.Calling && Mathf.Abs(c.Along - s.Along) < 70f && c.Direction == direction && _honkAt < Time.time)
                    {
                        BotInput.Press(KeyAction.Horn);
                        _honkAt = Time.time + 1.5f;
                    }
                }
                var tank = street.Shift.Tank;
                if (tank != null && tank.Fraction < 0.35f)
                    foreach (var st in tank.Config.stations)
                        if ((st.distanceAlongRouteMeters - s.Along) * direction > -5f) stopAt = Nearest(stopAt, st.distanceAlongRouteMeters, s.Along);

                // 3) Arrêté au bon endroit : le bouton contextuel.
                if (controls.ActionVisible && taxi.SpeedKmh < 1f && !pending.Any)
                {
                    var label = controls.ActionLabel;
                    Press(KeyAction.Action, "action_" + label.Split(' ')[0]);
                    if (label.StartsWith("Faire descendre")) drops++;
                    if (label.Contains("monter")) boards++;
                }

                // 4) Conduite : direction vers un point devant, vitesse vers la cible.
                var target = CruiseKmh;
                if (!float.IsNaN(stopAt))
                {
                    var dist = Mathf.Abs(stopAt - s.Along);
                    target = dist < 8f ? 0f : Mathf.Min(CruiseKmh, 3f * Mathf.Sqrt(dist - 8f) + 2f);
                }
                if (controls.Locked || pending.Any) target = 0f;
                var obstacle = Time.time < ignoreObstaclesUntil ? float.PositiveInfinity : ObstacleAhead(taxi);
                blockedFor = obstacle < 25f && taxi.SpeedKmh < 1f ? blockedFor + Time.deltaTime : 0f;
                if (blockedFor > 10f)
                {
                    blockedTimes = Mathf.Abs(s.Along - blockedAt) < 3f ? blockedTimes + 1 : 1;
                    blockedAt = s.Along;
                    if (blockedTimes >= 2)
                    {
                        // Coincé pour de bon (un véhicule du trafic collé au taxi, le muret) : le pilote « contourne » en
                        // reposant le taxi un peu plus loin. Noté au journal : c'est peut-être un défaut du trafic.
                        _journal.Add($"{street.Shift.Clock.Display} à {s.Along:0} m : coincé ({_lastObstacle}), taxi reposé 20 m plus loin");
                        taxi.PlaceOnRoute(Mathf.Clamp(s.Along + direction * 20f, 50f, length - 50f), direction);
                        blockedTimes = 0;
                    }
                    else
                    {
                        _journal.Add($"{street.Shift.Clock.Display} à {s.Along:0} m : obstacle devant depuis 10 s ({_lastObstacle}), on avance quand même");
                        ignoreObstaclesUntil = Time.time + 3f;
                    }
                    blockedFor = 0f;
                }
                if (Mathf.Abs(s.Lateral) > 7f && taxi.SpeedKmh < 2f && stuckFor > 3f) BotInput.Press(KeyAction.ResetCar); // sorti de la route
                if (obstacle < 25f) target = Mathf.Min(target, Mathf.Max(0f, (obstacle - 5f) * 1.5f));
                Steer(taxi, xz, cum, s.Along, direction, target);

                // 5) Bout de l'itinéraire : demi-tour (le taxi est reposé dans l'autre sens).
                if ((direction > 0 && s.Along > length - 40f) || (direction < 0 && s.Along < 40f))
                {
                    if (street.Shift.Cabin.PassengerCount == 0 || stuckFor > 20f)
                    {
                        direction = -direction;
                        taxi.PlaceOnRoute(Mathf.Clamp(s.Along, 50f, length - 50f), direction);
                        _journal.Add($"{street.Shift.Clock.Display} demi-tour → sens {direction}");
                    }
                }

                // Bloqué (contre un obstacle) : on remet le taxi sur la route, comme le bouton du joueur.
                stuckFor = Mathf.Abs(s.Along - lastAlong) < 0.05f && (target > 0f || Mathf.Abs(s.Lateral) > 7f) ? stuckFor + Time.deltaTime : 0f;
                lastAlong = s.Along;
                if (stuckFor > 8f)
                {
                    BotInput.Press(KeyAction.ResetCar);
                    _journal.Add($"{street.Shift.Clock.Display} bloqué à {s.Along:0} m : taxi remis sur la route");
                    stuckFor = 0f;
                }

                if (street.Shift.Cabin.PassengerCount != lastPax)
                {
                    _journal.Add($"{street.Shift.Clock.Display} à {s.Along:0} m : {street.Shift.Cabin.PassengerCount} passager(s), recette {street.Shift.Ledger.Revenue}");
                    lastPax = street.Shift.Cabin.PassengerCount;
                }
                if (Time.time > nextShot) { Shot($"route_{street.Shift.Clock.Display}"); nextShot = Time.time + 120f; }
                if (street.Shift.Clock.GameMinutes > driveUntilGameMinute && !street.Shift.Clock.IsOver)
                {
                    street.Shift.Clock.Advance(1e6f);
                    _journal.Add("Fin de journée avancée par le test");
                }
                yield return Capture();
            }
            Hold(false, false, false, false);
            Time.timeScale = 1f;
            _journal.Add($"Fin de boucle : {street.Shift.Clock.Display} (minute {street.Shift.Clock.GameMinutes}, finie={street.Shift.Clock.IsOver}), " +
                         $"bilan={street.DayFinished} (arrêté={street.Summary?.Arrested}), passagers={street.Shift.Cabin.PassengerCount}, temps réel {Time.realtimeSinceStartup - realStart:0} s");

            _journal.Insert(0, $"Montées {boards}, descentes {drops}, recette {street.Shift.Ledger.Revenue} FCFA, " +
                               $"{street.Shift.Ledger.MetersDriven / 1000f:0.0} km, situations vues : {string.Join(", ", _seen.OrderBy(x => x))}");
            Debug.Log($"PARCOURS écran {Screen.width}x{Screen.height} " + _journal[0]);
            Assert.IsTrue(street.DayFinished, "La journée se termine (bilan) en moins de 7 minutes réelles : " + _journal[0]);
            Assert.Greater(boards, 0, "Au moins un client monté : " + _journal[0]);
            Assert.Greater(street.Shift.Ledger.Revenue, 0, "Au moins une course payée : " + _journal[0]);

            // Bilan, puis jour suivant.
            for (var t = 0f; t < 3f && view.SummaryView == null; t += Time.deltaTime) yield return Capture();
            Assert.IsNotNull(view.SummaryView, "Écran de bilan");
            yield return new WaitForSeconds(6f); // animation du bilan
            Shot("90_bilan");
            yield return Capture();
            for (var i = 0; i < 6 && SceneManager.GetActiveScene().isLoaded && Object.FindFirstObjectByType<StreetView>() == view; i++)
            {
                BotInput.Press(KeyAction.Action);
                yield return new WaitForSeconds(1f);
            }
            var next = Object.FindFirstObjectByType<StreetView>();
            Assert.IsNotNull(next, "Le jour suivant recharge la scène");
            Assert.AreNotSame(view, next, "Nouvelle journée");
            Assert.AreEqual(day + 1, next.Save.Career.DayNumber, "Jour suivant");
            Assert.IsFalse(next.Street.DayFinished);
            Shot("91_jour_suivant");
            yield return Capture();
        }

        private float _honkAt;

        private static float Nearest(float current, float candidate, float along) =>
            float.IsNaN(current) || Mathf.Abs(candidate - along) < Mathf.Abs(current - along) ? candidate : current;

        private void Press(KeyAction action, string situation)
        {
            BotInput.Press(action);
            if (_seen.Add(situation)) Shot(situation);
        }

        private static string _lastObstacle = "";

        /// <summary>Distance (m) du premier obstacle devant le taxi (véhicule, piéton, mur), l'infini sinon.</summary>
        private static float ObstacleAhead(TaxiCar taxi)
        {
            var t = taxi.transform;
            var f = t.forward;
            f.y = 0f;
            f.Normalize();
            var origin = t.position + Vector3.up * 1.2f + f * 2.6f;
            var hits = Physics.SphereCastAll(origin, 1.5f, f, 30f, ~0, QueryTriggerInteraction.Collide);
            var best = float.PositiveInfinity;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(t) || h.normal.y > 0.7f) continue; // le taxi lui-même, le sol
                if (h.distance <= 0f && h.collider.bounds.size.magnitude > 15f) continue; // sol, trottoir, bâtiment déjà touché
                if (h.collider.isTrigger && h.collider.GetComponentInParent<PedestrianMarker>() == null) continue; // seuls les piétons comptent parmi les déclencheurs
                var local = t.InverseTransformPoint(h.point == Vector3.zero ? h.collider.bounds.center : h.point);
                var pedestrian = h.collider.GetComponentInParent<PedestrianMarker>() != null;
                if (Mathf.Abs(local.x) > (pedestrian ? 3f : 1.4f) || local.z < 0f) continue; // pas dans la voie du taxi (marge pour un piéton qui traverse)
                if (h.distance < best) { best = h.distance; _lastObstacle = $"{h.collider.name} ({h.collider.transform.root.name}) à {h.distance:0} m"; }
            }
            return best;
        }

        private static void Hold(bool left, bool right, bool accelerate, bool brake)
        {
            BotInput.Hold(KeyAction.Left, left);
            BotInput.Hold(KeyAction.Right, right);
            BotInput.Hold(KeyAction.Accelerate, accelerate);
            BotInput.Hold(KeyAction.Brake, brake);
        }

        private static void Steer(TaxiCar taxi, float[] xz, float[] cum, float along, int direction, float targetKmh)
        {
            var ahead = along + direction * Mathf.Max(12f, taxi.SpeedKmh * 0.6f);
            WorldGeometry.SampleAlong(xz, cum, ahead, out var x, out var z, out var dx, out var dz);
            if (direction < 0) { dx = -dx; dz = -dz; }
            var aim = new Vector3(x + dz * LaneOffset, 0f, z - dx * LaneOffset);
            var to = aim - taxi.transform.position;
            to.y = 0f;
            var f = taxi.transform.forward;
            f.y = 0f;
            var angle = Vector3.SignedAngle(f, to, Vector3.up);
            var speed = taxi.SpeedKmh;
            // Commande tout ou rien : par petits coups (une image sur deux) quand l'écart est faible, pour ne pas zigzaguer.
            var pulse = Mathf.Abs(angle) > 6f || Time.frameCount % 2 == 0;
            Hold(angle < -1.5f && pulse, angle > 1.5f && pulse, speed < targetKmh - 2f && targetKmh > 0f, speed > targetKmh + 3f || (targetKmh == 0f && speed > 0.5f));
        }

        private string _lastPendingKey = "";

        private void Journal(TaxiOnRoute s, PendingChoices p, StreetView view)
        {
            var key = $"{p.PoliceControl}{p.Crash}{p.Haggle}{p.Refusal}{p.DryTank}{p.FlatTyre}{p.BrokenDown}";
            if (key == _lastPendingKey) return;
            _lastPendingKey = key;
            var clock = view.Street.Shift.Clock.Display;
            _journal.Add($"{clock} à {s.Along:0} m : police={p.PoliceControl} accrochage={p.Crash} marchandage={p.Haggle} " +
                         $"refus={p.Refusal} sec={p.DryTank} crevé={p.FlatTyre} panne={p.BrokenDown}");
        }

        private string _pendingShot;

        /// <summary>Demande une capture ; elle est prise à la fin de l'image (<see cref="Capture"/>).</summary>
        private void Shot(string name)
        {
            var safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray());
            _pendingShot = Path.Combine(_shots, $"{_shotIndex++:00}_{safe}.png");
        }

        private int _shotIndex;

        /// <summary>
        /// Écrit la capture demandée, puis attend l'image suivante (à appeler par yield return). En batchmode,
        /// WaitForEndOfFrame ne vient jamais : la caméra est rendue dans une texture, les interfaces passées un instant
        /// en mode « caméra » pour y figurer.
        /// </summary>
        private IEnumerator Capture()
        {
            if (_pendingShot != null)
            {
                var path = _pendingShot;
                _pendingShot = null;
                var cam = Camera.main;
                if (cam != null)
                {
                    int w = Screen.width, h = Screen.height;
                    var rt = RenderTexture.GetTemporary(w, h, 24);
                    var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
                    foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.05f; }
                    Canvas.ForceUpdateCanvases();
                    var previous = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = previous;
                    foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
                    var active = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = active;
                    RenderTexture.ReleaseTemporary(rt);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.Destroy(tex);
                }
            }
            yield return null;
        }
    }
}
