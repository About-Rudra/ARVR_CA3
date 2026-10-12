'use strict';
// Finds a local Chrome/Edge for puppeteer-core (override with CHROME_PATH).
const fs = require('fs');
const CANDIDATES = [
  process.env.CHROME_PATH,
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome', '/usr/bin/chromium-browser', '/usr/bin/chromium',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
].filter(Boolean);

function chromePath() {
  const p = CANDIDATES.find((c) => fs.existsSync(c));
  if (!p) throw new Error('No Chrome/Edge found. Set CHROME_PATH.');
  return p;
}

module.exports = { chromePath };
