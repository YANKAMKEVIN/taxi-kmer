using System.Collections.Generic;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Ux;
using TaxiKmer.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Mini-carte GPS ronde (idée n° 6) en haut à droite : routes du monde (image calculée une fois par
    /// <see cref="MiniMapRaster"/>), taxi au centre, destinations des passagers (couleur du lieu), postes de police et
    /// stations-service ; un repère hors de la carte reste sur le bord. Placement par <see cref="MiniMapPlacement"/> (Core).
    /// Créée par <see cref="StreetView"/> si minimap.json a enabled = true ; non exécutée dans Unity (session cloud).
    /// </summary>
    public sealed class MiniMapView
    {
        private static readonly Color32[] Palette =
        {
            new Color32(20, 24, 30, 0), new Color32(150, 150, 150, 255), new Color32(220, 220, 215, 255), new Color32(255, 205, 40, 255),
        };

        private readonly MiniMapConfig _config;
        private readonly MiniMapRaster _raster;
        private readonly RectTransform _root;
        private readonly RawImage _roads;
        private readonly Image _taxi;
        private readonly List<Image> _markers = new List<Image>();
        private int _used;
        private Vector2Int _laidOutFor;
        private float _radiusPx;

        public MiniMapView(RectTransform overlay, MiniMapConfig config, WorldData world)
        {
            _config = config;
            _raster = MiniMapRaster.Build(world, config);
            var px = new Color32[_raster.Cells.Length];
            for (var i = 0; i < px.Length; i++) px[i] = Palette[_raster.Cells[i]];
            var tex = new Texture2D(_raster.Width, _raster.Height, TextureFormat.RGBA32, false)
            {
                name = "Mini-carte", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);

            var disc = UiKit.NewImage(overlay, "Mini-carte", new Color(0.08f, 0.1f, 0.12f, 0.85f), UiKit.Disc);
            _root = disc.rectTransform;
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var go = new GameObject("Routes", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(_root, false);
            _roads = go.GetComponent<RawImage>();
            _roads.texture = tex;
            _roads.raycastTarget = false;
            UiKit.Fill(_roads.rectTransform);
            _taxi = UiKit.NewImage(_root, "Taxi", new Color(1f, 0.82f, 0.1f), UiKit.Rounded);
            _taxi.gameObject.AddComponent<Outline>().effectColor = Color.black;
            UiKit.NewImage(overlay, "Bord de la mini-carte", new Color(1f, 1f, 1f, 0.6f), UiKit.Ring).rectTransform.SetParent(_root, false);
        }

        /// <param name="markers">Repères : position x, z dans le monde et couleur.</param>
        public void Update(Vector3 taxiPosition, Vector3 taxiForward, IReadOnlyList<(float x, float z, Color color)> markers, bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (!visible) return;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _laidOutFor) Layout(size);

            // Fenêtre de l'image centrée sur le taxi, tournée pour que le devant soit en haut.
            _raster.ToPixel(taxiPosition.x, taxiPosition.z, out var cx, out var cy);
            var span = 2f * _config.radiusMeters / _raster.MetersPerPixel;
            _roads.uvRect = new Rect((cx - span / 2f) / _raster.Width, (cy - span / 2f) / _raster.Height, span / _raster.Width, span / _raster.Height);
            var angle = MiniMapPlacement.RotationDegrees(taxiForward.x, taxiForward.z, _config.rotateWithTaxi);
            _roads.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            _taxi.rectTransform.localEulerAngles = new Vector3(0f, 0f, _config.rotateWithTaxi ? 0f : -angle);

            _used = 0;
            foreach (var (x, z, color) in markers)
            {
                var inside = MiniMapPlacement.Place(taxiPosition.x, taxiPosition.z, taxiForward.x, taxiForward.z,
                    _config.rotateWithTaxi, _config.radiusMeters, x, z, out var u, out var v);
                var m = Marker();
                m.color = inside ? color : new Color(color.r, color.g, color.b, 0.6f);
                var edge = inside ? 1f : 0.9f;
                m.rectTransform.anchoredPosition = new Vector2(u * _radiusPx * edge, v * _radiusPx * edge);
            }
            for (var i = _used; i < _markers.Count; i++) _markers[i].enabled = false;
        }

        private Image Marker()
        {
            if (_used == _markers.Count)
            {
                var img = UiKit.NewImage(_root, "Repère", Color.white, UiKit.Disc);
                img.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = Vector2.one * Mathf.Max(8f, _radiusPx * 0.14f);
                _markers.Add(img);
            }
            var marker = _markers[_used++];
            marker.enabled = true;
            return marker;
        }

        private void Layout(Vector2Int size)
        {
            _laidOutFor = size;
            var u = size.y;
            var d = _config.size * u;
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 1f);
            _root.sizeDelta = new Vector2(d, d);
            _root.anchoredPosition = new Vector2(-_config.side * u, -_config.edge * u);
            _radiusPx = d / 2f;
            var t = _taxi.rectTransform;
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.sizeDelta = new Vector2(d * 0.06f, d * 0.1f);
            t.anchoredPosition = Vector2.zero;
            foreach (var m in _markers) m.rectTransform.sizeDelta = Vector2.one * Mathf.Max(8f, _radiusPx * 0.14f);
            var ring = _root.Find("Bord de la mini-carte") as RectTransform;
            if (ring != null) UiKit.Fill(ring);
        }
    }
}
