// Offline MindAR image-target compiler (mind-ar has no CLI; this wraps its OfflineCompiler).
// Usage (from this folder):  npm install && npm run compile
// Input order = targetIndex order: MARK-001..MARK-005 -> 0..4 (must match frontend/ar/app.js
// TARGET_INDEX_TO_MARKER and the <a-entity mindar-image-target> list in frontend/ar/index.html).
import { OfflineCompiler } from 'mind-ar/src/image-target/offline-compiler.js';
// canvas comes in via mind-ar. Do not add another canvas version to package.json:
// images from a second copy fail mind-ar's "Image or Canvas expected" check.
import { loadImage } from 'canvas';
import { writeFileSync, existsSync } from 'fs';
import { fileURLToPath } from 'url';
import path from 'path';

const here = path.dirname(fileURLToPath(import.meta.url));
const targetsDir = path.resolve(here, '..', '..', 'frontend', 'ar', 'targets');
const markers = ['MARK-001', 'MARK-002', 'MARK-003', 'MARK-004', 'MARK-005'];
const inputs = markers.map((m) => path.join(targetsDir, 'src', m + '.png'));
const out = path.join(targetsDir, 'targets.mind');

const missing = inputs.filter((p) => !existsSync(p));
if (missing.length) {
  console.error('Missing marker images:\n  ' + missing.join('\n  ') + '\nRun: python Assets/Markers/generate_markers.py');
  process.exit(1);
}

const images = [];
for (const p of inputs) images.push(await loadImage(p));
const compiler = new OfflineCompiler();
let last = -10;
await compiler.compileImageTargets(images, (p) => {
  if (p - last >= 20) { console.log(`compiling ${p.toFixed(0)}%`); last = p; }
});
const buf = compiler.exportData();
writeFileSync(out, buf);
console.log(`wrote ${path.relative(path.resolve(here, '..', '..'), out)} (${buf.byteLength} bytes, ${markers.length} targets: ${markers.join(', ')})`);
