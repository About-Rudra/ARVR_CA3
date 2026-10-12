# TEST PLAN — AR Passport (DEV4 QA)

End-to-end target flow: **deployed app → AR scanner (`?marker=MARK-001`) →
`GET /api/assets/marker/:markerId` → `GET /api/assets/:id` → passport →
`GET /api/assets/:id/maintenance` → `POST /api/issues` → dashboard shows the
issue → `PATCH /api/issues/:id/status` → reflects updated.**

Owners: DEV1 = WebAR (`frontend/ar/` A-Frame+MindAR, `?marker=` deep link,
manual `Simulate scan` fallback), DEV2 = API/DB (`Backend/server.js`,
`Backend/seedData.js`, `database/schema.sql`+`seed.sql`, `tests/api.test.js`),
DEV3 = web dashboard (`frontend/dashboard/` vanilla, real API + offline
sample fallback), DEV4 = this plan + `tests/validate_integration.ps1` +
deployment docs. Unity under `Assets/` is legacy (APK alternative path).

Run the automated gates first (no device needed):

```powershell
cd Backend; npm test          # jest + supertest over tests/api.test.js (in-memory seed, no DB needed) — must pass 10/10
node --check ../frontend/ar/app.js; node --check ../frontend/ar/config.js
node --check ../frontend/dashboard/app.js; node --check ../frontend/dashboard/config.js
powershell -ExecutionPolicy Bypass -File ../tests/validate_integration.ps1
powershell -ExecutionPolicy Bypass -File ../tests/validate_integration.ps1 -ProbeApi  # also probes a running backend
```

Manual API probe of the target flow (backend on `http://localhost:3000`):

```powershell
(Invoke-WebRequest http://localhost:3000/health -UseBasicParsing).Content
(Invoke-WebRequest http://localhost:3000/api/assets/marker/MARK-001 -UseBasicParsing).Content  # -> {"assetId":"AST-001"}
(Invoke-WebRequest http://localhost:3000/api/assets/AST-001 -UseBasicParsing).Content
(Invoke-WebRequest http://localhost:3000/api/assets/AST-001/maintenance -UseBasicParsing).Content  # >= 2 rows
$b = @{ assetId='AST-001'; title='QA probe'; description='verify dashboard visibility'; reportedBy='qa' } | ConvertTo-Json
$id = (Invoke-RestMethod http://localhost:3000/api/issues -Method POST -Body $b -ContentType 'application/json').id
Invoke-RestMethod "http://localhost:3000/api/issues/$id/status" -Method PATCH -Body (@{status='in_progress'} | ConvertTo-Json) -ContentType 'application/json'
Invoke-RestMethod "http://localhost:3000/api/issues?assetId=AST-001"   # new issue visible with status in_progress
```

## Frozen contracts (validator + jest enforced)

- Backend origin `http://localhost:3000` for local dev (no `/api` suffix);
  health at **`GET /health`** → `200 {"ok":true,...}` (root, not `/api/health`).
- Error envelope `{error:{code,message}}` (`MARKER_NOT_FOUND`, `ASSET_NOT_FOUND`,
  `ISSUE_NOT_FOUND`, `VALIDATION_ERROR`, `ASSET_CONFLICT`, `MARKER_CONFLICT`,
  `NOT_FOUND`, `INTERNAL_ERROR`).
- Marker lookup `GET /api/assets/marker/:markerId` → `200 {"assetId":"AST-00x"}`;
  unknown marker → `404 MARKER_NOT_FOUND`.
- Asset shape: `{id,name,category,location,status,description,imageUrl,modelUrl,
  lastMaintenance,nextMaintenance,markerId}`; `status` ∈
  `operational|under_maintenance|faulty|retired`.
- Issue status update is **`PATCH /api/issues/:id/status`** with
  `{"status":"open|in_progress|resolved|closed"}`.
- Web API-base resolution: **`window.__API_BASE_URL`** → **`localStorage
  "AR_API_BASE_URL"`** → `http://localhost:3000` dev default (AR page also
  accepts `?api=` once, which persists). No production URL is hardcoded.
- CORS: open by default for dev (`CORS_ORIGIN` empty); restrict to deployed
  frontend origin(s) in production.
- Demo seed (single truth = `Backend/seedData.js` = `database/seed.sql` =
  `Assets/Resources/seed_assets.json` = dashboard offline SAMPLE =
  `frontend/ar` marker list): 5 assets `AST-001..005` ↔ `MARK-001..005`,
  3 locations (`CL-6`, `FAB-LAB`, `AR-LAB`), 6 maintenance rows, 3 issues.
