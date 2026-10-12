'use strict';
/**
 * Live MindAR recognition test without a phone: Chrome's fake camera plays a video
 * of each marker image (generated with Pillow by make_marker_video.py), the real
 * A-Frame/MindAR engine tracks it with targets/targets.mind, and we assert the
 * passport for the right asset opens.
 *
 *   python make_marker_video.py        # writes videos/MARK-00x.mjpeg (needs Pillow)
 *   node marker_tracking.js <ar-origin> <api-origin>
 * Not a substitute for a real-device test (lighting, print, motion), but proves the
 * compiled targets + targetIndex -> marker -> API -> passport wiring end to end.
 */
const path = require('path');
const puppeteer = require('puppeteer-core');
const { chromePath } = require('./browser');

const [AR = 'http://localhost:8000', API = 'http://localhost:3000'] = process.argv.slice(2);
const EXPECT = { 'MARK-001': 'Epson', 'MARK-002': 'Creality', 'MARK-003': 'Tektronix', 'MARK-004': 'Meta Quest', 'MARK-005': 'Daikin' };

async function run(marker, want) {
  const video = path.join(__dirname, 'videos', marker + '.mjpeg');
  const b = await puppeteer.launch({
    executablePath: chromePath(), headless: true,
    args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream',
      '--use-file-for-fake-video-capture=' + video, '--enable-unsafe-swiftshader', '--use-angle=swiftshader']
  });
  try {
    const p = await b.newPage();
    await p.setViewport({ width: 390, height: 844, isMobile: true, hasTouch: true });
    await p.goto(`${AR}/?api=${encodeURIComponent(API)}`, { waitUntil: 'networkidle2', timeout: 90000 });
    await p.click('#btn-start');
    const found = await p.waitForFunction((w) => !document.getElementById('passport').hidden &&
      document.getElementById('passport-title').textContent.includes(w), { timeout: 60000 }, want).then(() => true, () => false);
    const title = await p.$eval('#passport-title', (e) => e.textContent.trim());
    console.log(`  [${found ? 'PASS' : 'FAIL'}] camera feed of ${marker} -> passport "${title}"`);
    return found;
  } finally {
    await b.close();
  }
}

(async () => {
  let fails = 0;
  for (const [m, w] of Object.entries(EXPECT)) if (!(await run(m, w))) fails++;
  console.log(fails ? `FAIL (${fails})` : 'PASS');
  process.exitCode = fails ? 1 : 0;
})().catch((e) => { console.error(e); process.exitCode = 2; });
