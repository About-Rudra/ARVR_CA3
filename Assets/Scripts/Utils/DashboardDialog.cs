using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>Modal confirmation dialog, built in code. Single instance at a time.</summary>
    public static class DashboardDialog
    {
        public static void ShowConfirm(Transform canvas, string title, string message, string confirmLabel, Action onConfirm)
        {
            ShowConfirm(canvas, title, message, confirmLabel, "Cancel", DashboardStyle.Danger, onConfirm);
        }

        public static void ShowConfirm(Transform canvas, string title, string message, string confirmLabel, string cancelLabel, Color confirmColor, Action onConfirm)
        {
            if (canvas == null) return;
            Close(canvas);

            GameObject overlay = new GameObject("DashboardDialog", typeof(RectTransform));
            overlay.transform.SetParent(canvas, false);
            RectTransform ort = overlay.GetComponent<RectTransform>();
            DashboardStyle.Stretch(ort);
            Image dim = overlay.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.45f);

            GameObject card = new GameObject("DialogCard", typeof(RectTransform));
            card.transform.SetParent(overlay.transform, false);
            Image cardImg = card.AddComponent<Image>();
            cardImg.color = DashboardStyle.CardBg;
            RectTransform crt = card.GetComponent<RectTransform>();
            // Responsive margins instead of fixed pixels.
            crt.anchorMin = new Vector2(0.12f, 0.30f);
            crt.anchorMax = new Vector2(0.88f, 0.70f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            DashboardStyle.MakeText(card.transform, title ?? "Please confirm",
                DashboardStyle.SectionSize, DashboardStyle.Text, true, TextAnchor.MiddleLeft, "DialogTitle");
            Text msg = DashboardStyle.MakeText(card.transform, message ?? "",
                DashboardStyle.BodySize, DashboardStyle.Text, false, TextAnchor.UpperLeft, "DialogMessage");
            msg.horizontalOverflow = HorizontalWrapMode.Wrap;

            GameObject btnRow = DashboardStyle.Row(card.transform, "DialogButtons", 12);
            DashboardStyle.Spacer(btnRow.transform, 1);
            Button cancel = DashboardStyle.MakeButton(btnRow.transform, cancelLabel ?? "Cancel",
                DashboardStyle.Neutral, () => Close(canvas), 15, 44f, "CancelButton");
            cancel.GetComponent<LayoutElement>().preferredWidth = 130;
            Button confirm = DashboardStyle.MakeButton(btnRow.transform, confirmLabel ?? "Confirm",
                confirmColor, () =>
                {
                    Close(canvas);
                    if (onConfirm != null)
                    {
                        try { onConfirm(); }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[Dashboard] Confirm action failed: " + ex.Message);
                        }
                    }
                }, 15, 44f, "ConfirmButton");
            confirm.GetComponent<LayoutElement>().preferredWidth = 150;

            // Keyboard users land on the safe default (Cancel) first.
            try
            {
                cancel.Select();
            }
            catch
            {
                // Focus is best-effort.
            }
        }

        public static void Close(Transform canvas)
        {
            if (canvas == null) return;
            try
            {
                Transform existing = canvas.Find("DashboardDialog");
                if (existing != null)
                {
                    UnityEngine.Object.Destroy(existing.gameObject);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