- `MARK-001` = Epson EB-X49 projector (`AST-001`, location `CL-6`) — the demo marker.
- `targets.mind` (5 targets, MARK-001..005) is checked in; manual simulation +
  `?marker=` work with or without it.

## Checklists A–N (web flow)

| # | Area | Steps | Expect | Owner |
|---|------|-------|--------|-------|
| A | Fresh setup | Fresh clone → Node 20 LTS → `cd Backend; npm install` → `copy ..\.env.example .env` (leave `DATABASE_URL` empty) → `npm test` | 8/8 pass, no DB needed | DEV4/DEV2 |
| B | Backend up | `npm start` → `GET /health` | `200 {"ok":true}`; log shows `postgres: false` (in-memory) | DEV2 |
| C | DB migrate+seed | `psql $DATABASE_URL -f ../database/schema.sql -f ../database/seed.sql` → count | 3 locations, 5 assets incl. `MARK-001`, 6 maintenance rows, 3 issues | DEV2 |
| D | Frontend serve | `cd frontend/ar && python3 -m http.server 8000` (or `npx serve`); same for `frontend/dashboard` on another port; open pages | Both pages load with no console errors; backend banner shows connected origin | DEV1/DEV3 |
| E | AR scanner (no camera) | Open AR page `/?marker=MARK-001`, or pick `MARK-001` + **Simulate scan** | Passport bottom-sheet shows Epson projector (`AST-001`, Operational badge) | DEV1 |
| F | Marker→asset | `resolveMarker('MARK-001')` → `GET /api/assets/marker/MARK-001` → `GET /api/assets/AST-001` | `{assetId:"AST-001"}` then full asset with `markerId:"MARK-001"` | DEV1+DEV2 |
| G | Asset passport | Passport shows name, id, marker, category, location, dates, description; **View Details** toggles; broken `imageUrl` shows placeholder, never blank | Matches `docs/API.md` asset shape | DEV1 |
| H | Maintenance | **Maintenance History** → `GET /api/assets/AST-001/maintenance` | ≥2 records (MNT-001/002); assets without records show explicit empty state | DEV2+DEV1 |
| I | Issue create → dashboard visible → status update | AR **Report Issue** (`POST /api/issues {assetId,title}`) → `201` → open dashboard Issues tab (or `GET /api/issues?assetId=...`) → change status to `in_progress` (`PATCH /:id/status`) → refresh | New issue visible in dashboard; status persists after refresh | ALL |
| J | Error handling (backend down) | Stop backend, reload both pages / retry scan | AR: backend-unavailable toast + backend-status banner; dashboard: offline banner + sample data, writes disabled, no crash | DEV4+DEV1/3 |
| K | Invalid marker | `GET /api/assets/marker/MARK-999` or scan/type unknown marker | `404 MARKER_NOT_FOUND`; AR shows "not linked" warning, stays on page | DEV1+DEV2 |
| L | Nonexistent asset | `GET /api/assets/NOPE-999` | `404 ASSET_NOT_FOUND` envelope | DEV2 |
| M | Malformed issue | `POST /api/issues` with `{}` / short title / bad `assetId` | `400 VALIDATION_ERROR` (or `404 ASSET_NOT_FOUND`); AR form shows inline message, no exception | DEV2+DEV1 |
| N | Camera denied / mobile / DB down / empty DB / prod | Deny camera → manual mode; phone over HTTPS → camera flow (manual, NOT run in CI — record device/OS when done); kill Postgres → `503 DB_UNAVAILABLE` → error UI; seed zero rows → dashboard empty-state; deployed HTTPS frontend + HTTPS backend → no mixed-content block | Camera denied: helpful message + manual bar; mobile: untested in CI (mark NOT RUN until a real phone test); DB down: offline UI; prod: `?marker=` flow works end-to-end on hosted URLs | DEV4+DEV2 |

## Automated suites (DEV4)

| Suite | Command | Covers |
|-------|---------|--------|
| API unit/contract | `cd Backend; npm test` | 10 jest+supertest tests, in-memory store |
| API end-to-end | `node tests/e2e_flow.js http://localhost:3000` (any backend origin, incl. deployed) | 34 checks: health, seed, marker→asset (3 markers), maintenance, issue create → list → `in_progress` persists, all statuses, asset CRUD + 409 conflicts, error envelopes (B, C, F–M) |
| Browser demo flow | `cd tests/browser; npm install; node ui_flow.js <ar> <dashboard> <api>` | 14 checks in headless Chrome: `?marker=` passport, maintenance, Report Issue, invalid marker, dashboard summary, issue visible, status persists after reload, `rejected`, 360 px layout, no JS errors (D, E, G–K) |
| Live marker tracking | `cd tests/browser; python make_marker_video.py; node marker_tracking.js <ar> <api>` | Chrome fake camera plays each marker; real MindAR + `targets.mind` must open the right passport for all 5 markers (F) |
| Integration/static | `pwsh tests/validate_integration.ps1` | file contracts, seed consistency, ports/keys |
| CI | `.github/workflows/validate.yml` | static checks + jest + e2e against a Postgres 15 service (schema+seed) and in-memory |

