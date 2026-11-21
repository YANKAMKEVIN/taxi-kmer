using TaxiKmer.Core.Day;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Road;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// La route est dure (jalon 6, activé le 21/11 ; affichage minimal) : nids-de-poule, crevaisons et pannes sur le trajet
    /// du taxi, pluie selon la saison (clients plus nombreux et moins patients, trafic plus dense), boutons « Changer la
    /// roue » (touche W) et « Appeler un mécanicien » (touche M) payés par le chauffeur. Toute la règle est dans
    /// <see cref="RoadDay"/>. Créé par <see cref="StreetView"/> seulement si road.json a enabled = true. Accrochages avec
    /// le trafic (<see cref="ReportCrash"/>, appelé par StreetView quand le taxi heurte un véhicule) : deux boutons au
    /// milieu de l'écran, « S'arrêter » / « Partir » (touches K et X) si le joueur est fautif, « Poursuivre » / « Laisser »
    /// si le chauffard fuit ; la règle est dans <see cref="TrafficCrash"/>. Non exécuté dans Unity.
    /// </summary>
    public sealed class RoadView
    {
        public RoadDay Road { get; }
        /// <summary>Il pleut en ce moment.</summary>
        public bool Raining { get; private set; }
        /// <summary>La pluie vient de commencer (cette image).</summary>
        public bool RainStarted { get; private set; }
        /// <summary>Le taxi vient de crever ou de tomber en panne (cette image).</summary>
        public bool IncidentStarted { get; private set; }

        /// <summary>Accrochages avec le trafic et choix en attente (jalon 6).</summary>
        public TrafficCrash Crash { get; }

        private readonly Image _spare, _mechanic, _crashYes, _crashNo;
        private readonly Text _spareText, _mechanicText, _crashYesText, _crashNoText;
        private float _lastAlong = -1f;

        public RoadView(RectTransform overlay, Font font, RoadConfig config, float routeLength, float damage, int startMinute, int endMinute, int month, int seed)
        {
            Road = new RoadDay(config, routeLength, 20261121, damage, startMinute, endMinute, month, new System.Random(seed));
            _spare = UiKit.NewButton(overlay, font, "", new Color(0.3f, 0.3f, 0.35f, 0.95f), out _spareText);
            _mechanic = UiKit.NewButton(overlay, font, "", new Color(0.12f, 0.55f, 0.25f, 0.95f), out _mechanicText);
            _spare.gameObject.SetActive(false);
            _mechanic.gameObject.SetActive(false);
            Crash = new TrafficCrash(config, new System.Random(seed + 1));
            _crashYes = UiKit.NewButton(overlay, font, "", new Color(0.12f, 0.55f, 0.25f, 0.95f), out _crashYesText);
            _crashNo = UiKit.NewButton(overlay, font, "", new Color(0.7f, 0.2f, 0.15f, 0.95f), out _crashNoText);
            _crashYes.gameObject.SetActive(false);
            _crashNo.gameObject.SetActive(false);
        }

        /// <summary>Le taxi vient de heurter un véhicule du trafic. Rend la phrase à afficher (vide si le choc ne compte pas).</summary>
        public string ReportCrash(Street street, int vehicleId, CrashJudgement crash) =>
            Crash.Report(street, Road.Car, vehicleId, crash, Time.time);

        /// <summary>À chaque image. Rend un message pour l'écran (vide sinon).</summary>
        public string Update(Street street, TaxiOnRoute state, bool driving)
        {
            RainStarted = false;
            IncidentStarted = false;
            var message = "";
            var shift = street.Shift;
            var minute = shift.Clock.GameMinutes;
            var raining = Road.Weather.RainingAt(minute);
            RainStarted = raining && !Raining;
            if (RainStarted) message = "Il commence à pleuvoir : la route glisse, les clients se pressent";
            else if (Raining && !raining) message = "La pluie s'arrête";
            Raining = raining;
            street.PatienceScale = Road.Weather.PatienceFactor(minute);

            if (driving && !street.DayFinished && _lastAlong >= 0f)
            {
                var e = Road.Drive(_lastAlong, state.Along, state.SpeedKmh);
                if (e.Punctured) { message = "Crevaison ! Change la roue (roue de secours)"; IncidentStarted = true; }
                if (e.BrokeDown) { message = "Panne : appelle un mécanicien du quartier"; IncidentStarted = true; }
            }
            _lastAlong = state.Along;

            var show = driving && !street.DayFinished;
            var flat = show && Road.Car.FlatTyre && !Road.Car.BrokenDown;
            var broken = show && Road.Car.BrokenDown;
            _spare.gameObject.SetActive(flat);
            _mechanic.gameObject.SetActive(broken);
            var h = Screen.height;
            if (flat)
            {
                UiKit.PlaceTopCenter(_spare.rectTransform, h * 0.42f, h * 0.6f, h * 0.1f);
                _spareText.fontSize = Mathf.RoundToInt(h * 0.03f);
                _spareText.text = "Changer la roue (20 min)";
                if (UiKit.Tapped(_spare.rectTransform) || UnityEngine.Input.GetKeyDown(KeyCode.W))
                    if (Road.FitSpareWheel(shift)) message = "Roue changée : on repart";
            }
            if (broken)
            {
                UiKit.PlaceTopCenter(_mechanic.rectTransform, h * 0.42f, h * 0.7f, h * 0.1f);
                _mechanicText.fontSize = Mathf.RoundToInt(h * 0.03f);
                _mechanicText.text = $"Appeler un mécanicien ({DaySummary.Money(Road.MechanicQuote())})";
                if (UiKit.Tapped(_mechanic.rectTransform) || UnityEngine.Input.GetKeyDown(KeyCode.M))
                {
                    var paid = Road.CallMechanic(shift);
                    if (paid > 0) message = $"Le mécanicien a réparé : {DaySummary.Money(paid)}";
                }
            }
            var crashSays = UpdateCrash(street, state, show);
            if (crashSays.Length > 0) message = crashSays;
            return message;
        }

        /// <summary>Choix après un accrochage : deux boutons côte à côte (touches K et X) ; sans réponse, la logique tranche.</summary>
        private string UpdateCrash(Street street, TaxiOnRoute state, bool show)
        {
            var pending = show ? Crash.Pending : CrashChoice.None;
            _crashYes.gameObject.SetActive(pending != CrashChoice.None);
            _crashNo.gameObject.SetActive(pending != CrashChoice.None);
            if (!show) return "";
            if (pending == CrashChoice.None) return "";

            var h = Screen.height;
            var bw = h * 0.5f;
            var bh = h * 0.1f;
            var y = h * 0.3f;
            UiKit.PlaceTopCenter(_crashYes.rectTransform, y, bw, bh);
            _crashYes.rectTransform.anchoredPosition = new Vector2(-bw * 0.55f, -y);
            UiKit.PlaceTopCenter(_crashNo.rectTransform, y, bw, bh);
            _crashNo.rectTransform.anchoredPosition = new Vector2(bw * 0.55f, -y);
            _crashYesText.fontSize = _crashNoText.fontSize = Mathf.RoundToInt(h * 0.03f);
            var atFault = pending == CrashChoice.StopOrFlee;
            _crashYesText.text = atFault ? "S'arrêter et payer" : "Poursuivre";
            _crashNoText.text = atFault ? "Partir" : "Laisser filer";

            var said = "";
            if (UiKit.Tapped(_crashYes.rectTransform) || UnityEngine.Input.GetKeyDown(KeyCode.K))
                said = atFault ? Crash.Stop(street) : Crash.Chase(street);
            else if (UiKit.Tapped(_crashNo.rectTransform) || UnityEngine.Input.GetKeyDown(KeyCode.X))
                said = atFault ? Crash.Flee(street) : Crash.LetGo(street);
            else
                said = Crash.Tick(street, Time.deltaTime, state.SpeedKmh);
            var still = Crash.Pending != CrashChoice.None;
            _crashYes.gameObject.SetActive(still);
            _crashNo.gameObject.SetActive(still);
            return said;
        }
    }
}
