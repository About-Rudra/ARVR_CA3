using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Create / edit form with validation. ID must match AST-### (required,
    /// editable only when creating), name/category/location required, status via
    /// dropdown, dates optional but validated (yyyy-MM-dd, next >= last).
    /// Delete is routed through DashboardDialog confirmation via the controller.
    /// </summary>
    public class AssetFormPanel : MonoBehaviour
    {
        private const string DateFormat = "yyyy-MM-dd";

        private DashboardController _controller;
        private GameObject _overlay;
        private Text _titleText;
        private Text _errorText;
        private bool _built;

        private InputField _idInput;
        private InputField _nameInput;
        private InputField _categoryInput;
        private InputField _locationInput;
        private Dropdown _statusDropdown;
        private InputField _markerInput;
        private InputField _descInput;
        private InputField _lastInput;
        private InputField _nextInput;

        private bool _isCreate;
        private DashboardAsset _editing;
        private Action _onSaved;

        private readonly List<string> _statusValues = new List<string>
        {
            DashboardAsset.StatusOperational,
            DashboardAsset.StatusMaintenance,
            DashboardAsset.StatusOutOfService
        };

        public void Initialize(DashboardController controller)
        {
            _controller = controller;
            Build();
            Hide();
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            _overlay = new GameObject("AssetFormOverlay", typeof(RectTransform));
            _overlay.transform.SetParent(transform, false);
            DashboardStyle.Stretch(_overlay.GetComponent<RectTransform>());
            Image dim = _overlay.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.45f);

            GameObject card = new GameObject("FormCard", typeof(RectTransform));
            card.transform.SetParent(_overlay.transform, false);
            Image cardImg = card.AddComponent<Image>();
            cardImg.color = DashboardStyle.CardBg;
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.08f, 0.05f);
            crt.anchorMax = new Vector2(0.92f, 0.95f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _titleText = DashboardStyle.MakeText(card.transform, "New Asset", DashboardStyle.TitleSize,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "FormTitle");

            RectTransform content;
            DashboardUIHelper.CreateScrollView(card.transform, "FormScroll", out content);

            _idInput = AddField(content.transform, "Asset ID * (format AST-001)", "e.g. AST-007", false);
            _nameInput = AddField(content.transform, "Name *", "e.g. Hydraulic Press", false);
            _categoryInput = AddField(content.transform, "Category *", "e.g. Machinery", false);
            _locationInput = AddField(content.transform, "Location *", "e.g. Bay A", false);
            AddLabel(content.transform, "Status *");
            List<string> statusLabels = new List<string>();
            foreach (string s in _statusValues) statusLabels.Add(DashboardAsset.DisplayStatus(s));
            _statusDropdown = DashboardUIHelper.CreateDropdown(content.transform, "StatusDropdown", statusLabels, null);
            _markerInput = AddField(content.transform, "Marker ID", "e.g. MK-007", false);
            _descInput = AddField(content.transform, "Description", "Optional notes", true);
            _lastInput = AddField(content.transform, "Last Maintenance (yyyy-MM-dd, optional)", "2026-01-15", false);
            _nextInput = AddField(content.transform, "Next Maintenance (yyyy-MM-dd, optional)", "2026-07-15", false);

            _errorText = DashboardStyle.MakeText(card.transform, "", DashboardStyle.BodySize,
                DashboardStyle.ErrorText, true, TextAnchor.UpperLeft, "FormError");
            _errorText.gameObject.SetActive(false);

            GameObject footer = DashboardStyle.Row(card.transform, "FormFooter", 12);
            footer.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            DashboardStyle.Spacer(footer.transform, 1);
            Button cancel = DashboardStyle.MakeButton(footer.transform, "Cancel", DashboardStyle.Neutral,
                () => Hide(), 15, 44f, "CancelButton");
            cancel.GetComponent<LayoutElement>().preferredWidth = 130;
            Button save = DashboardStyle.MakeButton(footer.transform, "Save", DashboardStyle.Primary,
                () => Submit(), 15, 44f, "SaveButton");
            save.GetComponent<LayoutElement>().preferredWidth = 150;
        }

        private void AddLabel(Transform parent, string text)
        {
            DashboardStyle.MakeText(parent, text, DashboardStyle.SmallSize,
                DashboardStyle.Muted, true, TextAnchor.MiddleLeft, "FieldLabel");
        }

        private InputField AddField(Transform parent, string label, string placeholder, bool multiLine)
        {
            AddLabel(parent, label);
            return DashboardStyle.MakeInput(parent, placeholder, multiLine, "Input_" + label);
        }

        public void ShowForCreate(Action onSaved)
        {
            Build();
            _isCreate = true;
            _editing = null;
            _onSaved = onSaved;
            _titleText.text = "New Asset";
            _idInput.text = "";
            _idInput.interactable = true;
            _nameInput.text = "";
            _categoryInput.text = "";
            _locationInput.text = "";
            _statusDropdown.value = 0;
            _statusDropdown.RefreshShownValue();
            _markerInput.text = "";
            _descInput.text = "";
            _lastInput.text = "";
            _nextInput.text = "";
            SetError(null);
            _overlay.SetActive(true);
            FocusFirst();
        }

        public void ShowForEdit(DashboardAsset asset, Action onSaved)
        {
            Build();
            if (asset == null) return;
            _isCreate = false;
            _editing = asset;
            _onSaved = onSaved;
            _titleText.text = "Edit Asset " + asset.id;
            _idInput.text = asset.id;
            _idInput.interactable = false; // IDs are immutable after creation.
            _nameInput.text = asset.name;
            _categoryInput.text = asset.category;
            _locationInput.text = asset.location;
            _statusDropdown.value = Mathf.Clamp(_statusValues.IndexOf(
                DashboardAsset.NormalizeStatus(asset.status)), 0, _statusValues.Count - 1);
            _statusDropdown.RefreshShownValue();
            _markerInput.text = asset.markerId;
            _descInput.text = asset.description;
            _lastInput.text = asset.lastMaintenance;
            _nextInput.text = asset.nextMaintenance;
            SetError(null);
            _overlay.SetActive(true);
            FocusFirst();
        }

        public void Hide()
        {
            if (_overlay != null) _overlay.SetActive(false);
        }

        private void FocusFirst()
        {
            try
            {
                (_isCreate ? _idInput : _nameInput).Select();
            }
            catch
            {
                // Focus is best-effort.
            }
        }

        private void SetError(string msg)
        {
            if (_errorText == null) return;
            if (string.IsNullOrEmpty(msg))
            {
                _errorText.gameObject.SetActive(false);
            }
            else
            {
                _errorText.text = msg;
                _errorText.gameObject.SetActive(true);
            }
        }

        private static bool ValidDate(string s, out DateTime dt)
        {
            return DateTime.TryParseExact((s ?? "").Trim(), DateFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
        }

        private void Submit()
        {
            string id = _idInput.text.Trim();
            string name = _nameInput.text.Trim();
            string category = _categoryInput.text.Trim();
            string location = _locationInput.text.Trim();
            string status = _statusValues[Mathf.Clamp(_statusDropdown.value, 0, _statusValues.Count - 1)];
            string marker = _markerInput.text.Trim();
            string desc = _descInput.text.Trim();
            string last = _lastInput.text.Trim();
            string next = _nextInput.text.Trim();

            if (_isCreate && !DashboardAsset.IsValidId(id))
            {
                SetError("Asset ID is required and must look like AST-001 (letters AST, dash, at least 3 digits).");
                return;
            }
            if (string.IsNullOrEmpty(name))
            {
                SetError("Name is required.");
                return;
            }
            if (string.IsNullOrEmpty(category))
            {
                SetError("Category is required.");
                return;
            }
            if (string.IsNullOrEmpty(location))
            {
                SetError("Location is required.");
                return;
            }
            DateTime lastDt = default(DateTime);
            DateTime nextDt = default(DateTime);
            bool hasLast = last.Length > 0;
            bool hasNext = next.Length > 0;
            if (hasLast && !ValidDate(last, out lastDt))
            {
                SetError("Last maintenance date must use yyyy-MM-dd (e.g. 2026-01-15) or be left empty.");
                return;
            }
            if (hasNext && !ValidDate(next, out nextDt))
            {
                SetError("Next maintenance date must use yyyy-MM-dd (e.g. 2026-07-15) or be left empty.");
                return;
            }
            if (hasLast && hasNext && nextDt < lastDt)
            {
                SetError("Next maintenance cannot be earlier than last maintenance.");
                return;
            }

            DashboardAsset asset = _isCreate ? new DashboardAsset() : _editing;
            if (asset == null) asset = new DashboardAsset();
            if (_isCreate) asset.id = id;
            asset.name = name;
            asset.category = category;
            asset.location = location;
            asset.status = status;
            asset.markerId = marker;
            asset.description = desc;
            asset.lastMaintenance = hasLast ? lastDt.ToString(DateFormat) : "";
            asset.nextMaintenance = hasNext ? nextDt.ToString(DateFormat) : "";
            SetError(null);

            if (_isCreate)
            {
                DashboardApiAdapter.CreateAsset(asset,
                    () =>
                    {
                        Hide();
                        if (_controller != null) _controller.Toast("Asset " + asset.id + " created.");
                        if (_onSaved != null) _onSaved();
                    },
                    msg => SetError(msg));
            }
            else
            {
                DashboardApiAdapter.UpdateAsset(asset,
                    () =>
                    {
                        Hide();
                        if (_controller != null) _controller.Toast("Asset " + asset.id + " updated.");
                        if (_onSaved != null) _onSaved();
                    },
                    msg => SetError(msg));
            }
        }
    }
}
