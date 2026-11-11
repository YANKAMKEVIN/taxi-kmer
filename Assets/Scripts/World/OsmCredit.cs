using UnityEngine;

namespace TaxiKmer.World
{
    /// <summary>Crédit obligatoire des données OpenStreetMap (licence ODbL), affiché dans un coin de l'écran.</summary>
    public sealed class OsmCredit : MonoBehaviour
    {
        public string text = "© contributeurs OpenStreetMap";
        private GUIStyle _style;

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(12, Screen.height / 45),
                    alignment = TextAnchor.LowerRight,
                };
                _style.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            }
            var pad = Screen.height / 60f;
            GUI.Label(new Rect(pad, pad, Screen.width - 2 * pad, Screen.height - 2 * pad), text, _style);
        }
    }
}
