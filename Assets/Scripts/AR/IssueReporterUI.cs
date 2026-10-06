using System.Collections;
using UnityEngine;
using UnityEngine.UI;

//
// IssueReporterUI.cs — simple issue form (assetId auto-filled, title,
// description, reportedBy) -> POST /api/issues via IAssetProvider.
// Built programmatically; hidden until OpenForAsset() is called.
//

namespace ArPassport.AR
{
    public class IssueReporterUI : MonoBehaviour
    {
        private GameObject _root;
        private InputField _assetField;
        private InputField _titleField;
        private InputField _descField;
        private InputField _byField;
        private Text _stateText;
        private Button _submitButton;

        private IAssetProvider _provider;
        private AssetDto _asset;
        private bool _busy;

        public void BuildUI()
        {
            if (_root != null) return;

            _root = new GameObject("IssueReporter", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            ARUiKit.FillParent(_root.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            ARUiKit.Panel(_root, "Dim", new Color(0, 0, 0, 0.6f));

            var card = ARUiKit.Panel(_root, "Card", ARUiKit.BgDark);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.05f, 0.12f);
            cardRt.anchorMax = new Vector2(0.95f, 0.9f);
            cardRt.offsetMin = Vector2.zero; cardRt.offsetMax = Vector2.zero;

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(card.transform, false);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0, 0.14f); bodyRt.anchorMax = new Vector2(1, 1);
            bodyRt.offsetMin = Vector2.zero; bodyRt.offsetMax = Vector2.zero;
            ARUiKit.Vertical(body, 28, 14);

            ARUiKit.Label(body, "Title", "Report Issue", 32, ARUiKit.TextLight, FontStyle.Bold);

            ARUiKit.Label(body, "AssetLabel", "Asset", 24, ARUiKit.TextDim);
            _assetField = ARUiKit.Input(body, "AssetId", "AST-001");
            ARUiKit.FixedHeight(_assetField.gameObject, 84);
            _assetField.readOnly = true; // auto-filled from the scanned asset

            ARUiKit.Label(body, "IssueLabel", "Title *", 24, ARUiKit.TextDim);
            _titleField = ARUiKit.Input(body, "IssueTitle", "e.g. Unusual vibration");
            ARUiKit.FixedHeight(_titleField.gameObject, 84);

            ARUiKit.Label(body, "DescLabel", "Description *", 24, ARUiKit.TextDim);
            _descField = ARUiKit.Input(body, "IssueDesc", "What did you observe?", 26, true);
            ARUiKit.FixedHeight(_descField.gameObject, 220);

            ARUiKit.Label(body, "ByLabel", "Reported by", 24, ARUiKit.TextDim);
            _byField = ARUiKit.Input(body, "ReportedBy", "Your name");
            ARUiKit.FixedHeight(_byField.gameObject, 84);

            _stateText = ARUiKit.Label(body, "State", string.Empty, 24, ARUiKit.Warning);

            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(body.transform, false);
            var hl = row.AddComponent<LayoutElement>();
            hl.minHeight = 110; hl.preferredHeight = 110;
            var hg = row.GetComponent<HorizontalLayoutGroup>();
            hg.spacing = 20;
            hg.childControlWidth = true; hg.childForceExpandWidth = true;

            _submitButton = ARUiKit.Button(row, "BtnSubmit", "Submit", Submit,
                ARUiKit.Success);
            ARUiKit.Button(row, "BtnCancel", "Cancel", Hide, ARUiKit.Grey);

            _root.SetActive(false);
        }

        public void OpenForAsset(AssetDto asset, IAssetProvider provider)
        {
            if (_root == null) BuildUI();
            _asset = asset;
            _provider = provider;
            _root.SetActive(true);
            _stateText.text = string.Empty;
            _assetField.text = asset != null
                ? (asset.id + "  •  " + asset.name) : string.Empty;
            _titleField.text = string.Empty;
            _descField.text = string.Empty;
            _byField.text = PlayerPrefs.GetString("arpassport.reporter", string.Empty);
            _busy = false;
            RefreshSubmit();
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _busy = false;
        }

        private void RefreshSubmit()
        {
            if (_submitButton != null) _submitButton.interactable = !_busy;
        }

        private void Submit()
        {
            if (_busy) return;
            if (_asset == null || string.IsNullOrEmpty(_asset.id))
            { _stateText.text = "No asset selected."; return; }
            if (_provider == null)
            { _stateText.text = "No data provider available."; return; }
            string title = _titleField.text != null ? _titleField.text.Trim() : string.Empty;
            string desc = _descField.text != null ? _descField.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(title))
            { _stateText.text = "Please enter a title."; return; }
            if (string.IsNullOrEmpty(desc))
            { _stateText.text = "Please describe the issue."; return; }

            string by = _byField.text != null ? _byField.text.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(by))
            {
                PlayerPrefs.SetString("arpassport.reporter", by);
                PlayerPrefs.Save();
            }
            _busy = true;
            RefreshSubmit();
            _stateText.color = ARUiKit.TextDim;
            _stateText.text = "Submitting…";
            StartCoroutine(SubmitRoutine(new IssueDraftDto
            {
                assetId = _asset.id,
                title = title,
                description = desc,
                reportedBy = by
            }));
        }

        private IEnumerator SubmitRoutine(IssueDraftDto draft)
        {
            IssueDto created = null;
            string error = null;
            yield return StartCoroutine(_provider.ReportIssue(draft,
                ok => { created = ok; },
                e => { error = e; }));
            _busy = false;
            RefreshSubmit();
            if (!string.IsNullOrEmpty(error))
            {
                _stateText.color = ARUiKit.Danger;
                _stateText.text = error;
                yield break;
            }
            _stateText.color = ARUiKit.Success;
            _stateText.text = "Reported ✓  (" + (created != null ? ARUiKit.Safe(created.id, "ok") : "ok") + ")";
            // Auto-close after a beat so the user sees the confirmation.
            yield return new WaitForSecondsRealtime(1.2f);
            if (_root != null && _root.activeSelf) Hide();
        }
    }
}
