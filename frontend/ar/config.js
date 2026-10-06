/* AR Passport WebAR config — frontend/ar/config.js
 *
 * Resolution order (never hardcode a production URL here):
 *   1. window.__API_BASE_URL  (injected by the host page / deploy env)
 *   2. localStorage "AR_API_BASE_URL" (manual override, persisted; also set via ?api=)
 *   3. fallback "http://localhost:3000" (local dev only)
 *
 * Loaded as a classic script before app.js. Exposes:
 *   window.AR_CONFIG  -> { API_BASE_URL }
 *   window.apiUrl(path) -> absolute URL string for an /api/... path
 *   window.api(path, options) -> fetch() against the API base with JSON defaults
 */
(function () {
  'use strict';

  var FALLBACK = 'http://localhost:3000';
  var STORAGE_KEY = 'AR_API_BASE_URL';

  function stripTrailingSlashes(s) {
    return String(s || '').replace(/\/+$/, '');
  }

  function readStorage() {
    try {
      if (typeof window !== 'undefined' && window.localStorage) {
        return window.localStorage.getItem(STORAGE_KEY) || '';
      }
    } catch (e) {
      /* storage unavailable (private mode) — ignore */
    }
    return '';
  }

  // Allow ?api=https://backend.example.com deep override; persisted to localStorage.
  // Explicitly a runtime value — nothing is baked in at build time.
  function readQueryOverride() {
    try {
      var q = new URLSearchParams(window.location.search || '');
      var v = (q.get('api') || '').trim();
      if (v) {
        try {
          window.localStorage.setItem(STORAGE_KEY, v);
        } catch (e) {
          /* ignore */
        }
        return v;
      }
    } catch (e) {
      /* ignore */
    }
    return '';
  }

  var fromWindow = (typeof window !== 'undefined' && window.__API_BASE_URL) || '';
  var fromQuery = (typeof window !== 'undefined' && window.location) ? readQueryOverride() : '';
  var fromStorage = readStorage();

  var API_BASE_URL = stripTrailingSlashes(fromWindow || fromQuery || fromStorage || FALLBACK);

  function apiUrl(path) {
    var p = String(path || '');
    if (!p.startsWith('/')) p = '/' + p;
    return API_BASE_URL + p;
  }

  function api(path, options) {
    var opts = Object.assign({ headers: {} }, options || {});
    opts.headers = Object.assign({ Accept: 'application/json' }, opts.headers || {});
    if (opts.body !== undefined && typeof opts.body !== 'string' && !(opts.body instanceof FormData)) {
      opts.body = JSON.stringify(opts.body);
      if (!opts.headers['Content-Type']) opts.headers['Content-Type'] = 'application/json';
    }
    return fetch(apiUrl(path), opts);
  }

  function setBaseUrl(url) {
    var clean = stripTrailingSlashes(String(url || '').trim());
    if (!clean) return API_BASE_URL;
    try {
      window.localStorage.setItem(STORAGE_KEY, clean);
    } catch (e) {
      /* ignore */
    }
    window.AR_CONFIG.API_BASE_URL = clean;
    return clean;
  }

  window.AR_CONFIG = {
    API_BASE_URL: API_BASE_URL,
    STORAGE_KEY: STORAGE_KEY,
    FALLBACK: FALLBACK,
    setBaseUrl: setBaseUrl
  };
  window.apiUrl = apiUrl;
  window.api = api;
})();
