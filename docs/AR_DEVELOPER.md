# AR Developer Guide — AR Digital Passport (Unity)

Owner: **DEV 1 (Unity AR Engineer)** · Unity **2022.3.9f1** · AR Foundation **5.x** · Android (ARCore)

Scope: `Assets/Scripts/AR/`, `Assets/Scenes/ARScanner.unity`, `Assets/Markers/`
(+ `Assets/StreamingAssets/markers/`), this doc.
Do not edit `Assets/Scripts/Core/` (DEV 2), `Assets/Scripts/Dashboard/` (DEV 3).

## 1. How AR works here

```
Printed marker (MARK-001, 15cm)
  → ARCore image tracking (ARTrackedImageManager)
    → ARMarkerTracker maps reference-image NAME = markerId
      → IAssetProvider.GetAssetByMarker(markerId)
          → live: ApiClient (DEV 2) via reflection bridge (ApiClientAdapter)
          → fallback: SimulationProvider (offline, built-in MARK-001/002/003)
        → PassportPanelController bottom-sheet (passport UI)
          → MaintenanceHistoryUI  (GET /api/assets/:id/maintenance)
          → IssueReporterUI       (POST /api/issues)
```

Key design decisions:

- **No compile-time dependency on DEV 2's code.** `AssetProvider.cs` mirrors the
  shared JSON field names in local DTOs (`AssetDto`, …) and reaches
  `ArPassport.Core.ApiClient` purely via reflection (`ApiClientAdapter`), with
  `JsonUtility` round-trip conversion. Verified against the landed
  `Assets/Scripts/Core/ApiClient.cs`: its methods return `Coroutine` with
  `(code, message)` error callbacks, and `GetAssetByMarker` resolves to an
  assetId **string** (`{assetId}`) — so the adapter chains
  marker → assetId → `GetAssetById` internally within one shared deadline.
- **Scene ships empty, builds itself.** `ARScanner.unity` contains no
  GameObjects; `ARBootstrap` auto-boots through
  `RuntimeInitializeOnLoadMethod` and constructs AR Session / Origin / Camera /
  Canvas / panels at runtime. No prefab GUIDs → no missing-reference breakage.
- **All UI is programmatic UGUI** (`ARUiKit` + legacy `Text`/`Button`/`InputField`,
  default Arial). No TextMeshPro dependency, no inspector wiring.
- **Base URL is never hardcoded.** `BackendConfigReader.BaseUrl` invokes
  `ApiConfig.GetBaseUrl()` via reflection when Core is present (which resolves:
  `AR_API_BASE_URL` env → PlayerPrefs `AR_API_BASE_URL` →
  `Resources/api_config.json` → ScriptableObject default
  `http://localhost:3000`). Without Core it mirrors the same chain, ending at
  an emulator-safe default (`http://10.0.2.2:3000`; plain `localhost` on a phone
  points at the phone itself).

## 2. AR Foundation setup (packages)

In Package Manager (`Window > Package Manager`, Unity Registry):

| Package | Version (2022.3) | Notes |
|---------|------------------|-------|
| `com.unity.xr.arfoundation` | 5.x (e.g. 5.0.7) | Core tracking APIs |
| `com.unity.xr.arcore` | 5.x | ARCore provider for Android |
| `com.unity.xr.management` + `com.unity.xr.core-utils` | as required | Pulled in automatically |
| Input | Old Input Manager is fine | Simulation keys use `Input.GetKeyDown` |

Project Settings after install:

1. `Edit > Project Settings > XR Plug-in Management` → Android tab → tick **ARCore**.
2. `Player > Android`: minimum API 24+, ARM64, `Require ARCore` as needed.
3. The scene needs **no manual AR objects** — `ARBootstrap` creates
   `AR Session`, `AR Session Origin`, `AR Camera` (+ `ARCameraManager`,
   `ARCameraBackground`) and the `ARTrackedImageManager` at runtime.

## 3. Adding a marker (ReferenceImageLibrary steps)

### Option A — serialized library (recommended for device builds)

