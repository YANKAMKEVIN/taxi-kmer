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
    /// <summary>Repères des destinations au-dessus de la route. Partie de <see cref="StreetView"/> (idée n° 33 : fichier découpé).</summary>
    public sealed partial class StreetView
    {
        private GameObject MakeStopMarker(Stop stop)
        {
            WorldGeometry.SampleAlong(_xz, _cum, stop.distanceAlongRouteMeters, out var x, out var z, out _, out _);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Repère " + stop.name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, 6f, z);
            go.transform.localScale = new Vector3(0.6f, 6f, 0.6f);
            if (markerMaterial != null) go.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;
            go.SetActive(false);
            return go;
        }

        private void SyncStopMarkers(float taxiAlong)
        {
            var shown = 0;
            foreach (var kv in _stopMarkers)
            {
                var count = Street.Shift.Cabin.PassengersFor(kv.Key).Count;
                kv.Value.SetActive(count > 0);
                if (count == 0 || Street.DayFinished) continue;
                ShowMarkerLabel(shown++, kv.Value.transform.position + Vector3.up * 6.8f,
                    WorldMarkers.DestinationLabel(Street.FindStop(kv.Key)?.name ?? kv.Key, count));
            }
            shown = SyncPolicePosts(shown, taxiAlong);
            for (var i = shown; i < _markerLabels.Count; i++) _markerLabels[i].gameObject.SetActive(false);
        }

        /// <summary>Nom du lieu et nombre de passagers qui y descendent, au-dessus de la colonne (texte d'écran).</summary>
        private void ShowMarkerLabel(int index, Vector3 world, string text)
        {
            while (_markerLabels.Count <= index)
            {
                var bg = UiKit.NewImage(controls.Overlay, "Nom du repère", new Color(0.05f, 0.25f, 0.1f, 0.85f), UiKit.Rounded);
                var r = bg.rectTransform;
                r.anchorMin = r.anchorMax = Vector2.zero;
                r.pivot = new Vector2(0.5f, 0f);
                var t = UiKit.NewText(bg.transform, controls.Font, TextAnchor.MiddleCenter, Color.white);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Fill(t.rectTransform);
                _markerLabels.Add(bg);
            }
            var label = _markerLabels[index];
            var screen = _camera != null ? _camera.WorldToScreenPoint(world) : Vector3.back;
            var visible = screen.z > 0f;
            label.gameObject.SetActive(visible);
            if (!visible) return;
            var h = Screen.height;
            var text2 = label.GetComponentInChildren<Text>();
            text2.text = text;
            text2.fontSize = Mathf.RoundToInt(h * Mathf.Clamp(1.2f / screen.z, 0.026f, 0.04f));
            label.rectTransform.sizeDelta = new Vector2(text2.preferredWidth + h * 0.03f, text2.fontSize * 1.6f);
            label.rectTransform.anchoredPosition = new Vector2(screen.x, screen.y);
        }
    }
}
