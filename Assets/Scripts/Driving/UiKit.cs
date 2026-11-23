using System.Collections.Generic;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Petits outils d'interface partagés : sprites dessinés par le code (<see cref="Shapes"/>), création de textes et
    /// d'images, et appui sur un rectangle (doigt ou souris), comme le reste du jeu qui lit l'écran sans EventSystem.
    /// </summary>
    public static class UiKit
    {
        private const int RoundSize = 64;
        private const float RoundRadius = 22f;
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<KeyAction, KeyCode[]> KeyCodes = new Dictionary<KeyAction, KeyCode[]>();

        /// <summary>Touches d'une action, lues dans la table du clavier (<see cref="KeyMap"/> : une touche par action, Kevin 23/11).</summary>
        public static KeyCode[] Keys(KeyAction action)
        {
            if (KeyCodes.TryGetValue(action, out var codes)) return codes;
            var names = KeyMap.KeysFor(action);
            codes = new KeyCode[names.Length];
            for (var i = 0; i < names.Length; i++) codes[i] = (KeyCode)System.Enum.Parse(typeof(KeyCode), names[i]);
            return KeyCodes[action] = codes;
        }

        /// <summary>Une touche de l'action vient d'être enfoncée (cette image).</summary>
        public static bool KeyDown(KeyAction action)
        {
            foreach (var k in Keys(action)) if (UnityEngine.Input.GetKeyDown(k)) return true;
            return false;
        }

        /// <summary>Une touche de l'action est tenue.</summary>
        public static bool KeyHeld(KeyAction action)
        {
            foreach (var k in Keys(action)) if (UnityEngine.Input.GetKey(k)) return true;
            return false;
        }

        /// <summary>Rectangle arrondi découpé en 9 (à utiliser avec Image.Type.Sliced).</summary>
        public static Sprite Rounded => Cached("arrondi", () =>
            ToSprite("UiArrondi", Shapes.RoundedRect(RoundSize, RoundRadius), RoundSize, RoundRadius + 2f));

        /// <summary>Contour d'un rectangle arrondi (bordures, places libres), découpé en 9.</summary>
        public static Sprite Frame => Cached("contour", () =>
            ToSprite("UiContour", Shapes.RoundedFrame(RoundSize, 18f, 5f), RoundSize, 20f));

        public static Sprite Disc => Cached("disque", () => ToSprite("UiDisque", Shapes.Disc(64), 64, 0f));

        public static Sprite Ring => Cached("anneau", () => ToSprite("UiAnneau", Shapes.Ring(64, 7f), 64, 0f));

        /// <summary>Sprite mis en cache sous un nom (formes dessinées une seule fois).</summary>
        public static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (!Sprites.TryGetValue(key, out var s) || s == null) Sprites[key] = s = make();
            return s;
        }

        /// <summary>Masque d'opacité carré (Core) → sprite blanc, teinté ensuite par Image.color.</summary>
        public static Sprite ToSprite(string name, byte[] alpha, int size, float border)
        {
            var px = new Color32[alpha.Length];
            for (var i = 0; i < alpha.Length; i++) px[i] = new Color32(255, 255, 255, alpha[i]);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static Image NewImage(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }
            return img;
        }

        public static Text NewText(Transform parent, Font font, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Texte", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Bouton arrondi avec son libellé centré (l'appui se lit avec <see cref="Tapped"/>).</summary>
        public static Image NewButton(Transform parent, Font font, string label, Color color, out Text text)
        {
            var img = NewImage(parent, label, color, Rounded);
            text = NewText(img.transform, font, TextAnchor.MiddleCenter, Color.white);
            text.text = label;
            Fill(text.rectTransform);
            return img;
        }

        /// <summary>Élément centré horizontalement, placé en pixels depuis le haut de son parent.</summary>
        public static void PlaceTopCenter(RectTransform r, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Étire un élément sur tout son parent, avec une marge intérieure en pixels.</summary>
        public static void Fill(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Élément ancré au coin haut gauche de son parent, placé en pixels depuis ce coin.</summary>
        public static void PlaceTopLeft(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Un doigt vient de se poser sur ce rectangle, ou la souris vient d'y cliquer (cette image).</summary>
        public static bool Tapped(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var t = UnityEngine.Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && RectTransformUtility.RectangleContainsScreenPoint(rect, t.position)) return true;
            }
            return UnityEngine.Input.touchCount == 0 && UnityEngine.Input.GetMouseButtonDown(0)
                && RectTransformUtility.RectangleContainsScreenPoint(rect, UnityEngine.Input.mousePosition);
        }

        /// <summary>Le point d'écran (pixels) est-il dans ce rectangle affiché ?</summary>
        public static bool Contains(RectTransform rect, Vector2 screenPoint)
            => rect != null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint);

        /// <summary>Pulsation entre 0 et 1 (temps réel, continue même en pause).</summary>
        public static float Pulse(float speed = 6f) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
    }
}