1. Put the marker PNG in `Assets/Markers/` (or import anywhere).
2. In the PNG import settings: **Read/Write Enabled = true**,
   disable mipmaps stripping (keep default), NPOT → keep `None` scaling issues away
   by using square POT textures (ours are 1024² RGB).
3. `Assets > Create > XR > Reference Image Library`, name it **`ARMarkers`**
   (matches `ARBootstrap.referenceLibraryResourceName`; it is loaded from a
   `Resources` folder — move/keep a copy at `Assets/Resources/ARMarkers.asset`).
4. `Add Image` → drag the PNG, set **Name = markerId** (`MARK-004`),
   **Physical Size = 0.15 m** (or your print size; keep
   `ARBootstrap.markerPrintWidthMeters` in sync).
5. Register the asset in the backend with the same `markerId`.
6. Build & run; `ARBootstrap` assigns the library automatically.

### Option B — runtime library (no Editor baking)

`ARBootstrap` falls back (device builds only) to building a
`MutableRuntimeReferenceImageLibrary` from `Assets/StreamingAssets/markers/*.png`
via `ScheduleAddImageWithValidationJob` at `markerPrintWidthMeters`.
Constraints: needs an ARCore version with mutable-library support and
readable PNGs; failures only degrade to SIM mode, never crash.

### Option C — Editor testing (no device)

Press Play in `ARScanner.unity` and use keys **1 / 2 / 3**, the on-screen
**SIM 1/2/3** buttons, or the menu `AR Passport > Simulate MARK-00x`.

## 4. API dependencies (shared contract — field names frozen, see docs/API.md)

| Call | Route | Used in |
|------|-------|---------|
| `GetAssetByMarker(markerId)` | `GET /api/assets/marker/:markerId` → `{assetId}`, then `GET /api/assets/:id` | `ARMarkerTracker` (two-step inside `ApiClientAdapter`) |
| `GetAssetById(id)` | `GET /api/assets/:id` | provider interface (Dashboard reuse) |
| `GetMaintenance(assetId)` | `GET /api/assets/:id/maintenance` → `[{id,assetId,date,type,technician,notes}]` | `MaintenanceHistoryUI` |
| `ReportIssue(draft)` | `POST /api/issues` `{assetId,title,description,reportedBy}` → issue + `{id,status,reportedAt}` | `IssueReporterUI` |

Asset JSON: `{id,name,category,location,status,description,imageUrl,modelUrl,lastMaintenance,nextMaintenance,markerId}`.
`status` ∈ `operational|under_maintenance|faulty|retired` (badge: green / amber / red / grey).
Errors use the `{error:{code,message}}` envelope (`MARKER_NOT_FOUND`,
`ASSET_NOT_FOUND`, `NETWORK_*`); the tracker treats *not-found* as the
"NOT LINKED" panel state, everything else as an error message.
Every request is bounded by `ApiConfig.GetTimeoutSeconds()` (default 10 s);
`ARBootstrap` pings `GET /health` (`{ok:true}`) at startup for the banner.

## 5. Editor simulation testing

1. Open `Assets/Scenes/ARScanner.unity`, press Play (no device, no backend needed).
2. Banner shows `OFFLINE SIMULATION`; tap **SIM 1/2/3** (or keys 1/2/3):
   - bottom sheet shows the asset, colour-coded status badge
     (green operational / amber maintenance / red fault),
   - `View Details` expands description + photo placeholder + model note,
   - `Maintenance History` opens the scroll list (simulated records),
   - `Report Issue` submits to the simulation provider (returns `SIM-*` id),
   - `Close` clears the panel.
3. With the backend running (`http://localhost:3000`, see `docs/API.md`), the
   adapter engages automatically; the banner shows `Connected: <baseUrl>`.
   Override the URL without rebuilding via PlayerPrefs `AR_API_BASE_URL`
   (or `BackendConfigReader.SetOverride(...)`, or `AR_API_BASE_URL` env) —
   use your LAN IP for a real phone, `10.0.2.2` only works from the emulator.

