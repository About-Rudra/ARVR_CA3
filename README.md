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
  **Simulate scan** fallback (works with no camera; `targets.mind` missing only
  disables live camera tracking).
- Web dashboard (`frontend/dashboard/`, DEV3, landed): vanilla HTML/CSS/JS, no
  framework, no build step — asset table + filters + detail/maintenance/issues,
  create/edit/delete, issue status changer, offline sample fallback with banner.
- REST API (`Backend/server.js`, DEV2, landed): `GET /api/assets`,
  `/marker/:markerId`, `/:id/maintenance`, `GET/POST /api/issues`,
  `PATCH /api/issues/:id/status`, `GET /api/locations`, `GET /health` with the
  `{error:{code,message}}` envelope and in-memory seed fallback (8/8 jest tests).
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
tests/                  DEV2 api.test.js (mirror) + DEV4 validate_integration.ps1
Assets/                 Unity legacy — DO NOT DELETE (AR/Core/Dashboard scripts, seed_assets.json, scenes, markers)
.github/workflows/     DEV4 CI (integration check + backend tests)
.env.example            all env vars (DEV4); Backend/.env.example mirrors backend vars
```

Setup (fresh clone):

```powershell
git clone <repo-url>; cd ca3-nimi
cd Backend; npm install
copy ..\.env.example .env            # leave DATABASE_URL empty for in-memory seed store
npm test                              # jest + supertest, no DB needed — must pass 8/8
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

Testing: `cd Backend; npm test` (8/8); `node --check` on the four frontend
JS files; `tests/validate_integration.ps1` (add `-ProbeApi` with backend up);
manual target-flow probe + full A–N checklists in `docs/TEST_PLAN.md`
(includes failure cases: backend down, invalid marker, nonexistent asset,
malformed issue, DB down, empty DB, camera denied).

Deployment: exact steps in `docs/DEPLOYMENT.md` — static `frontend/ar` +
`frontend/dashboard` on Vercel/Netlify, `Backend/` on Render/Railway,
Postgres on Supabase/Neon, Unity APK as alternative. Health checks
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
fixes, this quality gate.

Known limitations: `targets.mind` missing (compile per
`frontend/ar/targets/README.md`; camera tracking only — manual mode works);
seed `imageUrl`s are `example.com` placeholders (placeholder art shows);
in-memory mode is ephemeral (restart reseeds; use Postgres for persistence);
**no auth** on API routes (`admins` table reserved); phone camera flow not run
in CI (do not claim passes until a real-device test); Render free cold starts
delay first connect (~30–60 s, banners cover it).
