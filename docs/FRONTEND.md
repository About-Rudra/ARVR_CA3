# Frontend — Admin Dashboard (DEV 3)

Unity 2022.3.9f1, UGUI only (`UnityEngine.UI` + `UnityEngine.EventSystems`).
No React/HTML, no TextMeshPro, no third-party UI packages.

## 1. Architecture

Single scene, code-built UI (no fragile serialized references):

- `Assets/Scenes/Dashboard.unity` — minimal scene: one `Dashboard` GameObject
  with `DashboardController`. Everything else is constructed at runtime.
  Regenerate/extend via menu **ARPassport → Build Dashboard Scene**
  (`Assets/Scripts/Dashboard/Editor/DashboardSceneBuilder.cs`).
- `Assets/Scripts/Dashboard/`
  - `DashboardController.cs` — root: header (title, **Open AR Scanner**,
    **Refresh**), 4 summary cards (Total / Operational / Under Maintenance /
    Issues Open), tabs (Assets / Issues / Stats), loading overlay, error banner
    (Retry + Use sample data), status footer (Online vs Offline sample data).
  - `AssetListController.cs` — search (id/name/category/location), three
    dropdown filters (status/category/location), column header
    (ID/Name/Category/Location/Status/NextMaintenance) + scroll list.
  - `AssetListItemUI.cs` — row widget (View / Edit / Delete).
  - `AssetDetailPanel.cs` — full info + marker ID (with Copy) + maintenance
    history + issues for the asset + Edit / Delete.
  - `AssetFormPanel.cs` — create/edit form + validation + delete goes through
    a confirm dialog.
  - `IssueManagerUI.cs` — filterable issue list, detail, status change to
    open / in_progress / resolved / rejected with color badges.
  - `StatsPanel.cs` — counts by asset status / category / issue status,
    computed locally from the fetched lists.
  - `DashboardModels.cs` — `DashboardAsset`, `DashboardIssue`,
    `DashboardMaintenanceRecord` DTOs + normalization/validation.
  - `DashboardSeedProvider.cs` — tolerant `Resources/seed_assets.json` parser.
  - `DashboardApiAdapter.cs` — backend bridge (see §3).
- `Assets/Scripts/Utils/` (UI helpers only)
  - `DashboardStyle.cs` — palette, type scale, widget factories.
  - `DashboardUIHelper.cs` — scroll-view + dropdown builders, toasts.
  - `DashboardDialog.cs` — modal confirm dialog.
- `Assets/Prefabs/README.md` — rows/cards are code-built; no `.prefab` files.
- `Assets/Resources/seed_assets.json` — offline sample dataset.

The AR scene (`Assets/Scripts/AR/`) is owned by another developer and is only
referenced **by name** (`ARScanner`) for navigation — never directly.

## 2. Setup

1. Open the project in Unity 2022.3.9f1.
2. Open `Assets/Scenes/Dashboard.unity` (or run **ARPassport → Build Dashboard
   Scene** and save it there).
3. Add `Dashboard` (and the AR scene when ready) to
   **File → Build Settings → Scenes In Build**.
4. Press Play. With no backend present, the dashboard starts in
   **Offline sample data** mode automatically.

## 3. Backend contract (DEV 2)

All traffic goes through `DashboardApiAdapter`, which calls
`ArPassport.Core.ApiClient` directly (`using ArPassport.Core;`) with the
documented callback contract and converts Core DTOs (`Asset`,
`MaintenanceRecord`, `Issue`, `Location`) into dashboard DTOs. Status
vocabularies are mapped at the boundary:

| Dashboard | Backend wire |
|---|---|
| `operational` | `operational` |
| `maintenance` | `under_maintenance` (`faulty` also reads as maintenance) |
| `out_of_service` | `retired` |
| issue `open` / `in_progress` / `resolved` | same |
| issue `rejected` (seed/local only) | sent as `closed`; `closed` reads back as `resolved` |

Endpoints consumed:

