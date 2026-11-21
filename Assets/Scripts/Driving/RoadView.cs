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
    /// <see cref="RoadDay"/>. Créé par <see cref="StreetView"/> seulement si road.json a enabled = true. Les accrochages
    /// avec le trafic (fuite, poursuite) sont prêts dans la logique mais pas encore branchés. Non exécuté dans Unity.
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

        private readonly Image _spare, _mechanic;
        private readonly Text _spareText, _mechanicText;
        private float _lastAlong = -1f;

        public RoadView(RectTransform overlay, Font font, RoadConfig config, float routeLength, float damage, int startMinute, int endMinute, int month, int seed)
        {
            Road = new RoadDay(config, routeLength, 20261121, damage, startMinute, endMinute, month, new System.Random(seed));
            _spare = UiKit.NewButton(overlay, font, "", new Color(0.3f, 0.3f, 0.35f, 0.95f), out _spareText);
            _mechanic = UiKit.NewButton(overlay, font, "", new Color(0.12f, 0.55f, 0.25f, 0.95f), out _mechanicText);
            _spare.gameObject.SetActive(false);
            _mechanic.gameObject.SetActive(false);
        }

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
            return message;
        }
    }
}
