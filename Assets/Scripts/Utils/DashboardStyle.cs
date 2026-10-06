using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Single source of truth for dashboard look-and-feel: palette, typography
    /// scale and widget factories. Light theme with dark-on-light text for
    /// readable contrast; status badges use white text on saturated fills.
    /// </summary>
    public static class DashboardStyle
    {
        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        public static readonly Color Bg = Parse("#EDF1F6");
        public static readonly Color HeaderBg = Parse("#1F2937");
        public static readonly Color HeaderText = Color.white;
        public static readonly Color CardBg = Color.white;
        public static readonly Color Border = Parse("#D7DEE8");
        public static readonly Color Text = Parse("#111827");
        public static readonly Color Muted = Parse("#5B6472");
        public static readonly Color Primary = Parse("#2563EB");
        public static readonly Color PrimaryDark = Parse("#1D4ED8");
        public static readonly Color Danger = Parse("#DC2626");
        public static readonly Color DangerDark = Parse("#B91C1C");
        public static readonly Color Success = Parse("#16A34A");
        public static readonly Color Warning = Parse("#D97706");
        public static readonly Color Info = Parse("#2563EB");
        public static readonly Color Neutral = Parse("#6B7280");
        public static readonly Color InputBg = Color.white;
        public static readonly Color ErrorBg = Parse("#FDECEC");
        public static readonly Color ErrorText = Parse("#991B1B");
        public static readonly Color NoticeBg = Parse("#FFFBEB");
        public static readonly Color NoticeText = Parse("#92400E");
        public static readonly Color RowAlt = Parse("#F7F9FC");

        public const int TitleSize = 26;
        public const int SectionSize = 18;
        public const int BodySize = 15;
        public const int SmallSize = 13;
        public const float TouchHeight = 48f;

        public static Color Parse(string hex)
        {
            Color c = Color.gray;
            if (!ColorUtility.TryParseHtmlString(hex, out c))
            {
                c = Color.gray;
            }
            return c;
        }

        public static Color AssetStatusColor(string status)
        {
            switch (DashboardAsset.NormalizeStatus(status))
            {
                case DashboardAsset.StatusMaintenance: return Warning;
                case DashboardAsset.StatusOutOfService: return Danger;
                default: return Success;
            }
        }

        public static Color IssueStatusColor(string status)
        {
            switch (DashboardIssue.NormalizeIssueStatus(status))
            {
                case DashboardIssue.StatusOpen: return Danger;
                case DashboardIssue.StatusInProgress: return Info;
                case DashboardIssue.StatusResolved: return Success;
                default: return Neutral;
            }
        }

        public static Text MakeText(Transform parent, string text, int size, Color color, bool bold, TextAnchor align, string name)
        {
            GameObject go = new GameObject(name ?? "Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text ?? "";
            t.fontSize = size;
            t.color = color;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = align;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text MakeText(Transform parent, string text, int size, Color color)
        {
            return MakeText(parent, text, size, color, false, TextAnchor.UpperLeft, "Label");
        }

        public static Button MakeButton(Transform parent, string label, Color bg, UnityAction onClick, int fontSize, float minHeight, string name)
        {
            GameObject go = new GameObject(name ?? "Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = bg;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = ColorBlock.defaultColorBlock;
            cb.normalColor = bg;
            cb.highlightedColor = Multiply(bg, 0.92f);
            cb.pressedColor = Multiply(bg, 0.82f);
            cb.selectedColor = Multiply(bg, 0.92f);
            cb.disabledColor = Parse("#9AA3B2");
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.05f; // Subtle; no excessive animation.
            btn.colors = cb;
            Text t = MakeText(go.transform, label, fontSize, ContrastingText(bg), true, TextAnchor.MiddleCenter, "Label");
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(12, 6);
            t.rectTransform.offsetMax = new Vector2(-12, -6);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }
            return btn;
        }

        public static Button MakeButton(Transform parent, string label, Color bg, UnityAction onClick)
        {
            return MakeButton(parent, label, bg, onClick, 16, TouchHeight, "Button");
        }

        /// <summary>White card panel with padding. Returns the content container.</summary>
        public static GameObject MakeCard(Transform parent, string name)
        {
            GameObject go = new GameObject(name ?? "Card", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = CardBg;
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
        }

        /// <summary>Status badge (saturated fill, white text). Keep the returned
        /// GameObject and update it with SetBadge.</summary>
        public static GameObject MakeBadge(Transform parent, string text, Color bg)
        {
            GameObject go = new GameObject("Badge", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = bg;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 30;
            le.minWidth = 90;
            Text t = MakeText(go.transform, text, SmallSize, Color.white, true, TextAnchor.MiddleCenter, "BadgeText");
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(8, 2);
            t.rectTransform.offsetMax = new Vector2(-8, -2);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return go;
        }

        public static void SetBadge(GameObject badge, string text, Color bg)
        {
            if (badge == null) return;
            Image img = badge.GetComponent<Image>();
            if (img != null) img.color = bg;
            Text t = badge.GetComponentInChildren<Text>();
            if (t != null) t.text = text ?? "";
        }

        public static InputField MakeInput(Transform parent, string placeholder, bool multiLine, string name)
        {
            GameObject go = new GameObject(name ?? "Input", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = InputBg;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1);
            InputField input = go.AddComponent<InputField>();
            input.targetGraphic = img;
            input.transition = Selectable.Transition.ColorTint;

            GameObject textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            Text text = textGO.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = BodySize;
            text.color = Text;
            text.alignment = multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            text.supportRichText = false;
            StretchWithMargins(text.rectTransform, 10, 8, 10, 8);

            GameObject phGO = new GameObject("Placeholder", typeof(RectTransform));
            phGO.transform.SetParent(go.transform, false);
            Text ph = phGO.AddComponent<Text>();
            ph.font = DefaultFont;
            ph.fontSize = BodySize;
            ph.color = Muted;
            ph.text = placeholder ?? "";
            ph.alignment = multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            StretchWithMargins(ph.rectTransform, 10, 8, 10, 8);

            input.textComponent = text;
            input.placeholder = ph;
            input.lineType = multiLine ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = multiLine ? 88f : 44f;
            le.flexibleWidth = 1f;
            return input;
        }

        public static GameObject Spacer(Transform parent, float flex)
        {
            GameObject go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = flex <= 0 ? 1f : flex;
            le.minWidth = 8;
            return go;
        }

        public static GameObject Divider(Transform parent)
        {
            GameObject go = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = Border;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 1;
            le.flexibleWidth = 1f;
            return go;
        }

        public static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void StretchWithMargins(RectTransform rt, float left, float top, float right, float bottom)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static GameObject Row(Transform parent, string name, float spacing)
        {
            GameObject go = new GameObject(name ?? "Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(8, 8, 6, 6);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childScaleWidth = false;
            h.childScaleHeight = false;
            return go;
        }

        public static GameObject Column(Transform parent, string name, float spacing)
        {
            GameObject go = new GameObject(name ?? "Column", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            VerticalLayoutGroup v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(0, 0, 0, 0);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return go;
        }

        private static Color Multiply(Color c, float f)
        {
            return new Color(c.r * f, c.g * f, c.b * f, c.a);
        }

        private static Color ContrastingText(Color bg)
        {
            double lum = 0.299 * bg.r + 0.587 * bg.g + 0.114 * bg.b;
            return lum > 0.6 ? Text : Color.white;
        }
    }
}
