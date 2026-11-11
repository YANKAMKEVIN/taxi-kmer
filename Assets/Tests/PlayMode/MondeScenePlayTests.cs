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
    }
}