| Dashboard call | Core method | HTTP |
|---|---|---|
| `FetchAssets` | `GetAssets` | `GET /api/assets` |
| `FetchAssetById` | `GetAssetById` | `GET /api/assets/{id}` |
| `FetchMaintenance` | `GetMaintenance` | `GET /api/assets/{id}/maintenance` |
| `FetchIssues` | `GetIssues()` | `GET /api/issues` |
| `FetchLocations` | `GetLocations` | `GET /api/locations` |
| `UpdateIssueStatus` | `UpdateIssueStatus` | `PATCH /api/issues/{id}/status` |
| `CreateAsset` | `CreateAsset` | `POST /api/assets` |
| `UpdateAsset` | `UpdateAsset` | `PATCH /api/assets/{id}` |
| `DeleteAsset` | `DeleteAsset` | `DELETE /api/assets/{id}` |

Fallback policy: **reads** degrade gracefully — network-level failures
(`NETWORK_OFFLINE`, `NETWORK_ERROR`) and a missing/uninitialized `ApiClient`
fall back to the offline seed (Core `LocalSeedProvider`, then the tolerant
local parser), and the footer shows **Offline sample data**. Other server
errors surface as human-readable messages. **Writes** never fake success:
offline writes return a friendly error. Raw exceptions/codes are logged via
`Debug.LogWarning`, never shown in the UI; the error banner offers **Retry**
and **Use sample data**.

The shared seed file `Assets/Resources/seed_assets.json` uses Core's schema
(`locations`, `assets`, `maintenanceRecords`, `issues`, `reportedAt`) so both
`LocalSeedProvider` and the dashboard parser read the same file; asset
`location` values equal location ids/names, and extra `severity` keys on
issues are consumed by the dashboard only.

## 4. Env / config

- `AR_API_BASE_URL` environment variable overrides the API base URL.
- Otherwise the adapter reads `ArPassport.Core.ApiConfig.BaseUrl` (also tries
  `BaseURL`, `ApiBaseUrl`, `ServerUrl`) via reflection when Core exists.
- Final fallback: `http://localhost:3000` (dev default only; shown in docs,
  never shown to end users as an error).
- No other configuration. No API keys in the client.

## 5. Validation rules (Asset form)

- ID: required on create, immutable on edit, pattern `AST-` + ≥3 digits
  (e.g. `AST-001`).
- Name, category, location: required.
- Status: dropdown (Operational / Under Maintenance / Out of Service).
- Dates: optional, `yyyy-MM-dd`; next maintenance must not precede last.
- Delete: always behind a modal confirm dialog.

## 6. Build & deployment

- **PC (Windows/Mac/Linux):** Build Settings → target platform → add
  `Dashboard.unity` as scene 0 → Build. Dashboard is mouse + keyboard usable
  (buttons are focusable; dialogs focus Cancel by default).
- **Android APK:** same, Android platform. The dashboard tab layout uses
  `CanvasScaler` (1280×800 reference, 0.5 match) + layout groups +
  `ContentSizeFitter`, so phones/tablets reflow; lists scroll by touch.
  `AR_API_BASE_URL` must point at a reachable (LAN/VPN) server — `localhost`
  inside the APK means the phone itself.
- Internet permission / cleartext HTTP: if the backend is plain HTTP on
  Android 9+, allow cleartext (network security config) or serve HTTPS.

## 7. Manual test checklist

- [ ] Cold start, no backend → offline banner + 5 sample assets (mirror of
      `Backend/seedData.js`: AST-001…AST-005, locations CL-6 / FAB-LAB / AR-LAB).
- [ ] Search "cl-6" / "AST-00" filters correctly; each dropdown filters; empty
      result shows the empty state.
- [ ] Create valid asset (online) → appears after refresh; invalid ID / missing
      name / bad date shows inline error.
- [ ] Edit asset; delete asset behind confirm dialog.
- [ ] Issue filter + status change to each of the 4 states; counts update.
- [ ] Kill backend mid-session → friendly error + Retry + sample-data button.
- [ ] 360px-wide emulator: no horizontal clipping of cards/tabs/dialogs.
- [ ] Keyboard-only: Tab reaches all buttons, Enter activates, Esc… (dialog
      has Cancel focused).