## Results log

**2026-10-12 — DEV4 integration pass (local, branch `feature/integration-deployment`)**

| Area | Result |
|------|--------|
| A/C Fresh DB | `schema.sql` + `seed.sql` on a clean Postgres 15: OK; seed re-run idempotent; counts 3/5/5/6/3; FK rejects orphan issue |
| B/F–M API | `e2e_flow.js` 34/34 against Postgres and against in-memory; `npm test` 10/10 |
| D–K UI | `ui_flow.js` 14/14 against Postgres-backed API |
| F Marker recognition | `marker_tracking.js` 5/5 (fake camera, real MindAR) |
| Failures | backend down (AR toast + dashboard offline banner), invalid marker, nonexistent asset, malformed issue/JSON, DB refused + DB host unreachable (503 within timeout), empty DB (empty lists, `MARKER_NOT_FOUND`): all graceful |
| M Real phone | **NOT RUN** — needs a deployed HTTPS URL + printed markers |
| N Production | **NOT RUN** — not deployed yet |

Bugs found and fixed in this pass: forced SSL broke every local/non-SSL
Postgres (500 on all routes); asset create/edit silently re-pointed a marker
already in use (and deleting that asset then removed `MARK-001`); no DB
connect timeout (requests hung forever); duplicate asset id → 500 instead of
409; `rejected` issue status from the spec missing (dashboard showed it as
Resolved); AR `#loader`/`#start-overlay` never hid (CSS `display:flex`
overrode `[hidden]`), blocking Maintenance/Report after every scan;
`MARK-001`/`MARK-003` not recognised by MindAR (too few features);
`targets.mind` and `MARK-004/005` missing; documented MindAR CLI did not exist.

## Failure-case matrix (expected behavior, all verified except phone)

| Case | Probe | Expect |
|------|-------|--------|
| Backend down | Stop `node server.js`, reload pages | AR toast `Backend unavailable`, banner `Backend unreachable`; dashboard offline banner + sample data; writes disabled |
| Invalid marker | `GET /api/assets/marker/MARK-999` | `404 {"error":{"code":"MARKER_NOT_FOUND",...}}` |
| Nonexistent asset | `GET /api/assets/NOPE-999` | `404 {"error":{"code":"ASSET_NOT_FOUND",...}}` |
| Malformed issue | `POST /api/issues {}` / `{assetId,title:"x"}` / `{assetId:"AST-999",title:"Valid"}` | `400 VALIDATION_ERROR` / `400` / `404 ASSET_NOT_FOUND` |
| Bad issue status | `PATCH /api/issues/ISS-001/status {"status":"bogus"}` | `400 VALIDATION_ERROR` |
| Unknown issue | `PATCH /api/issues/ISS-999/status` | `404 ISSUE_NOT_FOUND` |
| DB down (Postgres mode) | Kill Postgres / bad `DATABASE_URL` / unreachable host | `503 DB_UNAVAILABLE` envelope within `DB_CONNECT_TIMEOUT_MS` (10 s default, never hangs); `GET /health` → `{ok:true, db:"down"}`; frontends show their error UI |
| Empty DB | Fresh Postgres, schema only, no seed | Dashboard empty-state ("no assets"); AR scan → `MARKER_NOT_FOUND` until `seed.sql` is run |
| Camera denied | Deny permission on Start | Blocking explainer + manual simulation bar stays usable |
| Insecure context | Plain-HTTP URL on a phone (not localhost) | `getUserMedia` blocked → manual mode; fix = serve frontend over HTTPS (Vercel/Netlify default) |

## Sign-off

Pre-demo gate: `npm test` 10/10 green + `node tests/e2e_flow.js <backend>` PASS + `node --check` on all four frontend
JS files green + A–I green against the target backend + J–M spot-checked.
Record backend URL, commit hash, and (for phone runs) device model, OS, and
browser version with each run. Known gaps at handoff: seed `imageUrl`s are `example.com` placeholders
(placeholder art shows instead); in-memory mode is ephemeral (restart reseeds);
**no auth** on API routes (`admins` table reserved); phone camera flow is
**NOT RUN** in CI — do not claim it passes until tested on a real device.
