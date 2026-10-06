# Dashboard Prefabs

This folder intentionally contains **no binary `.prefab` files**.

All dashboard rows and cards are built in code at runtime:

- Asset table rows → `Assets/Scripts/Dashboard/AssetListItemUI.cs`
  (built by `AssetListController.cs`)
- Summary cards, badges, buttons, dialogs → `Assets/Scripts/Utils/DashboardStyle.cs`
- Issue rows → `Assets/Scripts/Dashboard/IssueManagerUI.cs`
- Stat bars → `Assets/Scripts/Dashboard/StatsPanel.cs`

Rationale: code-built UI has zero missing-reference risk, works on any
form factor via layout groups, and needs no prefab maintenance when DEV2's
backend shapes change.

If the team later wants designer-editable prefabs:

1. Enter Play mode in `Dashboard.unity`.
2. Drag a runtime row/card from the Hierarchy into this folder to capture it.
3. Replace the corresponding `Build()` call with `Instantiate(prefab)`.
