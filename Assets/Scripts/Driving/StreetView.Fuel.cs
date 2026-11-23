using System.Collections.Generic;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.Ux;
using TaxiKmer.Core.World;
using TaxiKmer.World;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>Carburant de la rue : jauge, panne sèche (pousser ou appeler le technicien), stations-service. Partie de <see cref="StreetView"/> (idée n° 33 : fichier découpé).</summary>
    public sealed partial class StreetView
    {
        /// <summary>Réservoir : vitesse limitée quand il est vide, messages de réserve et de panne sèche (une fois chacun).</summary>
        private void UpdateFuel(FuelTank tank)
        {
            if (tank == null) return;
            taxi.SpeedCapKmh = Mathf.Min(tank.SpeedCapKmh, _road != null ? _road.Road.SpeedCapKmh : float.PositiveInfinity);
            if (Street.DayFinished) return;
            if (!tank.IsLow) _lowFuelShown = _emptyFuelShown = false;
            if (tank.IsLow && !_lowFuelShown) { _lowFuelShown = true; Toast(FuelGauge.LowMessage, 5f); }
            if (tank.IsEmpty && !_emptyFuelShown)
            {
                _emptyFuelShown = true;
                Toast(FuelGauge.EmptyMessage, 7f);
                _talk?.Say(Core.Talk.TalkTriggers.Breakdown, TalkView.AnyPassenger(Street));
                Haptics.Play(HapticEvent.Refused);
            }
        }

        /// <summary>
        /// Panne sèche hors de la station : deux boutons au milieu de l'écran (le taxi est arrêté), « Pousser le taxi »
        /// (au pas) ou « Appeler un technicien (prix, délai) » ; touches U et T au clavier (KeyMap). Toute la règle est dans Core.
        /// </summary>
        private void UpdateDryTank(TaxiOnRoute state)
        {
            if (_pushButton == null || _technicianButton == null) return;
            var show = !_menu.Frozen && _summaryView == null && Street.DryTankChoice(state);
            _pushButton.gameObject.SetActive(show);
            _technicianButton.gameObject.SetActive(show);
            if (!show) return;

            var h = Screen.height;
            var bw = h * 0.6f;
            var bh = h * 0.11f;
            var y = h * 0.55f;
            UiKit.PlaceTopCenter(_pushButton.rectTransform, y, bw, bh);
            _pushButton.rectTransform.anchoredPosition = new Vector2(-bw * 0.55f, -y);
            UiKit.PlaceTopCenter(_technicianButton.rectTransform, y, bw, bh);
            _technicianButton.rectTransform.anchoredPosition = new Vector2(bw * 0.55f, -y);
            _pushText.fontSize = _technicianText.fontSize = Mathf.RoundToInt(h * 0.032f);
            _technicianText.text = FuelGauge.TechnicianLabel(Street.TechnicianQuote());

            if (UiKit.Tapped(_pushButton.rectTransform) || UiKit.KeyDown(KeyAction.PushTaxi))
            {
                if (Street.PushTaxi()) Toast(FuelGauge.PushingMessage, 5f);
            }
            else if (UiKit.Tapped(_technicianButton.rectTransform) || UiKit.KeyDown(KeyAction.CallTechnician))
            {
                if (Street.CallTechnician(out var call))
                {
                    _sounds.PlayCoins();
                    Haptics.Play(HapticEvent.Payment);
                    Toast(FuelGauge.TechnicianCame(call), 6f);
                }
            }
            var stillShown = Street.DryTankChoice(state);
            _pushButton.gameObject.SetActive(stillShown);
            _technicianButton.gameObject.SetActive(stillShown);
        }

        /// <summary>Station-service provisoire (auvent et pompe en primitives) à droite de la route dans le sens vers Étoudi.</summary>
        private void MakeStation(StationData station)
        {
            WorldGeometry.SampleAlong(_xz, _cum, station.distanceAlongRouteMeters, out var x, out var z, out var dx, out var dz);
            var right = new Vector3(dz, 0f, -dx);
            var root = new GameObject("Station " + station.name);
            root.transform.SetParent(transform, false);
            var at = new Vector3(x, 0f, z) + right * (sideOffset + 6f);
            root.transform.SetPositionAndRotation(OnGround(at.x, at.z), Quaternion.LookRotation(new Vector3(dx, 0f, dz), Vector3.up));
            var white = new Color(0.92f, 0.92f, 0.9f);
            var red = new Color(0.8f, 0.12f, 0.1f);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 4.2f, 0f), new Vector3(9f, 0.4f, 6f), white);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 4.55f, 0f), new Vector3(9.1f, 0.3f, 6.1f), red);
            foreach (var px in new[] { -3.5f, 3.5f })
                Part(root.transform, PrimitiveType.Cube, new Vector3(px, 2f, 0f), new Vector3(0.3f, 4f, 0.3f), white);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 1.6f, 0.5f), red);
            Part(root.transform, PrimitiveType.Cube, new Vector3(-5.5f, 3f, -2.5f), new Vector3(0.25f, 6f, 0.25f), white);
            Part(root.transform, PrimitiveType.Cube, new Vector3(-5.5f, 6.2f, -2.5f), new Vector3(1.6f, 1.2f, 0.2f), red);
        }
    }
}
