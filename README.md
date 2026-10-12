# AR Passport — WebAR + Dashboard over Express/Postgres (Unity legacy kept)

Problem: field equipment needs a scannable identity — point a phone at a
machine marker and see its passport (specs, service history), file issues, and
have the back-office dashboard reflect it live. This repo is a **web-first**
build: a browser WebAR scanner and a static web dashboard over a free-tier
Express/Postgres backend. The original Unity project (`Assets/`) is kept as a
legacy alternative (Android ARCore APK path) and must not be deleted.

Features:

- WebAR scanner (`frontend/ar/`, DEV1, landed): A-Frame + MindAR image tracking
  (`MARK-001..005` → `targetIndex` 0..4), marker → asset passport bottom-sheet,
  maintenance history, issue reporting, `?marker=MARK-001` deep link + manual
  **Simulate scan** fallback (works with no camera). Live camera tracking of
  all 5 markers via the compiled `frontend/ar/targets/targets.mind`.
- Web dashboard (`frontend/dashboard/`, DEV3, landed): vanilla HTML/CSS/JS, no
  framework, no build step — asset table + filters + detail/maintenance/issues,
  create/edit/delete, issue status changer, offline sample fallback with banner.
- REST API (`Backend/server.js`, DEV2, landed): `GET /api/assets`,
  `/marker/:markerId`, `/:id/maintenance`, `GET/POST /api/issues`,
  `PATCH /api/issues/:id/status`, `GET /api/locations`, `GET /health` with the
  `{error:{code,message}}` envelope and in-memory seed fallback (10/10 jest tests).
- Unity legacy (`Assets/`, alternative path): ARCore scanner + dashboard scenes,
  `ApiClient`/`ApiConfig`/`LocalSeedProvider` over `seed_assets.json`.

Architecture (ASCII):

```
  [WebAR scanner (frontend/ar)] --\
                                     >-- [Express /api] ---> [Postgres]
  [Web dashboard (frontend/dash)] --/          \---> [in-memory seed fallback]
  [Unity AR / Dashboard (legacy)] -------------/
```

Single demo truth: `Backend/seedData.js` = `database/seed.sql` =
`Assets/Resources/seed_assets.json` = dashboard offline sample =
`frontend/ar` marker list — 5 assets (`AST-001..005` ↔ `MARK-001..005`),
3 locations (`CL-6`, `FAB-LAB`, `AR-LAB`), 6 maintenance rows, 3 issues.

Stack: Node 20 + Express + `pg` (backend); Postgres (Supabase/Neon free tier);
vanilla JS + A-Frame 1.4.2 + MindAR 1.2.5 (CDN) for the frontends; Unity
2022.3.9f1 + AR Foundation 5.0.7 (legacy); jest + supertest for API tests.

Repo structure:

```
frontend/ar/            DEV1 WebAR scanner (index.html, config.js, app.js, styles.css, targets/)
frontend/dashboard/     DEV3 web dashboard (index.html, config.js, app.js, styles.css)
Backend/                DEV2 Express API (server.js, db.js, seedData.js, routes/, tests/api.test.js)
database/               DEV2 schema.sql + seed.sql (Supabase/Neon compatible)
docs/                   DEV4 API.md, AR_DEVELOPER.md, BACKEND.md, FRONTEND.md, TEST_PLAN.md, DEPLOYMENT.md
tests/                  DEV2 api.test.js (mirror) + DEV4 validate_integration.ps1, e2e_flow.js, browser/ (headless QA)
scripts/                DEV4 write-frontend-env.js (deploy-time API URL -> frontend env.js)
tools/mindar-compiler/  DEV4 compiles targets.mind from the marker PNGs (npm run compile)
render.yaml             DEV4 Render Blueprint for Backend/ (frontends: frontend/*/netlify.toml)
Assets/                 Unity legacy — DO NOT DELETE (AR/Core/Dashboard scripts, seed_assets.json, scenes, markers)
.github/workflows/     DEV4 CI (integration check + backend tests)
.env.example            all env vars (DEV4); Backend/.env.example mirrors backend vars
```

Setup (fresh clone):

```powershell
git clone <repo-url>; cd ca3-nimi
cd Backend; npm install
copy ..\.env.example .env            # leave DATABASE_URL empty for in-memory seed store
npm test                              # jest + supertest, no DB needed — must pass 10/10
npm start                             # http://localhost:3000, check GET /health
# Real DB (optional for local):
psql $env:DATABASE_URL -f ..\database\schema.sql -f ..\database\seed.sql
# Frontend smoke (no deploy needed):
cd ..\frontend\ar; python3 -m http.server 8000        # open http://localhost:8000/?marker=MARK-001
cd ..\dashboard; python3 -m http.server 8001          # open http://localhost:8001/
```