## 8. Limitations

- Writes require a live backend; offline mode is read-only sample data (edits
  are not queued for later sync).
- Unity legacy dashboard only: it still sends Rejected as `closed` (§3). The
  backend accepts `rejected` since the DEV4 integration pass, and the web
  dashboard stores and shows it as-is.
- Summary cards use a horizontal layout (squeezes but stays usable on narrow
  phones) rather than wrapping.
- Issue list is global; per-asset issue filtering for large datasets happens
  client-side after `GET /api/issues`.

---

## 9. Web Dashboard — frontend/dashboard/

A lightweight static companion to the Unity dashboard (§1–§8 above, which
are unchanged). Vanilla HTML/CSS/JS, no framework, no build step — it
consumes the same REST contract (`docs/API.md`) directly from the browser.

Files:

- `frontend/dashboard/index.html` — header (title, API URL setting,
  `../ar/` link), 4 summary cards, search + 3 filters, Assets/Issues tabs,
  asset table (ID/Name/Category/Location/Status/NextMaint/Actions), paged
  scroll, loading/empty/error states, footer connection status, detail /
  form / delete-confirm modals, toast region.
- `frontend/dashboard/config.js` — API base resolution:
  `window.__API_BASE_URL` → `localStorage "AR_API_BASE_URL"` →
  `http://localhost:3000` (dev default only; no prod URL hardcoded).
- `frontend/dashboard/app.js` — fetch wrappers, card/table/issue rendering,
  client-side search+filter, detail modal (info + markerId + maintenance +
  issues), validated create/edit form, delete confirm, issue status changer,
  offline sample fallback with banner, friendly toasts (no stacks).
- `frontend/dashboard/styles.css` — responsive (desktop/tablet/mobile),
  cards/tables/badges/modals, visible focus, semantic landmarks.

### Setup

1. Start the backend: `node Backend/server.js` (default
   `http://localhost:3000`).
2. Serve the folder over HTTP (module-free, but `fetch`/CORS behave best
   off `file://`): e.g. `npx serve frontend/dashboard`.
3. Open the page; if the backend is down you get the banner + sample data
   and writes stay disabled until it is back.

### Env

- Inline: `window.__API_BASE_URL = "https://<host>"` before `app.js`.
- Or use the in-page API URL field (persisted to
  `localStorage "AR_API_BASE_URL"`).
- Dev default: `http://localhost:3000`. No production URL is committed.

### API dependency

`GET /api/assets`, `GET /api/assets/:id` (via list cache),
`POST /api/assets`, `PATCH /api/assets/:id`, `DELETE /api/assets/:id`,
`GET /api/assets/:id/maintenance`, `GET /api/issues[?assetId=&status=]`,
`PATCH /api/issues/:id/status`, `GET /api/locations`. Online/offline state is
derived from whether the initial `GET /api/assets` succeeds. Status mapping mirrors §3: asset
`operational|under_maintenance|faulty|retired`; issue
`open|in_progress|resolved|rejected|closed` (`rejected` persisted by the
backend since the DEV4 integration pass).

### Build (no build) / deploy

- No build, no dependencies: deploy `frontend/dashboard/` as-is.
- **Vercel:** point a project at the repo, set Root Directory to
  `frontend/dashboard`, no build command, output = directory root; set the
  API URL via inline `__API_BASE_URL` or the in-page setting.
- **Netlify:** drag-and-drop the folder or set Publish directory to
  `frontend/dashboard`, no build command.
- Mixed-content note: serve the backend over HTTPS when the dashboard is
  on HTTPS, otherwise browsers block the API calls.

### Limitations

- Client-side filtering/pagination (page size 10); fine for lab scale.
- Offline mode is read-only embedded sample data (no queued writes).
- Asset-ID validation is `AST-` + ≥3 digits; location must already exist
  (form offers a dropdown); `nextMaintenance >= lastMaintenance`.
- Category column hides below 480px width to avoid horizontal clipping.
