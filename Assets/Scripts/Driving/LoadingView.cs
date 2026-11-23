using TaxiKmer.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Écran de chargement (idée n° 21) pendant que <see cref="TaxiKmer.World.WorldBuilder"/> construit le monde sur
    /// plusieurs images : fond sombre, « Chargement de Yaoundé… 42 % » et une barre. Non exécuté dans Unity.
    /// </summary>
    public sealed class LoadingView
    {
        private readonly GameObject _root;
        private readonly Text _text;
        private readonly Image _bar;

        public LoadingView(Transform parent, Font font)
        {
            _root = new GameObject("Chargement", typeof(Canvas));
            _root.transform.SetParent(parent, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var rt = (RectTransform)_root.transform;
            var back = UiKit.NewImage(rt, "Fond", new Color(0.06f, 0.06f, 0.08f, 1f));
            UiKit.Fill(back.rectTransform);
            _text = UiKit.NewText(rt, font, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.1f));
            _bar = UiKit.NewImage(rt, "Barre", new Color(1f, 0.82f, 0.1f), UiKit.Rounded);
        }

        public void Update(float progress)
        {
            var h = Screen.height;
            _text.text = WorldBuildPlan.LoadingText(progress);
            _text.fontSize = Mathf.RoundToInt(h * 0.05f);
            UiKit.PlaceTopCenter(_text.rectTransform, h * 0.4f, Screen.width, h * 0.1f);
            var w = h * 0.9f;
            UiKit.PlaceTopCenter(_bar.rectTransform, h * 0.54f, Mathf.Max(h * 0.02f, w * Mathf.Clamp01(progress)), h * 0.02f);
        }

        public void Close() => Object.Destroy(_root);
    }
}
