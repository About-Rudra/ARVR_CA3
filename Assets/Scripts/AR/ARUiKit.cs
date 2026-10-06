using System;
using UnityEngine;
using UnityEngine.UI;

//
// ARUiKit.cs — tiny programmatic-UGUI helper (no prefabs, no TextMeshPro).
// All AR UI is built in code so the scene has zero missing-reference risk.
//

namespace ArPassport.AR
{
    public static class ARUiKit
    {
        public static readonly Color BgDark = new Color(0.09f, 0.11f, 0.14f, 0.96f);
        public static readonly Color Card = new Color(0.16f, 0.19f, 0.24f, 1f);
        public static readonly Color Accent = new Color(0.13f, 0.59f, 0.95f, 1f);
        public static readonly Color TextLight = Color.white;
        public static readonly Color TextDim = new Color(0.75f, 0.78f, 0.82f, 1f);
        public static readonly Color Danger = new Color(0.85f, 0.27f, 0.27f, 1f);
        public static readonly Color Success = new Color(0.18f, 0.68f, 0.38f, 1f);
        public static readonly Color Warning = new Color(0.95f, 0.65f, 0.15f, 1f);
        public static readonly Color Grey = new Color(0.45f, 0.48f, 0.52f, 1f);

        private static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    if (_font == null) _font = Font.createDynamicFontFromOSFont("Arial", 16);
                }
                return _font;
            }
        }

        public static GameObject Panel(GameObject parent, string name, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = bg;
            return go;
        }

        public static Text Label(GameObject parent, string name, string text, int size,
            Color? color = null, FontStyle style = FontStyle.Normal,
            TextAnchor align = TextAnchor.UpperLeft, bool wrap = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent.transform, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text ?? string.Empty;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color ?? TextLight;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        public static Button Button(GameObject parent, string name, string text, Action onClick,
            Color? bg = null, int fontSize = 26)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            var img = go.GetComponent<Image>();
            img.color = bg ?? Accent;
            var btn = go.GetComponent<Button>();
            // Pressed-state tint.
            var colors = btn.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = colors;

            var label = Label(go, "Text", text, fontSize, Color.white, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static InputField Input(GameObject parent, string name, string placeholder,
            int fontSize = 26, bool multiLine = false, bool readOnly = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(InputField));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var field = go.GetComponent<InputField>();
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = Font; text.fontSize = fontSize; text.color = TextLight;
            text.supportRichText = false;
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16, 8); trt.offsetMax = new Vector2(-16, -8);

            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            phGo.transform.SetParent(go.transform, false);
            var ph = phGo.GetComponent<Text>();
            ph.font = Font; ph.fontSize = fontSize; ph.color = new Color(1f, 1f, 1f, 0.4f);
            ph.text = placeholder ?? string.Empty;
            var prt = phGo.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(16, 8); prt.offsetMax = new Vector2(-16, -8);

            field.textComponent = text;
            field.placeholder = ph;
            field.lineType = multiLine ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            field.readOnly = readOnly;
            return field;
        }

        public static void FillParent(RectTransform rt, Vector2 insetMin, Vector2 insetMax)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = insetMin; rt.offsetMax = insetMax;
        }

        public static VerticalLayoutGroup Vertical(GameObject go, int padding = 24,
            int spacing = 16, bool fitHeight = true)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            if (fitHeight)
            {
                var fitter = go.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return layout;
        }

        public static LayoutElement FixedHeight(GameObject go, float h)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
            return le;
        }

        public static Color StatusColor(string status)
        {
            if (string.IsNullOrEmpty(status)) return Grey;
            string s = status.Trim().ToLowerInvariant();
            // Backend vocabulary: operational | under_maintenance | faulty | retired.
            if (s.Contains("retired") || s.Contains("decommission")) return Grey;
            if (s.Contains("operation") || s == "ok" || s == "active" || s == "good") return Success;
            if (s.Contains("maint") || s.Contains("warn") || s.Contains("pending") || s.Contains("progress")) return Warning;
            if (s.Contains("fault") || s.Contains("fail") || s.Contains("error") || s.Contains("critical")
                || s.Contains("down") || s.Contains("service")) return Danger;
            return Grey;
        }

        public static string StatusText(string status)
        {
            if (string.IsNullOrEmpty(status)) return "UNKNOWN";
            return status.Trim().ToUpperInvariant();
        }

        public static string Safe(string v, string fallback = "—")
        {
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
    }
}
