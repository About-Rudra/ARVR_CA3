# TEST PLAN — AR Passport (DEV4 QA)

End-to-end flow: **Dashboard → AR scan `MARK-001` → passport → maintenance →
report issue → dashboard shows `in_progress`**. Owners: DEV1 = AR
(`AssetProvider` + `ARBootstrap` runtime scene builder + `ARMarkerTracker`
with Editor sim keys `1/2/3`), DEV2 = Core/Backend/DB
(`ApiClient`/`ApiConfig`/`Models`/`LocalSeedProvider`, `Backend/`,
`database/`, `tests/api.test.js`), DEV3 = Dashboard bridge/UI (`DashboardApiAdapter` via reflection over
`ApiClient`, models, seed provider; **scenes `Boot`/`Dashboard` still missing**), DEV4 = this
plan + `tests/validate_integration.ps1` + `Assets/Tests/IntegrationCheck.cs`
(+ DEV2 `DataTests.cs` for seed shapes).

Run the automated gate first (no Unity needed):

```powershell
powershell -ExecutionPolicy Bypass -File ./tests/validate_integration.ps1
powershell -ExecutionPolicy Bypass -File ./tests/validate_integration.ps1 -Strict    # pre-demo: warnings are errors
powershell -ExecutionPolicy Bypass -File ./tests/validate_integration.ps1 -ProbeApi  # also probe backend endpoints
```

Backend unit tests (DEV2, needs `npm install` in `Backend/`):

```powershell
cd Backend; npm test          # jest + supertest over tests/api.test.js (in-memory seed, no DB needed)
```

In-Unity: Test Runner → EditMode → `ArPassport.Tests.IntegrationCheck`
(+ `ArPassport.Tests.DataTests.RunAll()` from an editor button).

## Frozen contracts (validator-enforced)

- Backend origin `http://localhost:3000` (no `/api` suffix); health at
  **`GET /health`** → `200 {"ok":true,...}` (root, not `/api/health`).
- Error envelope `{error:{code,message}}` (`MARKER_NOT_FOUND`, `ASSET_NOT_FOUND`,
  `VALIDATION_ERROR`, `ISSUE_NOT_FOUND`, ...).
- Marker lookup `GET /api/assets/marker/:markerId` → `200 {"assetId":"AST-00x"}`;
  unknown marker → `404 MARKER_NOT_FOUND`.
- Issue status update is **`PATCH /api/issues/:id/status`** with
  `{"status":"open|in_progress|resolved|closed"}`.
- Unity override key **`AR_API_BASE_URL`** (case-sensitive, shared by
  `ApiConfig` and `AppBootstrapper`); base URLs never end with `/api`.
- Offline seed `Assets/Resources/seed_assets.json` mirrors `database/seed.sql`
  (5 assets `AST-001..005` ↔ `MARK-001..005`, locations, maintenance, issues).
- `MARK-001` = Epson EB-X49 projector (`AST-001`, location `CL-6`) — the demo marker.

## Checklists A–N

| # | Area | Steps | Expect | Owner |
|---|------|-------|--------|-------|
| A | Fresh setup | Clone → Unity 2022.3.9f1 + Android module, Node 20 LTS, psql → `npm install` in `Backend/` → open project | No console errors; `ProjectVersion.txt` matches Hub version | DEV4/DEV2 |
| B | Backend up | `copy .env.example Backend\.env`, set `DATABASE_URL` (or leave empty for seed store), `npm start` → `GET /health` | `200 {"ok":true}`; log shows `postgres: true/false` | DEV2 |
| C | Scenes | `Boot` first in Build Settings, then `Dashboard`, `ARScanner`; play Boot | Boot auto-routes to Dashboard; loading screen flashes; missing scene → clear error, no crash | DEV4+DEV3 |
| D | DB migrate+seed | `psql $DATABASE_URL -f database/schema.sql` then `-f database/seed.sql` → count | 3 locations, 5 assets incl. `MARK-001`, 6 maintenance rows, 3 issues | DEV2 |
| E | Dashboard list | Open Dashboard scene, online | All seeded assets render with name + status | DEV3 (WIP) |
| F | AR sim (Editor) | Open ARScanner (intentionally empty scene; `ARBootstrap` auto-builds stack), press `1/2/3` (`ARMarkerTracker`) | Fake detection of `MARK-001/002/003`; passport panel opens | DEV1 |
| G | Marker→asset | Resolve `MARK-001` → `GET /api/assets/AST-001` (or `LocalSeedProvider` offline) | Passport shows Epson projector details | DEV1+DEV2 |
| H | Maintenance | `GET /api/assets/AST-001/maintenance` | ≥2 records (MNT-001/002); empty array (not error) for assets without | DEV2+DEV3 |
| I | Issue create→visible→update | `POST /api/issues {assetId,title}` → `201` → dashboard list → `PATCH /:id/status {in_progress}` | New issue visible; status persists + refreshes | ALL |
| J | Backend down | Stop backend, relaunch app | Offline banner; `NETWORK_ERROR` → `LocalSeedProvider` fallback; no crash | DEV4+DEV2 |
| K | Invalid marker | `GET /api/assets/marker/MARK-999` / unknown sim marker | `404 MARKER_NOT_FOUND`; scanner shows not-found card, stays in scene | DEV1+DEV2 |
| L | Nonexistent asset | `GET /api/assets/NOPE-999` | `404 ASSET_NOT_FOUND`; Unity not-found UI, single log | DEV2+DEV1 |
| M | Malformed issue | `POST /api/issues` with `{}` / bad `assetId` / short title | `400 VALIDATION_ERROR` (or `404 ASSET_NOT_FOUND`); Unity surfaces message, no exception | DEV2+DEV1 |
| N | DB down / empty DB / camera denied / mobile-browser | Kill Postgres / seed zero rows / deny permission / open in mobile browser | DB down: `500 INTERNAL_ERROR` → offline UI; empty DB: dashboard empty-state ("run seed"); camera denied: Android rationale + retry (see `DEPLOYMENT.md`); mobile browser: N/A — native ARCore app, no WebAR path | DEV4+DEV2 |

## Sign-off

Pre-demo gate: validator `-Strict` green (currently 2 warnings: `Boot.unity`
+ `Dashboard.unity` missing) + `npm test` green + A–I green on
target device + J–N spot-checked. Record device model, OS, APK version,
backend URL with each run. Known gaps at handoff: **`Boot`/`Dashboard`
scenes missing (DEV3)** — C/E UI steps are contracts awaiting those files;
**deep-link `PendingMarker` not yet consumed by DEV1** (`AppBootstrapper`
side ready); DEV3 reflection bridge should disambiguate the two
`ApiClient.GetIssues` overloads at runtime.
