using System.Collections;
using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.Rides;
using TaxiKmer.Driving;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TaxiKmer.PlayModeTests
{
    /// <summary>
    /// La vraie scène Monde : monde construit, taxi au départ, clients au bord de la route, et une course complète
    /// (klaxon, montée, trajet, descente, paiement) jouée en déplaçant le taxi. Toute erreur dans la console fait échouer.
    /// </summary>
    public class MondeScenePlayTests
    {
        [UnityTearDown]
        public IEnumerator UnloadMonde()
        {
            // Scène vide pour les tests suivants (le taxi d'essai ne doit pas apparaître dans les bâtiments).
            var empty = SceneManager.CreateScene("Vide " + System.Guid.NewGuid());
            SceneManager.SetActiveScene(empty);
            var monde = SceneManager.GetSceneByName("Monde");
            if (monde.isLoaded) yield return SceneManager.UnloadSceneAsync(monde);
        }

        [UnityTest]
        public IEnumerator Monde_FullRide_FromDispensaire()
        {
            SceneManager.LoadScene("Monde");
            yield return null;
            yield return new WaitForSeconds(1f);

            var view = Object.FindFirstObjectByType<StreetView>();
            var taxi = Object.FindFirstObjectByType<TaxiCar>();
            Assert.IsNotNull(view, "StreetView dans la scène");
            Assert.IsNotNull(view.Street, "Clients créés");
            Assert.AreEqual(6 * 3, view.Street.Waiting.Count, "3 clients à chacun des 6 lieux");
            Assert.Less(taxi.transform.position.magnitude, 1e5f);

            // Au départ (dispensaire), les clients du dispensaire appellent le taxi qui part vers Étoudi.
            var street = view.Street;
            var state = new TaxiOnRoute { Along = taxi.startDistance, Direction = 1, Lateral = 2f, SpeedKmh = 0f };
            street.Update(0.1f, state);
            Assert.IsTrue(street.Waiting.Any(c => c.Calling), "Un client du dispensaire appelle");
            Assert.AreNotEqual(HonkResult.NobodyCalling, street.Honk(state, out var client));
            var boardAt = new TaxiOnRoute { Along = client.Along, Direction = 1, Lateral = 2f };
            Assert.AreEqual(BoardResult.Boarded, street.Perform(boardAt).Board);

            // On « téléporte » le taxi à la destination et on vérifie que la vue suit (repère, bouton contextuel).
            var dest = street.FindStop(client.Client.DestinationStopId);
            taxi.PlaceOnRoute(dest.distanceAlongRouteMeters);
            yield return new WaitForSeconds(1.5f);
            var controls = Object.FindFirstObjectByType<DriverControls>();
            Assert.IsTrue(controls.ActionVisible, "Bouton « Faire descendre » affiché à la destination");
            StringAssert.StartsWith("Faire descendre", controls.ActionLabel);
            var marker = GameObject.Find("Repère " + dest.name);
            Assert.IsNotNull(marker, "Repère de destination visible");

            var paid = street.Perform(new TaxiOnRoute { Along = dest.distanceAlongRouteMeters, Direction = 1, Lateral = 2f });
            Assert.AreEqual(client.Client.ProposedFare, paid.Paid);
            Assert.AreEqual(client.Client.ProposedFare, street.Shift.Ledger.Revenue);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Monde_EndOfDay_ShowsSummary_ThenNewDay()
        {
            SceneManager.LoadScene("Monde");
            yield return null;
            yield return new WaitForSeconds(0.5f);
            var view = Object.FindFirstObjectByType<StreetView>();
            var controls = Object.FindFirstObjectByType<DriverControls>();
            var ledger = view.Street.Shift.Ledger;
            ledger.AddFare(500);
            ledger.AddDistance(10000f);
            Assert.IsNull(view.SummaryView);

            view.Street.Shift.Clock.Advance(1e6f);
            yield return null;
            yield return null;
            Assert.IsTrue(view.Street.DayFinished, "Taxi vide à l'heure de fin : journée terminée");
            Assert.IsNotNull(view.SummaryView, "Écran de bilan affiché");
            Assert.IsTrue(view.SummaryView.Visible);
            Assert.IsTrue(controls.Locked, "Commandes bloquées pendant le bilan");
            Assert.IsNotNull(GameObject.Find("Bilan"));
            var s = view.Street.Summary;
            Assert.AreEqual(500, s.Revenue);
            Assert.AreEqual(s.Revenue - s.Versement - s.FuelCost, s.Remaining);
            var meters = ledger.MetersDriven;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(meters, ledger.MetersDriven, "Plus de distance comptée après le bilan");

            view.SummaryView.NewDay();
            yield return null;
            yield return new WaitForSeconds(0.5f);
            var fresh = Object.FindFirstObjectByType<StreetView>();
            Assert.AreNotSame(view, fresh);
            Assert.IsFalse(fresh.Street.DayFinished, "Nouvelle journée");
            Assert.AreEqual(0, fresh.Street.Shift.Ledger.Revenue);
            Assert.AreEqual("06:00", fresh.Street.Shift.Clock.Display);
        }

        [UnityTest]
        public IEnumerator Monde_QualityButton_CyclesLevels_AndKeepsChoice()
        {
            PlayerPrefs.DeleteKey(QualityView.PrefKey);
            SceneManager.LoadScene("Monde");
            yield return null;
            yield return null;
            var quality = Object.FindFirstObjectByType<QualityView>();
            Assert.IsNotNull(quality, "QualityView ajouté par les commandes");
            Assert.AreEqual("Moyen", quality.Current.name, "Moyen par défaut");
            Assert.AreEqual("Moyen", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Assert.AreEqual(quality.Current.drawDistance, Camera.main.farClipPlane, 0.01f);
            Assert.AreEqual(30, Application.targetFrameRate);

            quality.Apply(2, save: true);
            yield return null;
            Assert.AreEqual("Haut", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(600f, Camera.main.farClipPlane, 0.01f);
            StringAssert.StartsWith("Qualité : Haut", Object.FindFirstObjectByType<DriverControls>().QualityLabel);

            // Nouvelle partie : le choix est gardé.
            SceneManager.LoadScene("Monde");
            yield return null;
            yield return null;
            Assert.AreEqual("Haut", Object.FindFirstObjectByType<QualityView>().Current.name);
            PlayerPrefs.DeleteKey(QualityView.PrefKey);
        }
    }
}