Env vars: see root `.env.example` — backend origin convention (no `/api`
suffix), `PORT=3000`, `DATABASE_URL` (empty = seed store), `CORS_ORIGIN`
(empty = allow all for local dev incl. `localhost:<any-port>` frontends;
prod = deployed frontend origin(s)), `API_BASE_URL` (alias
`AR_API_BASE_URL`, also the `localStorage`/Unity PlayerPrefs key). Web apps
resolve `window.__API_BASE_URL` → `localStorage "AR_API_BASE_URL"` →
`http://localhost:3000` dev default (AR page also takes `?api=` once).
Never commit `.env`.

DB setup: `database/schema.sql` then `database/seed.sql` (idempotent) against
Supabase/Neon, connection string into `DATABASE_URL`. Counts: 3 locations,
5 assets, 5 markers, 6 maintenance rows, 3 issues. Without a DB the API runs
on the in-memory seed (ephemeral — restart reseeds).

Frontend / AR / backend setup: backend `npm start` on `:3000`; serve each
frontend folder over HTTP (`python3 -m http.server` or `npx serve`) — camera
needs `localhost` or HTTPS. For a phone on LAN, set the page's API URL field
to `http://<laptop-lan-ip>:3000`. For prod, deploy frontends to Vercel/Netlify
(HTTPS) and point them at the Render/Railway backend origin (same HTTPS
requirement to avoid mixed-content blocks). Live URLs: _(placeholders —
nothing deployed yet)_.

Testing (details + latest results in `docs/TEST_PLAN.md`):

```powershell
cd Backend; npm test                                   # 10 jest tests, no DB
node tests/e2e_flow.js http://localhost:3000           # 34-check API flow (also works on the deployed backend)
cd tests/browser; npm install                          # headless Chrome/Edge QA (local browser)
node ui_flow.js http://localhost:8000 http://localhost:8001 http://localhost:3000   # 14-check demo flow
python make_marker_video.py; node marker_tracking.js http://localhost:8000 http://localhost:3000  # 5/5 markers via fake camera
pwsh tests/validate_integration.ps1                    # static contracts + seed consistency
```

CI (`.github/workflows/validate.yml`) runs the static checks, jest, and the
E2E flow against a real Postgres 15 built from `schema.sql` + `seed.sql`.
Failure cases covered: backend down, invalid marker, nonexistent asset,
malformed issue, DB down/unreachable (503), empty DB, camera denied.

Deployment: exact steps in `docs/DEPLOYMENT.md` — Postgres on Supabase/Neon
(run `schema.sql` + `seed.sql`), `Backend/` on Render via the checked-in
`render.yaml` Blueprint, `frontend/ar` + `frontend/dashboard` as two Netlify
sites (base directory = folder; `netlify.toml` writes `env.js` from the
`API_BASE_URL` env var), Unity APK as alternative. Health checks
(`GET /health`, marker spot-check, `?marker=` render, dashboard round-trip)
and rollback (host rollback / re-point API URL) included.

Demo script (3–5 min): open the dashboard → open the AR scanner
`?marker=MARK-001` (no camera needed) → Epson projector (`AST-001`) passport
opens → **Maintenance History** (MNT-001/002) → **Report Issue** ("Lamp
flicker") → back to dashboard Issues tab → new issue visible → set
`in_progress` → refresh → status persists.

Team contributions: DEV1 `frontend/ar/` + WebAR section of
`docs/AR_DEVELOPER.md` (Unity `Assets/Scripts/AR/` legacy); DEV2 `Backend/` +
`database/` + `Assets/Scripts/Core/` + `tests/api.test.js`; DEV3
`frontend/dashboard/` + `docs/FRONTEND.md` web section (Unity dashboard
scripts legacy); DEV4 (integration, QA, deployment): `README.md`, `docs/`
TEST_PLAN/DEPLOYMENT, `.env.example`, `.github/workflows`, seed-consistency
fixes, this quality gate; integration fixes (Postgres SSL/transactions/
timeouts, `rejected` status, AR overlay bug, MARK-004/005 + trackable
MARK-001/003, `targets.mind`), deploy config (`render.yaml`, `netlify.toml`,
`scripts/write-frontend-env.js`), `tests/e2e_flow.js`, `tests/browser/`,
`tools/mindar-compiler/`.

Known limitations: seed `imageUrl`s are `example.com` placeholders (placeholder art shows);
in-memory mode is ephemeral (restart reseeds; use Postgres for persistence);
**no auth** on API routes (`admins` table reserved); marker tracking verified with
Chrome's fake camera only — a real phone + printed markers test is still
required (re-print `MARK-001`/`MARK-003`: their artwork changed); issue IDs
are time-based (`ISS-` + 6 digits), unique in practice but not guaranteed;
no DELETE for issues; Render free cold starts
delay first connect (~30–60 s, banners cover it).
