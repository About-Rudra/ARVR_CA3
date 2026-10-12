// Headless browser walk of the demo flow: AR page (?marker=) -> report issue -> dashboard -> status change.
'use strict';
/**
 * Headless walk of the demo flow (TEST_PLAN D-K + L/M spot checks):
 * AR ?marker=MARK-001 -> passport -> maintenance -> report issue -> invalid marker,
 * then dashboard -> summary -> issue visible -> status in_progress persists -> rejected persists -> 360px layout.
 *
 *   node ui_flow.js <ar-origin> <dashboard-origin> <api-origin>
 *   e.g. node ui_flow.js http://localhost:8000 http://localhost:8001 http://localhost:3000
 * Side effect: creates one issue on AST-001 (left as rejected).
 */
const puppeteer = require('puppeteer-core');
const { chromePath } = require('./browser');
const [AR, DASH, API] = process.argv.slice(2);
const ok = (n, c, d) => { console.log(`  [${c ? 'PASS' : 'FAIL'}] ${n}${!c && d ? ' — ' + d : ''}`); if (!c) process.exitCode = 1; };
(async () => {
  const browser = await puppeteer.launch({ executablePath: chromePath(), headless: true,
    args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'] });
  const errs = [];
  const page = await browser.newPage();
  page.on('pageerror', (e) => errs.push('AR pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errs.push('AR console: ' + m.text()); });
  await page.setViewport({ width: 390, height: 844, isMobile: true, hasTouch: true });
  await page.goto(`${AR}/?marker=MARK-001&api=${encodeURIComponent(API)}`, { waitUntil: 'networkidle2', timeout: 60000 });
  await page.waitForFunction(() => /Epson/.test(document.getElementById('passport-title').textContent), { timeout: 20000 }).catch(() => {});
  const title = await page.$eval('#passport-title', (e) => e.textContent.trim());
  ok('AR ?marker=MARK-001 shows Epson passport', /Epson/.test(title), title);
  const meta = await page.$eval('#passport', (e) => e.innerText);
  ok('passport shows AST-001, CL-6, status', /AST-001/.test(meta) && /CL-6/.test(meta) && /operational/i.test(meta), meta.slice(0, 200));
  const envOk = await page.evaluate(() => typeof window.__API_BASE_URL !== 'undefined');
  ok('env.js loaded before config.js', envOk);
  const mind = await page.evaluate(async () => (await fetch('./targets/targets.mind')).status);
  ok('targets/targets.mind served', mind === 200, 'status ' + mind);

  await page.click('#btn-maintenance');
  await page.waitForFunction(() => /MNT-00|preventive|corrective/i.test(document.getElementById('maintenance-list').innerText), { timeout: 10000 }).catch(() => {});
  ok('Maintenance history lists records', /preventive|corrective/i.test(await page.$eval('#maintenance-list', (e) => e.innerText)));
  await page.click('#maintenance-close');

  const issueTitle = 'UI lamp flicker ' + Date.now();
  await page.click('#btn-report');
  await page.type('#report-title', issueTitle);
  await page.type('#report-desc', 'Raised by headless UI test');
  await page.click('#report-submit');
  await page.waitForFunction(() => /Reported as ISS-/.test(document.getElementById('report-status').textContent), { timeout: 10000 }).catch(() => {});
  const rep = await page.$eval('#report-status', (e) => e.textContent);
  ok('Report Issue -> stored (ISS id shown)', /Reported as ISS-/.test(rep), rep);
  const issueId = (rep.match(/ISS-\d+/) || [])[0];

  // invalid marker
  await page.goto(`${AR}/?marker=MARK-999`, { waitUntil: 'networkidle2' });
  await new Promise((r) => setTimeout(r, 2500));
  const body = await page.evaluate(() => document.body.innerText);
  ok('invalid marker -> friendly message, no crash', /not linked|not recogni|unknown|no asset/i.test(body), body.slice(0, 300));

  // dashboard
  const d = await browser.newPage();
  d.on('pageerror', (e) => errs.push('DASH pageerror: ' + e.message));
  await d.evaluateOnNewDocument((api) => localStorage.setItem('AR_API_BASE_URL', api), API);
  await d.setViewport({ width: 1280, height: 900 });
  await d.goto(DASH + '/', { waitUntil: 'networkidle2' });
  await d.waitForFunction(() => document.getElementById('statTotal').textContent.trim() !== '' && /\d/.test(document.getElementById('statTotal').textContent), { timeout: 15000 }).catch(() => {});
  const total = await d.$eval('#statTotal', (e) => e.textContent.trim());
  ok('Dashboard summary: total assets >= 5', Number(total) >= 5, total);
  ok('Dashboard online (no offline banner)', await d.$eval('#offlineBanner', (e) => e.hidden));
  await d.click('#tabIssues');
  await d.waitForFunction((t) => document.getElementById('issueList').innerText.includes(t), { timeout: 10000 }, issueTitle).catch(() => {});
  ok('Dashboard Issues tab shows the AR-reported issue', (await d.$eval('#issueList', (e) => e.innerText)).includes(issueTitle));
  await d.select(`#st-${issueId}`, 'in_progress');
  await new Promise((r) => setTimeout(r, 2000));
  await d.reload({ waitUntil: 'networkidle2' });
  await d.click('#tabIssues');
  await d.waitForSelector(`#st-${issueId}`, { timeout: 10000 }).catch(() => {});
  const after = await d.$eval(`#st-${issueId}`, (e) => e.value).catch(() => 'missing');
  ok('Status -> in_progress persists after reload', after === 'in_progress', after);
  await d.select(`#st-${issueId}`, 'rejected');
  await new Promise((r) => setTimeout(r, 2000));
  const viaApi = await (await fetch(`${API}/api/issues?status=rejected`)).json();
  ok('Status -> rejected persists in backend', viaApi.some((i) => i.id === issueId));
  // mobile layout
  await d.setViewport({ width: 360, height: 800, isMobile: true });
  const overflow = await d.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  ok('Dashboard at 360px: no horizontal page scroll', overflow <= 1, `overflow ${overflow}px`);

  const real = errs.filter((e) => !/getUserMedia|camera|favicon|404/i.test(e));
  ok('no JS errors on either page', real.length === 0, real.join(' | '));
  await browser.close();
})().catch((e) => { console.error('UI test crashed', e); process.exitCode = 2; });