## 6. Android build

- No HTTPS requirement for native builds (unlike WebAR): plain `http://` to a
  LAN/emulator backend works; add `android:usesCleartextTraffic` only if your
  target SDK setup blocks cleartext (Unity sets this via `Player Settings >
  Allow downloads over HTTP` / custom manifest on API 28+ — test on device).
- Camera permission is requested at runtime (`CheckCameraPermission`); denial
  shows a blocking notice but SIM mode keeps working.
- Physical device needs Google Play Services for AR (ARCore); the banner reports
  `Unsupported` on incompatible phones.

## 7. File map (DEV 1 owned)

| File | Purpose |
|------|---------|
| `Assets/Scripts/AR/AssetProvider.cs` | DTOs, `IAssetProvider`, `SimulationProvider`, `ApiClientAdapter`, `BackendConfigReader`, `ProviderFactory` |
| `Assets/Scripts/AR/ARMarkerTracker.cs` | `ARTrackedImageManager` wrapper, lookup + timeout + sim keys 1/2/3 |
| `Assets/Scripts/AR/ARUiKit.cs` | Programmatic-UGUI helpers, status colours |
| `Assets/Scripts/AR/PassportPanelController.cs` | Bottom-sheet passport UI + photo loader + placeholders |
| `Assets/Scripts/AR/ARBootstrap.cs` | Runtime scene construction, permission/backend/library checks |
| `Assets/Scripts/AR/MaintenanceHistoryUI.cs` | Maintenance scroll list overlay |
| `Assets/Scripts/AR/IssueReporterUI.cs` | Issue form overlay (POST /api/issues) |
| `Assets/Scripts/AR/Editor/ARSceneSetupHelper.cs` | `AR Passport` menu: scene setup + simulate entries |
| `Assets/Scenes/ARScanner.unity` | Intentionally empty scene (auto-boot at runtime) |
| `Assets/Markers/` | `MARK-001..005.png`, `generate_markers.py`, `README.md` |
| `Assets/StreamingAssets/markers/` | APK-bundled copies for the runtime-library fallback |

## 8. Limitations (known, accepted)

1. **No runtime 3D model viewer.** `modelUrl` is displayed as text with a pointer
   to the desktop Dashboard; loading remote GLB/GLTF at runtime (glTFast) is out
   of scope for this milestone.
2. **Photo panel only.** Remote `imageUrl`s load via `UnityWebRequestTexture`
   with a 10 s timeout; offline/broken links show a placeholder, never a blank box.
3. **Simulation data ≠ backend data.** `SimulationProvider` records are illustrative;
   empty real responses render an explicit "No maintenance records" state.
4. **Runtime image library is best-effort.** Prefer the baked `ARMarkers`
   serialized library for production; the StreamingAssets fallback depends on
   ARCore mutable-library support.
5. **Status colours follow the backend vocabulary** (`operational`→green,
   `under_maintenance`→amber, `faulty`→red, `retired`→grey; substring match,
   case-insensitive) — see `ARUiKit.StatusColor`. New statuses need mapping there.
6. **Compiles against AR Foundation 5.x.** The scene degrades gracefully at
   *runtime* without AR hardware, but building requires the ARF/ARCore packages.

---

## 9. WebAR (A-Frame + MindAR) — `frontend/ar/`

Browser-based companion to the Unity scanner above. Unity code, backend API,
and seed data are untouched — this is a static frontend that consumes the same
frozen contract (`docs/API.md`). No auth, no secrets.

### 9.1 How it works

```
Printed marker (MARK-001, 15 cm)
  → MindAR image tracking (CDN: A-Frame 1.4.2 + mind-ar 1.2.5)
    → targetFound (targetIndex 0..4 → MARK-001..005 via TARGET_INDEX_TO_MARKER)
      → resolveMarker()  GET /api/assets/marker/:markerId → {assetId}
        → fetchAsset()   GET /api/assets/:id → passport bottom-sheet
          → fetchMaintenance()  GET /api/assets/:id/maintenance (on demand)
          → reportIssue()      POST /api/issues {assetId,title,description,reportedBy} (on demand)
```

