using System.IO;
using TaxiKmer.Core.Ux;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Mode photo (idée n° 27), ouvert depuis la pause : HUD masqué, heure figée (le temps du jeu est arrêté), caméra
    /// libre autour du taxi (<see cref="PhotoCamera"/>) : glisser pour regarder, boutons pour avancer, reculer, monter,
    /// descendre (flèches / Z Q S D au clavier) ; « Photo » enregistre une image dans le dossier de l'appli,
    /// « Retour » revient à la pause. Non exécuté dans Unity.
    /// </summary>
    public sealed class PhotoModeView
    {
        private readonly GameObject _root;
        private readonly Image _shoot, _back, _forward, _backward, _up, _down;
        private readonly Text _hint;
        private PhotoCamera _camera;
        private ChaseCamera _chase;
        private GameObject _hud;
        private Vector3 _lastPointer;
        private bool _dragging;
        private int _shots;

        public bool Active => _camera != null;

        public PhotoModeView(Transform parent, Font font)
        {
            _root = new GameObject("Mode photo", typeof(Canvas));
            _root.transform.SetParent(parent, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var rt = (RectTransform)_root.transform;
            var grey = new Color(0.1f, 0.1f, 0.12f, 0.6f);
            _shoot = UiKit.NewButton(rt, font, "Photo", new Color(0.1f, 0.45f, 0.2f, 0.85f), out _);
            _back = UiKit.NewButton(rt, font, "Retour", grey, out _);
            _forward = UiKit.NewButton(rt, font, "▲", grey, out _);
            _backward = UiKit.NewButton(rt, font, "▼", grey, out _);
            _up = UiKit.NewButton(rt, font, "Monter", grey, out _);
            _down = UiKit.NewButton(rt, font, "Descendre", grey, out _);
            _hint = UiKit.NewText(rt, font, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.8f));
            _root.SetActive(false);
        }

        /// <summary>À chaque image. Rend true quand le joueur demande le retour à la pause.</summary>
        public bool Update(bool open, Transform taxi, GameObject hud, int day, string clock)
        {
            if (open && _camera == null) Enter(taxi, hud);
            if (!open && _camera != null) Exit();
            if (_camera == null) return false;

            Layout();
            var dt = Time.unscaledDeltaTime; // le temps du jeu est arrêté
            if (UiKit.Tapped(_back.rectTransform) || UiKit.KeyDown(KeyAction.Pause)) return true;
            if (UiKit.Tapped(_shoot.rectTransform)) Shoot(day, clock);
            var forward = (Held(_forward) || UiKit.KeyHeld(KeyAction.Accelerate) ? 1f : 0f) - (Held(_backward) || UiKit.KeyHeld(KeyAction.Brake) ? 1f : 0f);
            var right = (UiKit.KeyHeld(KeyAction.Right) ? 1f : 0f) - (UiKit.KeyHeld(KeyAction.Left) ? 1f : 0f);
            var up = (Held(_up) ? 1f : 0f) - (Held(_down) ? 1f : 0f);
            _camera.Move(forward, right, up, dt);
            Look();
            var cam = Camera.main;
            if (cam != null)
                cam.transform.SetPositionAndRotation(new Vector3(_camera.X, _camera.Y, _camera.Z), Quaternion.Euler(_camera.PitchDeg, _camera.YawDeg, 0f));
            return false;
        }

        private void Enter(Transform taxi, GameObject hud)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _chase = cam.GetComponent<ChaseCamera>();
            if (_chase != null) _chase.enabled = false; // ne plus suivre le taxi
            _hud = hud;
            if (_hud != null) _hud.SetActive(false);
            var e = cam.transform.eulerAngles;
            var p = cam.transform.position;
            _camera = new PhotoCamera(p.x, p.y, p.z, e.y, e.x > 180f ? e.x - 360f : e.x, taxi.position.x, taxi.position.z);
            _root.SetActive(true);
        }

        private void Exit()
        {
            _camera = null;
            if (_chase != null) _chase.enabled = true;
            if (_hud != null) _hud.SetActive(true);
            _root.SetActive(false);
        }

        private void Shoot(int day, string clock)
        {
            var dir = Path.Combine(Application.persistentDataPath, "photos");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, PhotoCamera.FileName(day, clock, _shots++));
            // Sans les boutons sur la photo : le canevas est masqué pendant la capture de la fin d'image.
            _root.SetActive(false);
            ScreenCapture.CaptureScreenshot(path);
            _hint.text = "Photo enregistrée : " + Path.GetFileName(path);
            _root.SetActive(true);
        }

        private void Look()
        {
            var pointer = Input.mousePosition;
            var down = Input.GetMouseButton(0) && !OnButton(pointer);
            if (down && _dragging)
            {
                var d = pointer - _lastPointer;
                _camera.Look(d.x / Mathf.Max(1f, Screen.width), d.y / Mathf.Max(1f, Screen.width));
            }
            _dragging = down;
            _lastPointer = pointer;
        }

        private bool OnButton(Vector2 p)
        {
            foreach (var b in new[] { _shoot, _back, _forward, _backward, _up, _down })
                if (UiKit.Contains(b.rectTransform, p)) return true;
            return false;
        }

        private static bool Held(Image button) => Input.GetMouseButton(0) && UiKit.Contains(button.rectTransform, Input.mousePosition);

        private void Layout()
        {
            var h = Screen.height;
            var w = Screen.width;
            var s = h * 0.12f;
            UiKit.PlaceTopLeft(_back.rectTransform, h * 0.04f, h * 0.04f, h * 0.25f, h * 0.1f);
            UiKit.PlaceTopLeft(_shoot.rectTransform, w - h * 0.34f, h * 0.04f, h * 0.3f, h * 0.12f);
            UiKit.PlaceTopLeft(_forward.rectTransform, h * 0.04f, h * 0.6f, s, s);
            UiKit.PlaceTopLeft(_backward.rectTransform, h * 0.04f, h * 0.6f + s * 1.1f, s, s);
            UiKit.PlaceTopLeft(_up.rectTransform, w - h * 0.34f, h * 0.6f, h * 0.3f, s);
            UiKit.PlaceTopLeft(_down.rectTransform, w - h * 0.34f, h * 0.6f + s * 1.1f, h * 0.3f, s);
            _hint.fontSize = Mathf.RoundToInt(h * 0.03f);
            UiKit.PlaceTopCenter(_hint.rectTransform, h * 0.04f, w * 0.5f, h * 0.06f);
            if (string.IsNullOrEmpty(_hint.text)) _hint.text = "Glisser pour regarder";
        }
    }
}
