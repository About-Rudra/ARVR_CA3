using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>Small UGUI construction helpers shared by dashboard panels.</summary>
    public static class DashboardUIHelper
    {
        /// <summary>Builds a ScrollRect with Viewport(Content) hierarchy. Content
        /// uses a VerticalLayoutGroup + ContentSizeFitter so rows stack.</summary>
        public static RectTransform CreateScrollView(Transform parent, string name, out RectTransform content)
        {
            GameObject root = new GameObject(name ?? "ScrollView", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRT = root.GetComponent<RectTransform>();
            Image rootImg = root.AddComponent<Image>();
            rootImg.color = DashboardStyle.CardBg;
            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(root.transform, false);
            Image vpImg = viewportGO.AddComponent<Image>();
            vpImg.color = DashboardStyle.CardBg;
            Mask mask = viewportGO.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            RectTransform viewportRT = viewportGO.GetComponent<RectTransform>();
            DashboardStyle.Stretch(viewportRT);

            GameObject contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRT;
            scroll.content = contentRT;

            LayoutElement le = root.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.flexibleWidth = 1f;
            le.minHeight = 120f;

            content = contentRT;
            return rootRT;
        }

        /// <summary>
        /// Builds a fully working UnityEngine.UI.Dropdown in code, including the
        /// hidden Template (Viewport/Content/Item) hierarchy it requires.
        /// </summary>
        public static Dropdown CreateDropdown(Transform parent, string name, List<string> options, UnityAction<int> onChanged)
        {
            GameObject root = new GameObject(name ?? "Dropdown", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Image bg = root.AddComponent<Image>();
            bg.color = Color.white;
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = DashboardStyle.Border;
            outline.effectDistance = new Vector2(1, -1);
            Dropdown dropdown = root.AddComponent<Dropdown>();
            dropdown.targetGraphic = bg;
            dropdown.transition = Selectable.Transition.ColorTint;

            GameObject labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(root.transform, false);
            Text label = labelGO.AddComponent<Text>();
            label.font = DashboardStyle.DefaultFont;
            label.fontSize = DashboardStyle.BodySize;
            label.color = DashboardStyle.Text;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = false;
            DashboardStyle.StretchWithMargins(labelGO.GetComponent<RectTransform>(), 10, 4, 34, 4);

            GameObject arrowGO = new GameObject("Arrow", typeof(RectTransform));
            arrowGO.transform.SetParent(root.transform, false);
            Text arrow = arrowGO.AddComponent<Text>();
            arrow.font = DashboardStyle.DefaultFont;
            arrow.fontSize = DashboardStyle.BodySize;
            arrow.color = DashboardStyle.Muted;
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.text = "\u25BC";
            RectTransform arrowRT = arrowGO.GetComponent<RectTransform>();
            arrowRT.anchorMin = new Vector2(1, 0);
            arrowRT.anchorMax = new Vector2(1, 1);
            arrowRT.pivot = new Vector2(1, 0.5f);
            arrowRT.offsetMin = new Vector2(-32, 0);
            arrowRT.offsetMax = new Vector2(-6, 0);

            // Template hierarchy: Template > Viewport > Content > Item(Toggle).
            GameObject templateGO = new GameObject("Template", typeof(RectTransform));
            templateGO.transform.SetParent(root.transform, false);
            Image templateImg = templateGO.AddComponent<Image>();
            templateImg.color = Color.white;
            Outline templateOutline = templateGO.AddComponent<Outline>();
            templateOutline.effectColor = DashboardStyle.Border;
            templateOutline.effectDistance = new Vector2(1, -1);
            RectTransform templateRT = templateGO.GetComponent<RectTransform>();
            templateRT.anchorMin = new Vector2(0, 0);
            templateRT.anchorMax = new Vector2(1, 0);
            templateRT.pivot = new Vector2(0.5f, 1);
            templateRT.offsetMin = new Vector2(0, -4);
            templateRT.offsetMax = new Vector2(0, -164);

            GameObject vpGO = new GameObject("Viewport", typeof(RectTransform));
            vpGO.transform.SetParent(templateGO.transform, false);
            Image vpImg = vpGO.AddComponent<Image>();
            vpImg.color = Color.white;
            Mask mask = vpGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            DashboardStyle.Stretch(vpGO.GetComponent<RectTransform>());

            GameObject contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(vpGO.transform, false);
            RectTransform contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            GameObject itemGO = new GameObject("Item", typeof(RectTransform));
            itemGO.transform.SetParent(contentGO.transform, false);
            Toggle itemToggle = itemGO.AddComponent<Toggle>();
            Image itemBg = itemGO.AddComponent<Image>();
            itemBg.color = Color.white;
            itemToggle.targetGraphic = itemBg;
            itemToggle.isOn = true;
            RectTransform itemRT = itemGO.GetComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0, 1);
            itemRT.anchorMax = new Vector2(1, 1);
            itemRT.pivot = new Vector2(0.5f, 1);
            itemRT.sizeDelta = new Vector2(0, 32);

            GameObject bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(itemGO.transform, false);
            DashboardStyle.Stretch(bgGO.GetComponent<RectTransform>());

            GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform));
            checkGO.transform.SetParent(bgGO.transform, false);
            Text check = checkGO.AddComponent<Text>();
            check.font = DashboardStyle.DefaultFont;
            check.fontSize = DashboardStyle.BodySize;
            check.color = DashboardStyle.Primary;
            check.alignment = TextAnchor.MiddleCenter;
            check.text = "\u2714";
            RectTransform checkRT = checkGO.GetComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0, 0);
            checkRT.anchorMax = new Vector2(0, 1);
            checkRT.pivot = new Vector2(0, 0.5f);
            checkRT.offsetMin = new Vector2(6, 0);
            checkRT.offsetMax = new Vector2(30, 0);
            itemToggle.graphic = check;

            GameObject itemLabelGO = new GameObject("Item Label", typeof(RectTransform));
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            Text itemLabel = itemLabelGO.AddComponent<Text>();
            itemLabel.font = DashboardStyle.DefaultFont;
            itemLabel.fontSize = DashboardStyle.BodySize;
            itemLabel.color = DashboardStyle.Text;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemLabel.supportRichText = false;
            DashboardStyle.StretchWithMargins(itemLabelGO.GetComponent<RectTransform>(), 34, 2, 8, 2);

            templateGO.SetActive(false);

            dropdown.template = templateRT;
            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            SetOptions(dropdown, options);
            if (onChanged != null)
            {
                dropdown.onValueChanged.AddListener(onChanged);
            }

            LayoutElement le = root.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.flexibleWidth = 1f;
            return dropdown;
        }

        public static void SetOptions(Dropdown dropdown, List<string> options)
        {
            if (dropdown == null) return;
            List<Dropdown.OptionData> data = new List<Dropdown.OptionData>();
            if (options != null)
            {
                foreach (string o in options)
                {
                    data.Add(new Dropdown.OptionData(o ?? ""));
                }
            }
            if (data.Count == 0)
            {
                data.Add(new Dropdown.OptionData("-"));
            }
            dropdown.options = data;
            dropdown.value = 0;
            dropdown.RefreshShownValue();
        }

        public static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        public static void Rebuild(Transform root)
        {
            if (root == null) return;
            try
            {
                LayoutRebuilder.ForceRebuildLayoutInformation(root as RectTransform);
            }
            catch
            {
                // Layout rebuild is best-effort; never fatal.
            }
        }

        public static string Safe(string s)
        {
            return s ?? "";
        }

        public static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, Math.Max(0, max - 3)) + "...";
        }

        /// <summary>Transient, non-blocking message. Auto-hides after a delay.</summary>
        public static void ShowToast(MonoBehaviour host, Transform canvas, string message)
        {
            if (host == null || canvas == null) return;
            try
            {
                GameObject go = new GameObject("Toast", typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                Image img = go.AddComponent<Image>();
                img.color = DashboardStyle.HeaderBg;
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1);
                rt.anchorMax = new Vector2(0.5f, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(-260, -140);
                rt.offsetMax = new Vector2(260, -80);
                Text t = DashboardStyle.MakeText(go.transform, message ?? "", DashboardStyle.BodySize, Color.white, true, TextAnchor.MiddleCenter, "ToastText");
                DashboardStyle.Stretch(t.rectTransform);
                host.StartCoroutine(HideAfter(go, 2.5f));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] Toast failed: " + ex.Message);
            }
        }

        private static IEnumerator HideAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
