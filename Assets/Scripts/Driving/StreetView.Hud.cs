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
    /// <summary>Interface de la rue : bulles des clients, messages, recette, pièces qui tombent. Partie de <see cref="StreetView"/> (idée n° 33 : fichier découpé).</summary>
    public sealed partial class StreetView
    {
        private void BuildHud()
        {
            for (var i = 0; i < _messageLines.Length; i++)
            {
                _messageLines[i] = NewText(TextAnchor.MiddleCenter);
                var tr = _messageLines[i].rectTransform;
                tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
            }

            // Rappel de fin de journée, en petit en bas au centre (entre les flèches et les pédales).
            _endReminder = NewText(TextAnchor.LowerCenter);
            _endReminder.color = new Color(1f, 0.75f, 0.35f);
            _endReminder.horizontalOverflow = HorizontalWrapMode.Wrap;
            var er = _endReminder.rectTransform;
            er.anchorMin = er.anchorMax = er.pivot = new Vector2(0.5f, 0f);

            _tutorialPanel = UiKit.NewImage(controls.Overlay, "Tutoriel", new Color(0.05f, 0.05f, 0.07f, 0.82f), UiKit.Rounded);
            _tutorialText = UiKit.NewText(_tutorialPanel.transform, controls.Font, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.55f));
            UiKit.Fill(_tutorialText.rectTransform, Screen.height * 0.015f);
            _tutorialPanel.gameObject.SetActive(false);
        }

        private Text NewText(TextAnchor anchor)
        {
            var go = new GameObject("Texte", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(controls.Overlay, false);
            var t = go.GetComponent<Text>();
            t.font = controls.Font;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            return t;
        }

        private Bubble NewBubble()
        {
            var go = new GameObject("Bulle", typeof(RectTransform));
            go.transform.SetParent(controls.Overlay, false);
            var b = new Bubble { Root = (RectTransform)go.transform };
            b.Root.anchorMin = b.Root.anchorMax = Vector2.zero;
            b.Root.pivot = new Vector2(0.5f, 0f);
            var dark = new Color(0.06f, 0.06f, 0.08f, 0.9f);
            b.Pointer = UiKit.NewImage(b.Root, "Pointe", dark);
            b.Pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var bg = UiKit.NewImage(b.Root, "Fond", dark, UiKit.Rounded);
            UiKit.Fill(bg.rectTransform);
            b.Border = UiKit.NewImage(b.Root, "Bordure", BubbleFits, UiKit.Frame);
            UiKit.Fill(b.Border.rectTransform);
            b.Build = UiKit.NewImage(b.Root, "Carrure", new Color(1f, 1f, 1f, 0.9f), UiKit.Rounded);
            b.Text = UiKit.NewText(b.Root, controls.Font, TextAnchor.MiddleLeft, Color.white);
            b.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            b.Text.supportRichText = true;
            return b;
        }

        private void ShowBubble(int index, Vector3 world, Client client)
        {
            while (_bubbles.Count <= index) _bubbles.Add(NewBubble());
            var bubble = _bubbles[index];
            var screen = _camera != null ? _camera.WorldToScreenPoint(world) : Vector3.back;
            var visible = screen.z > 0f;
            bubble.Root.gameObject.SetActive(visible);
            if (!visible) return;
            var h = Screen.height;
            var font = Mathf.RoundToInt(h * Mathf.Clamp(0.7f / screen.z, 0.028f, 0.045f));
            bubble.Text.fontSize = font;
            // Destination en blanc, prix en jaune et en gras (« Étoudi, 500 ! »).
            bubble.Text.text = $"{ShortName(client.DestinationStopId)}, <b><color=#FFD21A>{client.ProposedFare}</color></b> !";

            var fit = CabinPlan.Fit(Street.Shift.Cabin, client);
            bubble.Border.color = fit == SeatFit.Fits ? BubbleFits : fit == SeatFit.NeedsMove ? BubbleNeedsMove : BubbleNoRoom;

            // Icône de carrure : un buste plus ou moins large, à gauche du texte.
            var pad = font * 0.45f;
            var iconH = font * 0.9f;
            var iconW = iconH * CabinPlan.SilhouetteWidth(client.Corpulence) * 0.8f;
            var iconSlot = iconH * 0.8f;
            var textW = bubble.Text.preferredWidth;
            var size = new Vector2(pad + iconSlot + pad * 0.6f + textW + pad, font * 1.6f);
            var pointer = font * 0.45f;

            var pos = new Vector2(screen.x, screen.y + pointer * 0.7f);
            for (var guard = 0; guard < 10; guard++)
            {
                var r = new Rect(pos.x - size.x / 2f, pos.y, size.x, size.y);
                var overlap = false;
                foreach (var p in _placed)
                {
                    if (!p.Overlaps(r)) continue;
                    overlap = true;
                    pos.y = p.yMax + 2f;
                }
                if (!overlap) { _placed.Add(r); break; }
            }
            bubble.Root.sizeDelta = size;
            bubble.Root.anchoredPosition = pos;

            var pr = bubble.Pointer.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(pointer, pointer);
            pr.anchoredPosition = Vector2.zero;
            // Seule la bulle la plus proche garde sa pointe : les autres sont empilées au-dessus.
            bubble.Pointer.enabled = pos.y <= screen.y + pointer;

            var ir = bubble.Build.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = new Vector2(pad + iconSlot / 2f, 0f);
            ir.sizeDelta = new Vector2(iconW, iconH);

            var tr = bubble.Text.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(pad + iconSlot + pad * 0.6f, 0f);
            tr.sizeDelta = new Vector2(textW + 2f, size.y);
        }

        private void UpdateHud()
        {
            var h = Screen.height;
            var shift = Street.Shift;
            UpdatePaymentPops();
            _gauges.Update(shift, Calendar.HudLabel(Today), _revenue.Shown, !Street.DayFinished,
                Street.Reputation != null && Street.Reputation.Visible ? Street.Reputation.HudLabel : "");

            var reminder = Street.DayFinished ? "" : HudGauges.EndOfDayReminder(shift.Clock, shift.Cabin.PassengerCount);
            _endReminder.enabled = reminder.Length > 0;
            _endReminder.text = reminder;
            _endReminder.fontSize = Mathf.RoundToInt(h * 0.03f);
            _endReminder.rectTransform.anchoredPosition = new Vector2(0f, h * 0.07f);
            _endReminder.rectTransform.sizeDelta = new Vector2(Mathf.Max(h * 0.4f, Screen.width - h * 1.2f), h * 0.1f);

            // Messages empilés sur deux lignes (le plus récent en haut), sous la consigne du tutoriel si elle est affichée.
            var lines = Street.DayFinished ? new List<string>() : new List<string>(_messages.Visible(Time.time));
            var top = _tutorialPanel.gameObject.activeSelf ? h * 0.42f : h * 0.30f;
            for (var i = 0; i < _messageLines.Length; i++)
            {
                var line = _messageLines[i];
                line.enabled = i < lines.Count;
                if (!line.enabled) continue;
                line.text = lines[i];
                line.fontSize = Mathf.RoundToInt(h * (i == 0 ? 0.045f : 0.036f) * UiKit.TextScale);
                line.color = i == 0 ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                line.rectTransform.anchoredPosition = new Vector2(0f, -(top + i * h * 0.06f));
                line.rectTransform.sizeDelta = new Vector2(h * 1.4f, h * 0.06f);
            }
        }

        private void Toast(string text, float seconds = 3f) => _messages.Push(text, seconds, Time.time);

        /// <summary>Le montant payé jaillit du taxi en jaune, grossit, puis file vers le compteur de recette.</summary>
        private void StartPaymentPop(int amount)
        {
            var t = NewText(TextAnchor.MiddleCenter);
            t.text = $"+{Money(amount)}";
            t.color = new Color(1f, 0.85f, 0.15f);
            t.fontStyle = FontStyle.Bold;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
            _pops.Add((t, Time.time, amount));
            UpdatePaymentPops();
        }

        private void UpdatePaymentPops()
        {
            var h = Screen.height;
            var from = _camera != null ? (Vector2)_camera.WorldToScreenPoint(taxi.transform.position + Vector3.up * 2.2f) : new Vector2(Screen.width / 2f, h / 2f);
            var to = _gauges.RevenueScreenPoint;
            for (var i = _pops.Count - 1; i >= 0; i--)
            {
                var (text, start, amount) = _pops[i];
                var elapsed = Time.time - start;
                if (PaymentPop.Arrived(elapsed))
                {
                    _revenue.SetTarget(_revenue.Target + amount);
                    Destroy(text.gameObject);
                    _pops.RemoveAt(i);
                    continue;
                }
                PaymentPop.Sample(elapsed, out var scale, out var travel);
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(h * 0.06f * scale));
                text.rectTransform.sizeDelta = new Vector2(h * 0.6f, h * 0.1f);
                text.rectTransform.anchoredPosition = Vector2.Lerp(from, to, travel);
            }
            // Recette changée sans animation (ex. tests) : le compteur la rattrape.
            if (_pops.Count == 0 && _revenue.Target != Street.Shift.Ledger.Revenue) _revenue.SetTarget(Street.Shift.Ledger.Revenue);
            _revenue.Step(Time.deltaTime);
        }
    }
}