Files:

| File | Purpose |
|------|---------|
| `frontend/ar/index.html` | A-Frame + MindAR scene, start overlay (camera explainer), scan hint, passport bottom-sheet, maintenance/report modals, manual test bar |
| `frontend/ar/config.js` | `API_BASE_URL` = `window.__API_BASE_URL` → `localStorage AR_API_BASE_URL` → `http://localhost:3000` fallback; `api()`/`apiUrl()` helpers. `?api=` query param persists an override. No prod URL is hardcoded |
| `frontend/ar/app.js` | `resolveMarker`, `fetchAsset`, `fetchMaintenance`, `reportIssue` (+ `ARPassport` namespace); error handling for camera denied / unknown marker / backend down / invalid asset / timeout / missing image-model; `?marker=MARK-001` deep link |
| `frontend/ar/styles.css` | Mobile-first, bottom-sheet passport, status badges (operational green / under_maintenance amber / faulty red / retired grey), overlays, 44 px accessible tap targets |
| `frontend/ar/targets/` | `README.md` (mapping + compiler instructions), `src/MARK-001..005.png` sources, compiled `targets.mind` (5 targets, checked in) |

### 9.2 Marker setup

`targets.mind` (5 targets, MARK-001…MARK-005 = `targetIndex` 0…4) is
compiled and checked in, so static hosts serve it with no build step. To change
markers: `python Assets/Markers/generate_markers.py` (writes all 5 PNGs into
`frontend/ar/targets/src/`), then `cd tools/mindar-compiler; npm install;
npm run compile` and commit the new `targets.mind` (see
`frontend/ar/targets/README.md`). Manual simulation keeps working without it.

### 9.3 API dependencies (same frozen contract as Unity §4)

Only these endpoints are consumed: `GET /api/assets/marker/:markerId`,
`GET /api/assets/:id`, `GET /api/assets/:id/maintenance`,
`POST /api/issues`, `GET /health` (banner). Error envelope
`{error:{code,message}}` is parsed (`MARKER_NOT_FOUND` → "not linked" warning;
network/timeout → backend-unavailable message). 10 s per-request timeout via
`AbortController`.

### 9.4 Local testing

```bash
# terminal 1 — backend (from repo root)
node Backend/server.js        # http://localhost:3000

# terminal 2 — static host for the WebAR app (camera needs localhost or HTTPS)
cd frontend/ar && python3 -m http.server 8000
```

Then open `http://localhost:8000/?marker=MARK-001` (no camera needed), or use
the on-page marker dropdown + **Simulate scan**. For the camera flow on a phone,
serve over HTTPS or use a LAN URL and set the API base in the start-overlay
`API base URL` field (persisted to `localStorage`).

### 9.5 Production deployment (HTTPS required)

Mobile browsers gate `getUserMedia` behind a **secure context**: deploy
`frontend/ar/` as a static site (Vercel / Netlify / any static host serves
HTTPS by default) and point it at the deployed backend via **one** of, in
priority order: `window.__API_BASE_URL` (inject before `config.js` in a
hosting snippet), `localStorage AR_API_BASE_URL`, or `?api=https://…` once
(which persists). The `http://localhost:3000` fallback only ever applies when
no override is set — no prod URL is baked into the code.

### 9.6 Browser limitations

1. **HTTPS + camera-capable browser required** for live tracking; denial or
   insecure context falls back to manual simulation (never a dead page).
2. **No runtime 3D model viewer** (same as Unity §8): `modelUrl` renders as a
   link/placeholder note.
3. **Remote `imageUrl`s** use `onerror` placeholder fallback; seed data uses
   `example.com` URLs that will not resolve — placeholder shows instead.
4. **`.mind` missing** (fresh clone) disables camera tracking only; all panels
   and manual mode still work.
5. Desktop browsers have no rear camera / motion sensors — test camera flow on
   a real phone; use `?marker=` on desktop.
