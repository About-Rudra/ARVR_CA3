'use strict';
/* AR Passport — Web Dashboard config.
 * Resolves the backend API base URL with this precedence:
 *   1. window.__API_BASE_URL (inline override for hosted deploys)
 *   2. localStorage key "AR_API_BASE_URL" (persisted via the header setting)
 *   3. "http://localhost:3000" (local dev default only — never a prod URL)
 * No production URL is hardcoded here; set it via 1 or 2 above.
 */
(function (global) {
  var STORAGE_KEY = 'AR_API_BASE_URL';
  var DEV_DEFAULT = 'http://localhost:3000';

  function normalize(url) {
    if (!url || typeof url !== 'string') return '';
    return url.trim().replace(/\/+$/, '');
  }

  function getApiBaseUrl() {
    var fromWindow = normalize(global.__API_BASE_URL);
    if (fromWindow) return fromWindow;
    try {
      var stored = normalize(global.localStorage && global.localStorage.getItem(STORAGE_KEY));
      if (stored) return stored;
    } catch (e) { /* storage unavailable (private mode) — fall through */ }
    return DEV_DEFAULT;
  }

  function setApiBaseUrl(url) {
    var clean = normalize(url);
    if (!clean) throw new Error('API base URL must not be empty.');
    if (!/^https?:\/\//i.test(clean)) throw new Error('API base URL must start with http:// or https://.');
    try {
      if (global.localStorage) global.localStorage.setItem(STORAGE_KEY, clean);
    } catch (e) { /* non-fatal: keep in-memory only */ }
    global.__API_BASE_URL = clean;
    return clean;
  }

  global.ARPassportConfig = {
    storageKey: STORAGE_KEY,
    devDefault: DEV_DEFAULT,
    getApiBaseUrl: getApiBaseUrl,
    setApiBaseUrl: setApiBaseUrl
  };
})(typeof window !== 'undefined' ? window : this);
