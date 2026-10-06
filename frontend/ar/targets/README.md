# WebAR image targets — `frontend/ar/targets/`

Companion to the Unity markers in `Assets/Markers/` (`MARK-001..003` PNGs + `generate_markers.py`).
The WebAR scene (`frontend/ar/index.html`) tracks these markers with
**MindAR image tracking**; Unity keeps using ARCore (see `docs/AR_DEVELOPER.md`).

## Marker ↔ asset mapping (frozen contract)

| # | Marker ID | MindAR `targetIndex` | Asset | Status |
|---|-----------|----------------------|-------|--------|
| 1 | `MARK-001` | `0` | AST-001 · Epson EB-X49 Projector | operational |
| 2 | `MARK-002` | `1` | AST-002 · Creality Ender-3 3D Printer | operational |
| 3 | `MARK-003` | `2` | AST-003 · Tektronix Oscilloscope | under_maintenance |
| 4 | `MARK-004` | `3` | AST-004 · Meta Quest 2 VR Headset | operational |
| 5 | `MARK-005` | `4` | AST-005 · Daikin 1.5T Split AC | faulty |

`targetIndex` order is the order images are passed to the MindAR compiler —
keep it exactly MARK-001…MARK-005 so it matches `TARGET_INDEX_TO_MARKER`
in `frontend/ar/app.js` and the `<a-entity mindar-image-target>` list in
`frontend/ar/index.html`. Print at **15 cm × 15 cm** (same as Unity).

## Source images

- `src/MARK-001.png` … `src/MARK-003.png` — copies of `Assets/Markers/*.png`
  (checked in for convenience; re-copy after regenerating with
  `python3 Assets/Markers/generate_markers.py`).
- `MARK-004` / `MARK-005` source images do **not** exist yet. Create two new
  1024×1024 high-contrast asymmetric PNGs (same design rules as
  `Assets/Markers/README.md`: thick black border, no rotational symmetry,
  human-readable ID banner), drop them in `src/` as `MARK-004.png` /
  `MARK-005.png`, and register them in the backend with the same `markerId`.

## Compiling `targets.mind` (required — NOT checked in)

The binary `targets.mind` is a build artifact and is **not** in git.
Generate it with the official MindAR image-target compiler:

```bash
# 1. Install the compiler (Node 18+)
npm install -g mind-ar   # provides mindar-image-target / compiler CLI

# 2. From the repo root, compile in MARK-001..005 order
npx mindar-image-target compile \
  -i frontend/ar/targets/src/MARK-001.png \
  -i frontend/ar/targets/src/MARK-002.png \
  -i frontend/ar/targets/src/MARK-003.png \
  -i frontend/ar/targets/src/MARK-004.png \
  -i frontend/ar/targets/src/MARK-005.png \
  -o frontend/ar/targets/targets.mind
```

(If your `mind-ar` CLI version uses different flags, follow
`https://github.com/hiukim/mind-ar-js` — the rule that matters is:
input order = MARK-001…MARK-005, one output `targets.mind` next to this README.)

Alternative: use the hosted compiler at the MindAR docs site and save the
result here as `targets.mind`.

## Verifying

1. Confirm `frontend/ar/targets/targets.mind` exists (a few hundred KB).
2. Serve locally over HTTPS or `localhost` (camera needs a secure context):
   `python3 -m http.server` in `frontend/ar/`, backend on `http://localhost:3000`.
3. Open `http://localhost:8000/?marker=MARK-001` (manual, no camera), then scan
   a printed marker with the camera flow.

Without `targets.mind` the page still works in manual-simulation mode
(dropdown / `?marker=`); only live camera tracking is unavailable.
