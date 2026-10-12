# Printable AR Markers

Five placeholder image targets for the AR Digital Passport scanner:

| File | Marker ID | Motif | Linked seed asset (`database/seed.sql`) |
|------|-----------|-------|-------------------------------------------|
| `MARK-001.png` | `MARK-001` | Concentric squares + wedge | AST-001 · Epson EB-X49 Projector (operational) |
| `MARK-002.png` | `MARK-002` | Triangle + dot row | AST-002 · Creality Ender-3 3D Printer (operational) |
| `MARK-003.png` | `MARK-003` | Cross + corner blocks | AST-003 · Tektronix Oscilloscope (under_maintenance) |
| `MARK-004.png` | `MARK-004` | Diagonal stripes + notched ring + L-bracket | AST-004 · Meta Quest 2 VR Headset (operational) |
| `MARK-005.png` | `MARK-005` | Irregular block grid + half-disc + bar code | AST-005 · Daikin 1.5T Split AC (faulty) |

MARK-004/005 are used by the WebAR scanner (`frontend/ar/targets/targets.mind`).
The legacy Unity reference image library still lists only MARK-001..003; add
004/005 there (step 4 of the Unity section) if the APK path needs them.

Copies in `Assets/StreamingAssets/markers/` are identical and are bundled into
the Android APK (StreamingAssets are packed read-only into the `.apk`).

## Printing

- **Size: 15 cm × 15 cm** (must match `ARBootstrap.markerPrintWidthMeters = 0.15` or pose scale will be wrong).
- Matte paper, rigid backing (no curl), flat even lighting, no lamination glare.
- The reference-image name registered in Unity **must equal the Marker ID**
  (`MARK-001` …) — that string is what `ARMarkerTracker` sends to
  `GET /api/assets/marker/:markerId`.

## Design rules applied

1024×1024 px, thick black border, asymmetric high-contrast motifs, large
human-readable ID banner, no rotational symmetry, no repetitive texture.

## Regenerating

```bash
python3 Assets/Markers/generate_markers.py
```

requires Pillow (`pip install pillow`). The script writes PNGs here and copies
them to `Assets/StreamingAssets/markers/`.

## Production markers

Replace these placeholders with real photographed targets (equipment plates,
QR-style codes). For each new marker:

1. Add the image to the `XRReferenceImageLibrary` (see `docs/AR_DEVELOPER.md`),
   keeping physical size = 0.15 m (or update `markerPrintWidthMeters`).
2. Register the asset with the same `markerId` in the backend.
3. Test with `AR Passport > Simulate MARK-00x` in Play mode first.
