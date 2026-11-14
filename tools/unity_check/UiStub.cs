// Stub minimal d'UnityEngine.UI (paquet uGUI absent des références NuGet), pour vérifier la compilation.
namespace UnityEngine.UI
{
    public abstract class Graphic : MonoBehaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform => null;
    }
    public class MaskableGraphic : Graphic { }
    public class Text : MaskableGraphic
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public float lineSpacing { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public float preferredWidth => 0f;
        public float preferredHeight => 0f;
    }
    public class Image : MaskableGraphic { }
    public class Shadow : MonoBehaviour
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
    }
    public class Outline : Shadow { }
}
