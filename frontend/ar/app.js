/* AR Passport WebAR app — frontend/ar/app.js
 * Depends on: config.js (window.AR_CONFIG, window.api, window.apiUrl).
 * Plain script (no modules/bundler) so it works from any static host.
 *
 * Flow: MindAR targetFound (or manual simulate / ?marker= deep link)
 *   -> resolveMarker(markerId)  [GET /api/assets/marker/:markerId -> {assetId}]
 *   -> fetchAsset(assetId)       [GET /api/assets/:id]
 *   -> passport bottom-sheet
 *   -> fetchMaintenance(assetId) [GET /api/assets/:id/maintenance] (on demand)
 *   -> reportIssue({...})        [POST /api/issues] (on demand)
 */
(function () {
  'use strict';

  var REQUEST_TIMEOUT_MS = 10000;

  // Must match the compile order of frontend/ar/targets/targets.mind.
  // See targets/README.md — index N in the .mind == MARK-00(N+1).
  var TARGET_INDEX_TO_MARKER = ['MARK-001', 'MARK-002', 'MARK-003', 'MARK-004', 'MARK-005'];
  var KNOWN_MARKERS = TARGET_INDEX_TO_MARKER.slice();

  var STATUS_LABEL = {
    operational: 'Operational',
    under_maintenance: 'Under maintenance',
    faulty: 'Faulty',
    retired: 'Retired'
  };

  var state = {
    started: false,
    scanning: false,
    currentMarker: null,
    currentAsset: null,
    detailsOpen: false
  };

  // ---------- tiny DOM helpers ----------

  function $(id) {
    return document.getElementById(id);
  }

  function show(el) {
    if (el) el.hidden = false;
  }

  function hide(el) {
    if (el) el.hidden = true;
  }

  function toast(msg, kind) {
    var bar = $('status-banner');
    if (!bar) return;
    bar.textContent = msg;
    bar.dataset.kind = kind || 'info';
    show(bar);
    clearTimeout(toast._t);
    if (kind === 'info') {
      toast._t = setTimeout(function () {
        hide(bar);
      }, 6000);
    }
  }

  function setLoading(on, label) {
    var loader = $('loader');
    if (!loader) return;
    if (on) {
      loader.querySelector('.loader-text').textContent = label || 'Loading…';
      show(loader);
    } else {
      hide(loader);
    }
  }

  function statusClass(status) {
    var s = String(status || '').toLowerCase();
    if (s === 'operational') return 'operational';
    if (s === 'under_maintenance') return 'under_maintenance';
    if (s === 'faulty') return 'faulty';
    if (s === 'retired') return 'retired';
    return 'unknown';
  }

  function statusLabel(status) {
    var s = String(status || '').toLowerCase();
    return STATUS_LABEL[s] || (status || 'Unknown');
  }

  function escapeHtml(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  // ---------- fetch core (timeout + error envelope) ----------

  function fetchJson(path, options) {
    var controller = new AbortController();
    var timer = setTimeout(function () {
      controller.abort();
    }, REQUEST_TIMEOUT_MS);
    var opts = Object.assign({}, options || {}, { signal: controller.signal });
    return window
      .api(path, opts)
      .then(function (res) {
        var contentType = res.headers.get('content-type') || '';
        var asJson = contentType.indexOf('application/json') !== -1;
        var bodyPromise = asJson ? res.json().catch(function () {
          return null;
        }) : res.text().catch(function () {
          return '';
        });
        return bodyPromise.then(function (body) {
          if (!res.ok) {
            var code = body && body.error && body.error.code;
            var message = (body && body.error && body.error.message) || 'Request failed (HTTP ' + res.status + ').';
            var err = new Error(message);
            err.code = code || ('HTTP_' + res.status);
            err.status = res.status;
            throw err;
          }
          return body;
        });
      })
      .catch(function (err) {
        if (err && err.name === 'AbortError') {
          var t = new Error('Request timed out — the network or backend is slow. Try again.');
          t.code = 'TIMEOUT';
          throw t;
        }
        if (err instanceof TypeError) {
          // fetch() rejects with TypeError on DNS/CORS/connection-refused.
          var n = new Error('Backend unavailable — could not reach ' + window.AR_CONFIG.API_BASE_URL + '. Check the API URL and network.');
          n.code = 'NETWORK_UNREACHABLE';
          n.cause = err;
          throw n;
        }
        throw err;
      })
      .finally(function () {
        clearTimeout(timer);
      });
  }

  // ---------- API functions (required surface) ----------

  // markerId e.g. "MARK-001" -> Promise<{ assetId }>
  // Throws with code MARKER_NOT_FOUND when the marker is not linked to any asset.
  function resolveMarker(markerId) {
    var id = String(markerId || '').trim().toUpperCase();
    if (!id) return Promise.reject(Object.assign(new Error('Enter a marker id like MARK-001.'), { code: 'VALIDATION_ERROR' }));
    return fetchJson('/api/assets/marker/' + encodeURIComponent(id)).then(function (body) {
      if (!body || !body.assetId) {
        throw Object.assign(new Error('Marker "' + id + '" is not linked to any asset.'), { code: 'MARKER_NOT_FOUND' });
      }
      return body;
    });
  }

  // assetId e.g. "AST-001" -> Promise<asset>
  function fetchAsset(assetId) {
    var id = String(assetId || '').trim();
    if (!id) return Promise.reject(Object.assign(new Error('Missing asset id.'), { code: 'VALIDATION_ERROR' }));
    return fetchJson('/api/assets/' + encodeURIComponent(id)).then(function (asset) {
      if (!asset || !asset.id) {
        throw Object.assign(new Error('Asset "' + id + '" is invalid or missing.'), { code: 'ASSET_NOT_FOUND' });
      }
      return asset;
    });
  }

  // assetId -> Promise<maintenance records[]>
  function fetchMaintenance(assetId) {
    return fetchJson('/api/assets/' + encodeURIComponent(assetId) + '/maintenance').then(function (rows) {
      return Array.isArray(rows) ? rows : [];
    });
  }

  // {assetId,title,description,reportedBy} -> Promise<created issue>
  function reportIssue(draft) {
    var d = draft || {};
    if (!d.assetId) return Promise.reject(Object.assign(new Error('No asset selected.'), { code: 'VALIDATION_ERROR' }));
    if (!d.title || String(d.title).trim().length < 3) {
      return Promise.reject(Object.assign(new Error('Give the issue a short title (min 3 characters).'), { code: 'VALIDATION_ERROR' }));
    }
    return fetchJson('/api/issues', {
      method: 'POST',
      body: {
        assetId: d.assetId,
        title: String(d.title).trim(),
        description: String(d.description || '').trim(),
        reportedBy: String(d.reportedBy || 'webar-user').trim() || 'webar-user'
      }
    });
  }

  // ---------- passport UI ----------

  function renderPassport(asset, markerId) {
    state.currentAsset = asset;
    state.currentMarker = markerId;

    $('passport-title').textContent = asset.name || asset.id;
    $('passport-subtitle').textContent = (asset.id || '') + (markerId ? ' · ' + markerId : '');

    var badge = $('passport-status');
    badge.textContent = statusLabel(asset.status);
    badge.className = 'badge status-' + statusClass(asset.status);

    $('passport-meta').innerHTML =
      metaRow('Category', asset.category) +
      metaRow('Location', asset.location) +
      metaRow('Last maintenance', asset.lastMaintenance || '—') +
      metaRow('Next maintenance', asset.nextMaintenance || '—');

    // Photo with missing/broken-image fallback (never a blank box).
    var img = $('passport-img');
    var imgFallback = $('passport-img-fallback');
    if (asset.imageUrl) {
      img.src = asset.imageUrl;
      img.alt = 'Photo of ' + (asset.name || asset.id);
      show(img);
      hide(imgFallback);
      img.onerror = function () {
        hide(img);
        img.removeAttribute('src');
        show(imgFallback);
      };
    } else {
      hide(img);
      img.removeAttribute('src');
      show(imgFallback);
    }

    // 3D model note: WebAR milestone shows a link/placeholder, no runtime GLB loader.
    var modelNote = $('passport-model-note');
    if (asset.modelUrl) {
      modelNote.innerHTML = '3D model: <a href="' + escapeHtml(asset.modelUrl) + '" target="_blank" rel="noopener">open model</a>';
    } else {
      modelNote.textContent = 'No 3D model attached — see the desktop Dashboard for model viewing.';
    }

    // Collapse details each time a new asset opens.
    state.detailsOpen = false;
    $('passport-desc').textContent = asset.description || 'No description available.';
    hide($('passport-desc'));
    $('btn-details').textContent = 'View Details';
    $('btn-details').setAttribute('aria-expanded', 'false');

    // Reset sub-panels.
    hide($('maintenance-modal'));
    hide($('report-modal'));

    show($('passport'));
    $('btn-close').focus({ preventScroll: true });
  }

  function metaRow(label, value) {
    return (
      '<div class="meta-row"><dt>' +
      escapeHtml(label) +
      '</dt><dd>' +
      escapeHtml(value == null || value === '' ? '—' : value) +
      '</dd></div>'
    );
  }

  function closePassport() {
    hide($('passport'));
    hide($('maintenance-modal'));
    hide($('report-modal'));
    state.currentAsset = null;
    state.currentMarker = null;
    state.detailsOpen = false;
  }

  // ---------- end-to-end marker handling ----------

  function handleMarker(markerId) {
    var id = String(markerId || '').trim().toUpperCase();
    if (!id) {
      toast('Enter a marker id (MARK-001 … MARK-005).', 'warn');
      return Promise.resolve(null);
    }
    setLoading(true, 'Resolving ' + id + '…');
    return resolveMarker(id)
      .then(function (ref) {
        setLoading(true, 'Loading asset ' + ref.assetId + '…');
        return fetchAsset(ref.assetId).then(function (asset) {
          return { asset: asset, markerId: id };
        });
      })
      .then(function (result) {
        setLoading(false);
        renderPassport(result.asset, result.markerId);
        try {
          var url = new URL(window.location.href);
          url.searchParams.set('marker', result.markerId);
          window.history.replaceState(null, '', url.toString());
        } catch (e) {
          /* file:// or restricted context — ignore */
        }
        return result.asset;
      })
      .catch(function (err) {
        setLoading(false);
        if (err && err.code === 'MARKER_NOT_FOUND') {
          toast('Marker "' + id + '" was scanned but is not linked to any asset. Check the backend seed data.', 'warn');
        } else if (err && (err.code === 'NETWORK_UNREACHABLE' || err.code === 'TIMEOUT')) {
          toast(err.message, 'error');
        } else if (err && err.code === 'ASSET_NOT_FOUND') {
          toast('Marker resolved, but the linked asset is invalid or was deleted: ' + err.message, 'error');
        } else {
          toast('Could not load asset: ' + (err && err.message ? err.message : err), 'error');
        }
        return null;
      });
  }

  // ---------- maintenance + report panels ----------

  function openMaintenance() {
    if (!state.currentAsset) return;
    var modal = $('maintenance-modal');
    var list = $('maintenance-list');
    show(modal);
    list.innerHTML = '<li class="muted">Loading maintenance history…</li>';
    fetchMaintenance(state.currentAsset.id)
      .then(function (rows) {
        if (!rows.length) {
          list.innerHTML = '<li class="muted">No maintenance records for this asset.</li>';
          return;
        }
        list.innerHTML = rows
          .map(function (r) {
            return (
              '<li class="maint-row"><strong>' +
              escapeHtml(r.date || '—') +
              ' · ' +
              escapeHtml(r.type || '—') +
              '</strong><span>' +
              escapeHtml(r.technician || '—') +
              '</span><p>' +
              escapeHtml(r.notes || '') +
              '</p></li>'
            );
          })
          .join('');
      })
      .catch(function (err) {
        list.innerHTML = '<li class="error-text">Could not load history: ' + escapeHtml(err.message) + '</li>';
      });
  }

  function openReport() {
    if (!state.currentAsset) return;
    show($('report-modal'));
    $('report-status').textContent = '';
    $('report-title').value = '';
    $('report-desc').value = '';
    if (!$('report-by').value) $('report-by').value = 'webar-user';
    $('report-title').focus();
  }

  function submitReport() {
    if (!state.currentAsset) return;
    var btn = $('report-submit');
    btn.disabled = true;
    $('report-status').textContent = 'Submitting…';
    reportIssue({
      assetId: state.currentAsset.id,
      title: $('report-title').value,
      description: $('report-desc').value,
      reportedBy: $('report-by').value || 'webar-user'
    })
      .then(function (issue) {
        $('report-status').textContent = 'Reported as ' + (issue.id || 'issue') + ' — thank you.';
        toast('Issue ' + (issue.id || '') + ' reported for ' + state.currentAsset.id + '.', 'info');
      })
      .catch(function (err) {
        $('report-status').textContent = 'Failed: ' + err.message;
      })
      .finally(function () {
        btn.disabled = false;
      });
  }

  // ---------- AR scene wiring (MindAR) ----------

  function targetFoundListener(e) {
    var markerId = null;
    if (e && e.target && e.target.dataset && e.target.dataset.marker) {
      markerId = e.target.dataset.marker;
    } else if (typeof e.detail !== 'undefined' && TARGET_INDEX_TO_MARKER[e.detail] ) {
      markerId = TARGET_INDEX_TO_MARKER[e.detail];
    }
    if (markerId) {
      toast('Marker ' + markerId + ' detected.', 'info');
      handleMarker(markerId);
    }
  }

  function bindMindArEvents() {
    // MindAR fires targetFound on each <a-entity mindar-image-target>.
    document.querySelectorAll('[mindar-image-target]').forEach(function (el) {
      el.addEventListener('targetFound', targetFoundListener);
    });
    var scene = document.querySelector('a-scene');
    if (scene) {
      scene.addEventListener('arError', function () {
        // MindAR surfaces camera / sensor failures here.
        showCameraDenied('AR failed to start (camera or sensors unavailable). You can still test manually below — no camera needed.');
      });
    }
  }

  function showCameraDenied(message) {
    var box = $('camera-error');
    box.textContent = message + ' If you denied camera access, allow it in the browser site settings and press Start again.';
    show(box);
    hide($('scan-hint'));
  }

  function startAr() {
    var startOverlay = $('start-overlay');
    var permissionNote = $('permission-note');

    if (!window.isSecureContext && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1') {
      permissionNote.textContent =
        'Note: this page is not on HTTPS, so mobile browsers will block the camera. ' +
        'Deploy over HTTPS (Vercel/Netlify) or use manual simulation below.';
    }

    // Explicit getUserMedia probe so denial is caught with a helpful message
    // before MindAR takes over the camera.
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      showCameraDenied('This browser has no camera API (needs HTTPS + a camera-capable browser).');
      hide(startOverlay);
      show($('manual-bar'));
      return Promise.resolve(false);
    }

    setLoading(true, 'Requesting camera…');
    return navigator.mediaDevices
      .getUserMedia({ video: { facingMode: 'environment' } })
      .then(function (stream) {
        // Hand the camera to MindAR/A-Frame; stop our probe tracks.
        stream.getTracks().forEach(function (t) {
          t.stop();
        });
        return startMindArScene();
      })
      .then(function () {
        setLoading(false);
        state.started = true;
        state.scanning = true;
        hide(startOverlay);
        hide($('camera-error'));
        show($('scan-hint'));
        show($('manual-bar'));
        toast('Point the camera at a printed marker (MARK-001 … MARK-005).', 'info');
        return true;
      })
      .catch(function (err) {
        setLoading(false);
        var name = (err && err.name) || '';
        if (name === 'NotAllowedError' || name === 'SecurityError') {
          showCameraDenied('Camera permission was denied.');
        } else if (name === 'NotFoundError' || name === 'OverconstrainedError') {
          showCameraDenied('No rear camera found on this device.');
        } else {
          showCameraDenied('Camera could not start (' + (err && err.message ? err.message : err) + ').');
        }
        // Manual testing stays available without a camera.
        hide(startOverlay);
        show($('manual-bar'));
        return false;
      });
  }

  function startMindArScene() {
    var scene = document.querySelector('a-scene');
    if (!scene) return Promise.resolve();
    // With autoStart:false the MindAR system exposes start() once A-Frame boots.
    return new Promise(function (resolve) {
      function go() {
        try {
          var sys = scene.systems && scene.systems['mindar-image-system'];
          if (sys && typeof sys.start === 'function') {
            sys.start();
            resolve();
          } else {
            // Older mindar builds auto-start; nothing to do.
            resolve();
          }
        } catch (e) {
          // Missing/uncompiled .mind or WebGL failure: keep manual mode alive.
          toast('AR engine could not start (is targets/targets.mind compiled? see targets/README.md). Manual simulation still works.', 'warn');
          resolve();
        }
      }
      if (scene.hasLoaded) go();
      else scene.addEventListener('loaded', go, { once: true });
      // Safety net: never trap the UI behind the loader.
      setTimeout(resolve, 8000);
    });
  }

  // ---------- manual testing (no camera) + deep link ----------

  function getQueryParam(name) {
    try {
      return new URLSearchParams(window.location.search || '').get(name);
    } catch (e) {
      return null;
    }
  }

  function bindManualControls() {
    var select = $('manual-marker');
    var input = $('manual-marker-input');
    KNOWN_MARKERS.forEach(function (m) {
      var opt = document.createElement('option');
      opt.value = m;
      opt.textContent = m;
      select.appendChild(opt);
    });
    $('manual-simulate').addEventListener('click', function () {
      var typed = (input.value || '').trim().toUpperCase();
      var chosen = typed || select.value;
      handleMarker(chosen);
    });
    input.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') {
        e.preventDefault();
        $('manual-simulate').click();
      }
    });
  }

  function checkHealth() {
    // Non-blocking; only drives the banner text.
    fetchJson('/health')
      .then(function () {
        var bar = $('backend-status');
        bar.textContent = 'Backend: ' + window.AR_CONFIG.API_BASE_URL;
        bar.dataset.kind = 'ok';
      })
      .catch(function () {
        var bar = $('backend-status');
        bar.textContent = 'Backend unreachable at ' + window.AR_CONFIG.API_BASE_URL + ' — manual simulation will fail until it is up. Override below if needed.';
        bar.dataset.kind = 'error';
      });
  }

  function bindApiOverride() {
    var input = $('api-base-input');
    input.value = window.AR_CONFIG.API_BASE_URL;
    $('api-base-save').addEventListener('click', function () {
      var next = window.AR_CONFIG.setBaseUrl(input.value);
      input.value = next;
      toast('API base URL set to ' + next, 'info');
      checkHealth();
    });
  }

  function init() {
    bindMindArEvents();
    bindManualControls();
    bindApiOverride();

    $('btn-start').addEventListener('click', startAr);
    $('btn-manual-only').addEventListener('click', function () {
      hide($('start-overlay'));
      show($('manual-bar'));
      toast('Manual mode — pick a marker below. No camera needed.', 'info');
    });
    $('btn-details').addEventListener('click', function () {
      state.detailsOpen = !state.detailsOpen;
      if (state.detailsOpen) {
        show($('passport-desc'));
        $('btn-details').textContent = 'Hide Details';
        $('btn-details').setAttribute('aria-expanded', 'true');
      } else {
        hide($('passport-desc'));
        $('btn-details').textContent = 'View Details';
        $('btn-details').setAttribute('aria-expanded', 'false');
      }
    });
    $('btn-maintenance').addEventListener('click', openMaintenance);
    $('btn-report').addEventListener('click', openReport);
    $('btn-close').addEventListener('click', closePassport);
    $('maintenance-close').addEventListener('click', function () {
      hide($('maintenance-modal'));
    });
    $('report-close').addEventListener('click', function () {
      hide($('report-modal'));
    });
    $('report-submit').addEventListener('click', submitReport);

    checkHealth();

    // ?marker=MARK-001 deep link: usable with or without camera.
    var deep = (getQueryParam('marker') || '').trim().toUpperCase();
    if (deep) {
      hide($('start-overlay'));
      show($('manual-bar'));
      var sel = $('manual-marker');
      if (KNOWN_MARKERS.indexOf(deep) !== -1) sel.value = deep;
      else $('manual-marker-input').value = deep;
      handleMarker(deep);
    }
  }

  // Public surface (used by the page + console testing).
  window.ARPassport = {
    resolveMarker: resolveMarker,
    fetchAsset: fetchAsset,
    fetchMaintenance: fetchMaintenance,
    reportIssue: reportIssue,
    handleMarker: handleMarker,
    KNOWN_MARKERS: KNOWN_MARKERS,
    TARGET_INDEX_TO_MARKER: TARGET_INDEX_TO_MARKER
  };
  // Convenience aliases matching the task's function names.
  window.resolveMarker = resolveMarker;
  window.fetchAsset = fetchAsset;
  window.fetchMaintenance = fetchMaintenance;
  window.reportIssue = reportIssue;

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
