using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//
// MaintenanceHistoryUI.cs — full-screen overlay listing maintenance records
// for an asset (GET /api/assets/:id/maintenance via IAssetProvider).
// Built programmatically; hidden until OpenForAsset() is called.
//

namespace ArPassport.AR
{
    public class MaintenanceHistoryUI : MonoBehaviour
    {
        private GameObject _root;
        private Text _titleText;
        private Text _stateText;
        private GameObject _listContent;
        private IAssetProvider _provider;
        private string _assetId;
        private Coroutine _loadRoutine;

        public void BuildUI()
        {
            if (_root != null) return;

            _root = new GameObject("MaintenanceHistory", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            ARUiKit.FillParent(_root.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            var dim = ARUiKit.Panel(_root, "Dim", new Color(0, 0, 0, 0.6f));

            var card = ARUiKit.Panel(_root, "Card", ARUiKit.BgDark);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.05f, 0.08f);
            cardRt.anchorMax = new Vector2(0.95f, 0.92f);
            cardRt.offsetMin = Vector2.zero; cardRt.offsetMax = Vector2.zero;

            _titleText = ARUiKit.Label(card, "Title", "Maintenance History", 32,
                ARUiKit.TextLight, FontStyle.Bold, TextAnchor.UpperCenter);
            var titleRt = _titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1);
            titleRt.offsetMin = new Vector2(24, -90); titleRt.offsetMax = new Vector2(-24, -20);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ScrollRect));
            scrollGo.transform.SetParent(card.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0.14f); scrollRt.anchorMax = new Vector2(1, 0.86f);
            scrollRt.offsetMin = Vector2.zero; scrollRt.offsetMax = Vector2.zero;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;

            _listContent = new GameObject("Rows", typeof(RectTransform));
            _listContent.transform.SetParent(scrollGo.transform, false);
            var contentRt = _listContent.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            ARUiKit.Vertical(_listContent, 24, 16);
            scroll.content = contentRt;

            _stateText = ARUiKit.Label(card, "State", "Loading…", 26, ARUiKit.TextDim,
                FontStyle.Italic, TextAnchor.MiddleCenter);
            var stateRt = _stateText.rectTransform;
            stateRt.anchorMin = new Vector2(0, 0.14f); stateRt.anchorMax = new Vector2(1, 0.86f);
            stateRt.offsetMin = Vector2.zero; stateRt.offsetMax = Vector2.zero;

            var close = ARUiKit.Button(card, "BtnClose", "Close", Hide, ARUiKit.Grey);
            var closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.1f, 0.02f); closeRt.anchorMax = new Vector2(0.9f, 0.12f);
            closeRt.offsetMin = Vector2.zero; closeRt.offsetMax = Vector2.zero;

            _root.SetActive(false);
        }

        public void OpenForAsset(string assetId, string assetName, IAssetProvider provider)
        {
            if (_root == null) BuildUI();
            _provider = provider;
            _assetId = assetId;
            _root.SetActive(true);
            ClearRows();
            _titleText.text = "Maintenance — " + ARUiKit.Safe(assetName, assetId);
            _stateText.gameObject.SetActive(true);
            _stateText.text = "Loading…";
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);
            _loadRoutine = StartCoroutine(LoadRoutine());
        }

        public void Hide()
        {
            if (_loadRoutine != null) { StopCoroutine(_loadRoutine); _loadRoutine = null; }
            if (_root != null) _root.SetActive(false);
        }

        private IEnumerator LoadRoutine()
        {
            if (_provider == null)
            {
                _stateText.text = "No data provider available.";
                yield break;
            }
            List<MaintenanceRecordDto> records = null;
            string error = null;
            bool done = false;
            yield return StartCoroutine(_provider.GetMaintenance(_assetId,
                ok => { records = ok; done = true; },
                e => { error = e; done = true; }));
            // Provider coroutines always invoke a callback; `done` guards hangs.
            if (!done) error = "Request did not complete.";
            _loadRoutine = null;

            if (!string.IsNullOrEmpty(error))
            {
                _stateText.text = error;
                yield break;
            }
            if (records == null || records.Count == 0)
            {
                _stateText.text = "No maintenance records for this asset.";
                yield break;
            }
            _stateText.gameObject.SetActive(false);
            foreach (var r in records) AddRow(r);
        }

        private void AddRow(MaintenanceRecordDto r)
        {
            var row = ARUiKit.Panel(_listContent, "Row", ARUiKit.Card);
            ARUiKit.Vertical(row, 20, 8);

            string headline = ARUiKit.Safe(r.date, "?") + "  •  " + ARUiKit.Safe(r.type, "service").ToUpperInvariant();
            ARUiKit.Label(row, "Headline", headline, 27, ARUiKit.TextLight, FontStyle.Bold);

            ARUiKit.Label(row, "Sub", "By " + ARUiKit.Safe(r.technician, "unknown"), 24, ARUiKit.TextDim);

            if (!string.IsNullOrEmpty(r.notes))
                ARUiKit.Label(row, "Notes", r.notes, 24, ARUiKit.TextLight);
        }

        private void ClearRows()
        {
            if (_listContent == null) return;
            for (int i = _listContent.transform.childCount - 1; i >= 0; i--)
                Destroy(_listContent.transform.GetChild(i).gameObject);
        }
    }
}
