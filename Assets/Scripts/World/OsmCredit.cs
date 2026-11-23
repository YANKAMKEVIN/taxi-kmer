using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.World
{
    /// <summary>
    /// Crédit obligatoire des données OpenStreetMap (licence ODbL), affiché dans un coin de l'écran. Texte uGUI créé une
    /// fois (idée n° 43) : l'ancien OnGUI était rappelé plusieurs fois par image, coûteux sur téléphone. Les crédits
    /// complets sont dans l'écran « Crédits » (idée n° 42).
    /// </summary>
    public sealed class OsmCredit : MonoBehaviour
    {
        public string text = "© contributeurs OpenStreetMap";
        [Tooltip("Coin ou bord de l'écran où afficher le crédit (en bas au centre quand les commandes occupent les coins).")]
        public TextAnchor alignment = TextAnchor.LowerRight;
        private Text _label;
        private int _laidOutForHeight;

        private void Start()
        {
            var canvasGo = new GameObject("Crédit OpenStreetMap", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // sous les commandes et les menus
            var go = new GameObject("Texte", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(canvasGo.transform, false);
            _label = go.GetComponent<Text>();
            _label.text = text;
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.alignment = alignment;
            _label.color = new Color(1f, 1f, 1f, 0.85f);
            _label.raycastTarget = false;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var r = _label.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
        }

        private void Update()
        {
            if (_label == null || Screen.height == _laidOutForHeight) return;
            _laidOutForHeight = Screen.height;
            _label.fontSize = Mathf.Max(12, Screen.height / 45);
            var pad = Screen.height / 60f;
            _label.rectTransform.offsetMin = new Vector2(pad, pad);
            _label.rectTransform.offsetMax = new Vector2(-pad, -pad);
        }
    }
}
