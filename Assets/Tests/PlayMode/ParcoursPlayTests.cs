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
        private string _saveDir;
        private string _shots;
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
            PhoneResolution();
        }

        /// <summary>
        /// Rendu au format d'un téléphone en paysage (S24 : 19,5/9) plutôt qu'au 640x480 du batchmode, pour que les
        /// captures montrent la vraie mise en page. API de l'éditeur appelée par réflexion (assembly de jeu).
        /// </summary>
        private static void PhoneResolution()
        {
#if UNITY_EDITOR
            var type = System.Type.GetType("UnityEditor.PlayModeWindow, UnityEditor");
            var set = type?.GetMethod("SetCustomRenderingResolution", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            set?.Invoke(null, new object[] { 1560u, 720u, "Téléphone paysage (tests)" });
#endif
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
            var pilot = view.gameObject.AddComponent<Autopilot>();
            pilot.Situation += Shot;
            Time.timeScale = 4f;
            // Conduite pendant une bonne partie de la journée (temps réel accéléré), puis fin de journée forcée.
            var driveUntilGameMinute = street.Shift.Clock.GameMinutes + 240;
            var realStart = Time.realtimeSinceStartup;
            var nextShot = 0f;
            while (!street.DayFinished && Time.realtimeSinceStartup - realStart < 420f)
            {
                if (Time.time > nextShot) { Shot($"route_{street.Shift.Clock.Display}"); nextShot = Time.time + 120f; }
                if (street.Shift.Clock.GameMinutes > driveUntilGameMinute && !street.Shift.Clock.IsOver)
                {
                    street.Shift.Clock.Advance(1e6f);
                    pilot.Journal.Add("Fin de journée avancée par le test");
                }
                yield return Capture();
            }
            Time.timeScale = 1f;
            _journal.AddRange(pilot.Journal);
            _journal.Add($"Fin de boucle : {street.Shift.Clock.Display} (minute {street.Shift.Clock.GameMinutes}, finie={street.Shift.Clock.IsOver}), " +
                         $"bilan={street.DayFinished} (arrêté={street.Summary?.Arrested}), passagers={street.Shift.Cabin.PassengerCount}, temps réel {Time.realtimeSinceStartup - realStart:0} s");
            _journal.Insert(0, pilot.Summary());
            Debug.Log($"PARCOURS écran {Screen.width}x{Screen.height} " + _journal[0]);
            Assert.IsTrue(street.DayFinished, "La journée se termine (bilan) en moins de 7 minutes réelles : " + _journal[0]);
            Assert.Greater(pilot.Boards, 0, "Au moins un client monté : " + _journal[0]);
            Assert.Greater(street.Shift.Ledger.Revenue, 0, "Au moins une course payée : " + _journal[0]);
            Object.Destroy(pilot);

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
