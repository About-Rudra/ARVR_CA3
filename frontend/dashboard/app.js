'use strict';
/* AR Passport — Web Dashboard app (vanilla JS, no framework).
 * Talks to the same Express backend as Unity (see docs/API.md).
 * Works against the real backend; falls back to an embedded sample
 * dataset (mirroring Assets/Resources/seed_assets.json) only when the
 * backend is unreachable, with a banner + offline footer state.
 */
(function () {
  var PAGE_SIZE = 10;
  var ASSET_STATUSES = ['operational', 'under_maintenance', 'faulty', 'retired'];
  var ISSUE_STATUSES = ['open', 'in_progress', 'resolved', 'rejected', 'closed'];
  var ID_PATTERN = /^AST-[0-9]{3,}$/;

  /* Offline sample (used ONLY when the backend is down).
   * Mirrors Backend/seedData.js + database/seed.sql exactly (5 assets
   * AST-001..005 <-> MARK-001..005, 3 locations, 6 maintenance rows,
   * 3 issues) so offline and online views show the same demo data. */
  var SAMPLE = {
    locations: [
      { id: 'CL-6', name: 'Computer Lab 6', floor: 2, building: 'C-Block' },
      { id: 'FAB-LAB', name: 'Fabrication Lab', floor: 1, building: 'Workshop Block' },
      { id: 'AR-LAB', name: 'AR/VR Lab', floor: 2, building: 'C-Block' }
    ],
    assets: [
      { id: 'AST-001', name: 'Epson EB-X49 Projector', category: 'AV Equipment', location: 'CL-6', status: 'operational', description: '3LCD XGA projector, 3600 lumens. Ceiling-mounted in Computer Lab 6, used for lectures and demos.', imageUrl: 'https://example.com/images/ast-001.jpg', modelUrl: '', lastMaintenance: '2026-07-15', nextMaintenance: '2026-10-15', markerId: 'MARK-001' },
      { id: 'AST-002', name: 'Creality Ender-3 3D Printer', category: 'Fabrication', location: 'FAB-LAB', status: 'operational', description: 'FDM 3D printer, 220x220x250mm build volume. Shared fabrication-lab asset.', imageUrl: 'https://example.com/images/ast-002.jpg', modelUrl: '', lastMaintenance: '2026-08-20', nextMaintenance: '2026-11-20', markerId: 'MARK-002' },
      { id: 'AST-003', name: 'Tektronix TBS1052B Oscilloscope', category: 'Lab Equipment', location: 'FAB-LAB', status: 'under_maintenance', description: '50 MHz 2-channel digital storage oscilloscope on electronics bench. Currently in calibration.', imageUrl: 'https://example.com/images/ast-003.jpg', modelUrl: '', lastMaintenance: '2026-06-01', nextMaintenance: '2026-10-10', markerId: 'MARK-003' },
      { id: 'AST-004', name: 'Meta Quest 2 VR Headset', category: 'AR/VR', location: 'AR-LAB', status: 'operational', description: '128GB standalone VR headset, demo unit for AR/VR lab coursework.', imageUrl: 'https://example.com/images/ast-004.jpg', modelUrl: '', lastMaintenance: '2026-09-01', nextMaintenance: '2026-12-01', markerId: 'MARK-004' },
      { id: 'AST-005', name: 'Daikin 1.5T Split Air Conditioner', category: 'HVAC', location: 'CL-6', status: 'faulty', description: '1.5 ton 5-star split AC serving Computer Lab 6. Reported weak cooling, suspected gas leak.', imageUrl: 'https://example.com/images/ast-005.jpg', modelUrl: '', lastMaintenance: '2026-05-12', nextMaintenance: '2026-08-12', markerId: 'MARK-005' }
    ],
    maintenance: [
      { id: 'MNT-001', assetId: 'AST-001', date: '2026-07-15', type: 'preventive', technician: 'R. Kumar', notes: 'Filter cleaned, lamp hours checked (812h). Focus recalibrated.' },
      { id: 'MNT-002', assetId: 'AST-001', date: '2026-04-10', type: 'corrective', technician: 'R. Kumar', notes: 'Replaced HDMI cable, fixed intermittent signal drop.' },
      { id: 'MNT-003', assetId: 'AST-002', date: '2026-08-20', type: 'preventive', technician: 'S. Iyer', notes: 'Bed levelled, nozzle replaced, belts tensioned.' },
      { id: 'MNT-004', assetId: 'AST-003', date: '2026-06-01', type: 'calibration', technician: 'P. Nair', notes: 'Annual calibration started. Channel 2 probe reads 5% high; sent for probe replacement.' },
      { id: 'MNT-005', assetId: 'AST-004', date: '2026-09-01', type: 'preventive', technician: 'A. Sharma', notes: 'Factory reset, straps sanitised, controllers paired, firmware v62.' },
      { id: 'MNT-006', assetId: 'AST-005', date: '2026-05-12', type: 'preventive', technician: 'K. Singh', notes: 'Filters washed, condenser coil cleaned. Cooling weak; flagged for gas-pressure check.' }
    ],
    issues: [
      { id: 'ISS-001', assetId: 'AST-003', title: 'Channel 2 reads high', description: 'Channel 2 shows ~5% higher voltage than calibrated source.', status: 'in_progress', reportedAt: '2026-09-18T10:30:00Z', reportedBy: 'lab-assistant' },
      { id: 'ISS-002', assetId: 'AST-005', title: 'Weak cooling', description: 'Room not cooling below 27C even at 18C setpoint. Suspected refrigerant leak.', status: 'open', reportedAt: '2026-09-28T14:00:00Z', reportedBy: 'faculty-cl6' },
      { id: 'ISS-003', assetId: 'AST-002', title: 'Filament jam', description: 'PLA jammed in extruder during overnight print. Cleared, test print OK.', status: 'resolved', reportedAt: '2026-08-25T09:15:00Z', reportedBy: 'student-fablab' }
    ]
  };

  var state = {
    online: true,
    assets: [],
    issues: [],
    locations: [],
    search: '',
    statusFilter: '',
    categoryFilter: '',
    locationFilter: '',
    issueStatusFilter: '',
    page: 0,
    editingId: null,   // null = create mode
    deleteId: null,
    detailId: null,
    lastFocused: null
  };

  function $(id) { return document.getElementById(id); }

  function esc(value) {
    return String(value == null ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function apiBase() {
    return window.ARPassportConfig.getApiBaseUrl();
  }

  /* ---------- fetch wrappers ---------- */

  function friendlyError(status, body, fallback) {
    if (body && body.error && body.error.message) return String(body.error.message);
    if (status === 0) return 'Could not reach the backend. Check the API base URL and that the server is running.';
    if (status === 404) return 'The requested item was not found.';
    if (status === 409) return (body && body.error && body.error.message) || 'Conflict: duplicate id or marker.';
    if (status >= 400 && status < 500) return (body && body.error && body.error.message) || 'The request was rejected. Check the form values.';
    return fallback || 'Something went wrong. Please try again.';
  }

  function apiFetch(path, options) {
    var url = apiBase() + path;
    var opts = options || {};
    opts.headers = Object.assign({ 'Content-Type': 'application/json' }, opts.headers || {});
    return fetch(url, opts).then(function (res) {
      if (res.status === 204) return null;
      return res.json().catch(function () { return null; }).then(function (body) {
        if (!res.ok) {
          var err = new Error(friendlyError(res.status, body, null));
          err.status = res.status;
          throw err;
        }
        return body;
      });
    }).catch(function (e) {
      if (e && e.status) throw e; // already friendly (backend envelope parsed)
      throw new Error('Could not reach the backend. Check the API base URL and that the server is running.');
    });
  }

  var api = {
    listAssets: function () { return apiFetch('/api/assets'); },
    listIssues: function () { return apiFetch('/api/issues'); },
    listLocations: function () { return apiFetch('/api/locations'); },
    getMaintenance: function (id) { return apiFetch('/api/assets/' + encodeURIComponent(id) + '/maintenance'); },
    issuesForAsset: function (id) { return apiFetch('/api/issues?assetId=' + encodeURIComponent(id)); },
    createAsset: function (body) { return apiFetch('/api/assets', { method: 'POST', body: JSON.stringify(body) }); },
    updateAsset: function (id, body) { return apiFetch('/api/assets/' + encodeURIComponent(id), { method: 'PATCH', body: JSON.stringify(body) }); },
    deleteAsset: function (id) { return apiFetch('/api/assets/' + encodeURIComponent(id), { method: 'DELETE' }); },
    updateIssueStatus: function (id, status) {
      return apiFetch('/api/issues/' + encodeURIComponent(id) + '/status', { method: 'PATCH', body: JSON.stringify({ status: status }) });
    }
  };

  /* ---------- toasts (friendly messages only, never stacks) ---------- */

  function toast(message) {
    var box = $('toasts');
    var div = document.createElement('div');
    div.className = 'toast';
    div.textContent = String(message);
    box.appendChild(div);
    while (box.children.length > 4) box.removeChild(box.firstChild);
    setTimeout(function () { if (div.parentNode) div.parentNode.removeChild(div); }, 4500);
  }

  /* ---------- modals + focus management ---------- */

  function openModal(id) {
    state.lastFocused = document.activeElement;
    var backdrop = $(id);
    backdrop.hidden = false;
    var focusable = backdrop.querySelector('input, select, textarea, button:not([data-close])');
    var initial = backdrop.querySelector('input, select, textarea') || focusable;
    if (initial) initial.focus();
  }

  function closeModal(id) {
    $(id).hidden = true;
    if (state.lastFocused && document.contains(state.lastFocused)) state.lastFocused.focus();
  }

  function wireModalCloses() {
    Array.prototype.forEach.call(document.querySelectorAll('[data-close]'), function (btn) {
      btn.addEventListener('click', function () { closeModal(btn.getAttribute('data-close')); });
    });
    Array.prototype.forEach.call(document.querySelectorAll('.modal-backdrop'), function (backdrop) {
      backdrop.addEventListener('click', function (e) {
        if (e.target === backdrop) closeModal(backdrop.id);
      });
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') {
        ['assetDetailModal', 'assetFormModal', 'deleteConfirmModal'].forEach(function (id) {
          if (!$(id).hidden) closeModal(id);
        });
      }
    });
  }

  /* ---------- labels ---------- */

  function assetStatusLabel(s) {
    switch (s) {
      case 'operational': return 'Operational';
      case 'under_maintenance': return 'Under maintenance';
      case 'faulty': return 'Faulty';
      case 'retired': return 'Retired';
      default: return s || '—';
    }
  }

  function issueStatusLabel(s) {
    switch (s) {
      case 'open': return 'Open';
      case 'in_progress': return 'In progress';
      case 'resolved': return 'Resolved';
      case 'closed': return 'Closed';
      case 'rejected': return 'Rejected';
      default: return s || '—';
    }
  }

  /* ---------- data loading ---------- */

  function setConn(online) {
    state.online = online;
    var el = $('connStatus');
    el.textContent = online ? 'Online' : 'Offline (sample data)';
    el.classList.toggle('online', online);
    el.classList.toggle('offline', !online);
    $('offlineBanner').hidden = online;
    $('addAssetBtn').disabled = !online;
  }

  function useSampleData() {
    state.assets = SAMPLE.assets.slice();
    state.issues = SAMPLE.issues.slice();
    state.locations = SAMPLE.locations.slice();
    setConn(false);
    afterDataLoaded();
  }

  function loadAll() {
    $('assetsLoading').hidden = false;
    $('assetsError').hidden = true;
    $('issuesLoading').hidden = false;
    $('issuesError').hidden = true;
    return Promise.all([
      api.listAssets(),
      api.listIssues().catch(function () { return []; }),
      api.listLocations().catch(function () { return []; })
    ]).then(function (parts) {
      state.assets = Array.isArray(parts[0]) ? parts[0] : [];
      state.issues = Array.isArray(parts[1]) ? parts[1] : [];
      state.locations = Array.isArray(parts[2]) ? parts[2] : [];
      setConn(true);
      $('assetsLoading').hidden = true;
      $('issuesLoading').hidden = true;
      afterDataLoaded();
    }).catch(function (e) {
      $('assetsLoading').hidden = true;
      $('issuesLoading').hidden = true;
      // Reads degrade to sample data (banner on); writes stay disabled.
      useSampleData();
      toast(e && e.message ? e.message : 'Could not reach the backend. Showing sample data.');
    });
  }

  function afterDataLoaded() {
    buildFilterOptions();
    state.page = 0;
    renderStats();
    renderTable();
    renderIssues();
  }

  function uniqueSorted(values) {
    var seen = {};
    var out = [];
    values.forEach(function (v) {
      if (v == null || v === '') return;
      var k = String(v);
      if (!seen[k]) { seen[k] = true; out.push(k); }
    });
    out.sort(function (a, b) { return a.toLowerCase().localeCompare(b.toLowerCase()); });
    return out;
  }

  function buildFilterOptions() {
    var cats = uniqueSorted(state.assets.map(function (a) { return a.category; }));
    var catSel = $('filterCategory');
    var keepCat = state.categoryFilter;
    catSel.innerHTML = '<option value="">All categories</option>' +
      cats.map(function (c) { return '<option value="' + esc(c) + '">' + esc(c) + '</option>'; }).join('');
    catSel.value = keepCat;
    if (catSel.value !== keepCat) state.categoryFilter = '';

    var locIds = state.locations.length
      ? state.locations.map(function (l) { return l.id; })
      : state.assets.map(function (a) { return a.location; });
    var locSel = $('filterLocation');
    var keepLoc = state.locationFilter;
    locSel.innerHTML = '<option value="">All locations</option>' +
      uniqueSorted(locIds).map(function (l) { return '<option value="' + esc(l) + '">' + esc(l) + '</option>'; }).join('');
    locSel.value = keepLoc;
    if (locSel.value !== keepLoc) state.locationFilter = '';

    // Form selects
    var fLoc = $('fLocation');
    fLoc.innerHTML = uniqueSorted(locIds).map(function (l) {
      return '<option value="' + esc(l) + '">' + esc(l) + '</option>';
    }).join('');
    $('categorySuggestions').innerHTML = cats.map(function (c) {
      return '<option value="' + esc(c) + '"></option>';
    }).join('');
  }

  /* ---------- stats ---------- */

  function renderStats() {
    var total = state.assets.length;
    var op = state.assets.filter(function (a) { return a.status === 'operational'; }).length;
    var maint = state.assets.filter(function (a) {
      return a.status === 'under_maintenance' || a.status === 'faulty';
    }).length;
    var openIssues = state.issues.filter(function (i) { return i.status === 'open'; }).length;
    $('statTotal').textContent = String(total);
    $('statOperational').textContent = String(op);
    $('statMaintenance').textContent = String(maint);
    $('statIssuesOpen').textContent = String(openIssues);
  }

  /* ---------- assets table ---------- */

  function filteredAssets() {
    var q = state.search.trim().toLowerCase();
    return state.assets.filter(function (a) {
      if (state.statusFilter && a.status !== state.statusFilter) return false;
      if (state.categoryFilter && a.category !== state.categoryFilter) return false;
      if (state.locationFilter && a.location !== state.locationFilter) return false;
      if (q) {
        var hay = [a.id, a.name, a.category, a.location].join(' ').toLowerCase();
        if (hay.indexOf(q) === -1) return false;
      }
      return true;
    });
  }

  function renderTable() {
    var rows = filteredAssets();
    var pages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
    if (state.page >= pages) state.page = pages - 1;
    var slice = rows.slice(state.page * PAGE_SIZE, state.page * PAGE_SIZE + PAGE_SIZE);

    $('assetsEmpty').hidden = rows.length !== 0;
    $('assetTable').style.display = rows.length ? '' : 'none';
    $('assetRows').innerHTML = slice.map(function (a) {
      return '<tr>' +
        '<td><code>' + esc(a.id) + '</code></td>' +
        '<td>' + esc(a.name) + '</td>' +
        '<td>' + esc(a.category || '—') + '</td>' +
        '<td>' + esc(a.location || '—') + '</td>' +
        '<td><span class="badge badge-' + esc(a.status) + '">' + esc(assetStatusLabel(a.status)) + '</span></td>' +
        '<td>' + esc(a.nextMaintenance || '—') + '</td>' +
        '<td><div class="row-actions">' +
          '<button type="button" class="btn btn-small" data-act="view" data-id="' + esc(a.id) + '">View</button>' +
          '<button type="button" class="btn btn-small" data-act="edit" data-id="' + esc(a.id) + '">Edit</button>' +
          '<button type="button" class="btn btn-small" data-act="del" data-id="' + esc(a.id) + '">Delete</button>' +
        '</div></td></tr>';
    }).join('');
    $('pageInfo').textContent = 'Page ' + (state.page + 1) + ' of ' + pages + ' (' + rows.length + ' assets)';
    $('prevPageBtn').disabled = state.page === 0;
    $('nextPageBtn').disabled = state.page + 1 >= pages;
  }

  function onTableClick(e) {
    var btn = e.target.closest('button[data-act]');
    if (!btn) return;
    var id = btn.getAttribute('data-id');
    var act = btn.getAttribute('data-act');
    if (act === 'view') openDetail(id);
    else if (act === 'edit') openForm(id);
    else if (act === 'del') askDelete(id);
  }

  /* ---------- detail modal ---------- */

  function maintenanceFor(id) {
    if (!state.online) {
      return Promise.resolve(SAMPLE.maintenance.filter(function (m) { return m.assetId === id; }));
    }
    return api.getMaintenance(id).catch(function () { return []; });
  }

  function issuesFor(id) {
    var local = state.issues.filter(function (i) { return i.assetId === id; });
    if (!state.online || local.length) return Promise.resolve(local);
    return api.issuesForAsset(id).catch(function () { return local; });
  }

  function openDetail(id) {
    state.detailId = id;
    var a = state.assets.find(function (x) { return x.id === id; });
    if (!a) { toast('Asset "' + id + '" was not found.'); return; }
    $('detailTitle').textContent = a.id + ' — ' + a.name;
    $('detailBody').innerHTML = '<div class="state" role="status">Loading details…</div>';
    openModal('assetDetailModal');
    $('detailEditBtn').disabled = !state.online;
    $('detailDeleteBtn').disabled = !state.online;

    Promise.all([maintenanceFor(id), issuesFor(id)]).then(function (parts) {
      if (state.detailId !== id || $('assetDetailModal').hidden) return;
      var maint = parts[0] || [];
      var issues = parts[1] || [];
      $('detailBody').innerHTML =
        '<dl class="kv">' +
        '<dt>Name</dt><dd>' + esc(a.name) + '</dd>' +
        '<dt>Category</dt><dd>' + esc(a.category || '—') + '</dd>' +
        '<dt>Location</dt><dd>' + esc(a.location || '—') + '</dd>' +
        '<dt>Status</dt><dd><span class="badge badge-' + esc(a.status) + '">' + esc(assetStatusLabel(a.status)) + '</span></dd>' +
        '<dt>Marker ID</dt><dd><code>' + esc(a.markerId || '—') + '</code></dd>' +
        '<dt>Last maintenance</dt><dd>' + esc(a.lastMaintenance || '—') + '</dd>' +
        '<dt>Next maintenance</dt><dd>' + esc(a.nextMaintenance || '—') + '</dd>' +
        '<dt>Description</dt><dd>' + esc(a.description || '—') + '</dd>' +
        '</dl>' +
        '<h3 class="subhead">Maintenance history (' + maint.length + ')</h3>' +
        (maint.length
          ? '<ul class="mini-list">' + maint.map(function (m) {
              return '<li>' + esc(m.date || '') + ' · ' + esc(m.type || '') + ' · ' + esc(m.technician || '') +
                (m.notes ? ' — ' + esc(m.notes) : '') + '</li>';
            }).join('') + '</ul>'
          : '<p class="hint">No maintenance records.</p>') +
        '<h3 class="subhead">Issues (' + issues.length + ')</h3>' +
        (issues.length
          ? '<ul class="mini-list">' + issues.map(function (i) {
              return '<li><strong>' + esc(i.title || i.id) + '</strong> ' +
                '<span class="badge badge-' + esc(i.status) + '">' + esc(issueStatusLabel(i.status)) + '</span></li>';
            }).join('') + '</ul>'
          : '<p class="hint">No issues reported for this asset.</p>');
    });
  }

  /* ---------- form modal (create/edit) ---------- */

  function openForm(id) {
    if (!state.online) { toast('Backend is offline — editing is disabled until the API is back.'); return; }
    state.editingId = id || null;
    var form = $('assetForm');
    form.reset();
    $('formErrors').hidden = true;
    $('formErrors').textContent = '';
    if (id) {
      var a = state.assets.find(function (x) { return x.id === id; });
      if (!a) { toast('Asset "' + id + '" was not found.'); return; }
      $('formTitle').textContent = 'Edit ' + a.id;
      $('fId').value = a.id;
      $('fId').disabled = true; // id immutable on edit
      $('fName').value = a.name || '';
      $('fCategory').value = a.category || '';
      if (a.location) $('fLocation').value = a.location;
      $('fStatus').value = ASSET_STATUSES.indexOf(a.status) !== -1 ? a.status : 'operational';
      $('fMarkerId').value = a.markerId || '';
      $('fLast').value = a.lastMaintenance || '';
      $('fNext').value = a.nextMaintenance || '';
      $('fDesc').value = a.description || '';
      $('fImage').value = a.imageUrl || '';
      $('fModel').value = a.modelUrl || '';
    } else {
      $('formTitle').textContent = 'Add asset';
      $('fId').disabled = false;
    }
    openModal('assetFormModal');
  }

  function validateForm(data, isCreate) {
    var errors = [];
    if (isCreate) {
      if (!data.id) errors.push('Asset ID is required (e.g. AST-007).');
      else if (!ID_PATTERN.test(data.id)) errors.push('Asset ID must match AST-xxx (e.g. AST-007).');
    }
    if (!data.name) errors.push('Name is required.');
    if (!data.category) errors.push('Category is required.');
    if (!data.location) errors.push('Location is required.');
    if (ASSET_STATUSES.indexOf(data.status) === -1) errors.push('Status must be one of: ' + ASSET_STATUSES.join(', ') + '.');
    if (data.lastMaintenance && data.nextMaintenance && data.nextMaintenance < data.lastMaintenance) {
      errors.push('Next maintenance must not be earlier than last maintenance.');
    }
    return errors;
  }

  function readForm() {
    function v(id) { return $(id).value.trim(); }
    var data = {
      name: v('fName'),
      category: v('fCategory'),
      location: $('fLocation').value,
      status: $('fStatus').value,
      description: v('fDesc'),
      imageUrl: v('fImage'),
      modelUrl: v('fModel'),
      lastMaintenance: v('fLast'),
      nextMaintenance: v('fNext'),
      markerId: v('fMarkerId')
    };
    if (!state.editingId) data.id = v('fId').toUpperCase();
    Object.keys(data).forEach(function (k) { if (data[k] === '') delete data[k]; });
    return data;
  }

  function submitForm(e) {
    e.preventDefault();
    if (!state.online) { toast('Backend is offline — saving is disabled until the API is back.'); return; }
    var isCreate = !state.editingId;
    var data = readForm();
    var problems = validateForm(
      Object.assign({ id: isCreate ? data.id : 'AST-000' }, data), isCreate
    );
    if (problems.length) {
      $('formErrors').textContent = problems.join(' ');
      $('formErrors').hidden = false;
      $('formErrors').focus && $('formErrors').focus();
      return;
    }
    var done = function () { closeModal('assetFormModal'); loadAll(); toast(isCreate ? 'Asset created.' : 'Asset updated.'); };
    var failed = function (err) {
      $('formErrors').textContent = err && err.message ? err.message : 'Could not save the asset.';
      $('formErrors').hidden = false;
    };
    if (isCreate) api.createAsset(data).then(done, failed);
    else api.updateAsset(state.editingId, data).then(done, failed);
  }

  /* ---------- delete ---------- */

  function askDelete(id) {
    if (!state.online) { toast('Backend is offline — deleting is disabled until the API is back.'); return; }
    state.deleteId = id;
    $('deleteConfirmText').textContent = 'Delete asset "' + id + '"? This cannot be undone.';
    openModal('deleteConfirmModal');
  }

  function confirmDelete() {
    if (!state.deleteId || !state.online) return;
    api.deleteAsset(state.deleteId).then(function () {
      closeModal('deleteConfirmModal');
      if (!$('assetDetailModal').hidden) closeModal('assetDetailModal');
      state.deleteId = null;
      loadAll();
      toast('Asset deleted.');
    }).catch(function (err) {
      toast(err && err.message ? err.message : 'Could not delete the asset.');
    });
  }

  /* ---------- issues ---------- */

  function renderIssues() {
    var list = state.issues.filter(function (i) {
      return !state.issueStatusFilter || i.status === state.issueStatusFilter;
    });
    $('issuesEmpty').hidden = list.length !== 0;
    $('issueList').innerHTML = list.map(function (i) {
      var opts = ISSUE_STATUSES.map(function (s) {
        return '<option value="' + s + '"' + (i.status === s ? ' selected' : '') + '>' + issueStatusLabel(s) + '</option>';
      }).join('');
      return '<li class="issue" data-issue="' + esc(i.id) + '">' +
        '<div><h3>' + esc(i.title || i.id) + ' <span class="badge badge-' + esc(i.status) + '">' + esc(issueStatusLabel(i.status)) + '</span></h3>' +
        '<p><code>' + esc(i.id) + '</code> · asset <code>' + esc(i.assetId || '—') + '</code> · reported ' + esc(i.reportedAt || '—') + '</p>' +
        (i.description ? '<p>' + esc(i.description) + '</p>' : '') + '</div>' +
        '<div class="issue-side"><label class="visually-hidden" for="st-' + esc(i.id) + '">Status for ' + esc(i.id) + '</label>' +
        '<select id="st-' + esc(i.id) + '" data-status-for="' + esc(i.id) + '"' + (state.online ? '' : ' disabled') + '>' + opts + '</select></div>' +
        '</li>';
    }).join('');
  }

  function onIssueChange(e) {
    var sel = e.target.closest('select[data-status-for]');
    if (!sel) return;
    var id = sel.getAttribute('data-status-for');
    var next = sel.value;
    if (!state.online) { toast('Backend is offline — issue updates are disabled until the API is back.'); renderIssues(); return; }
    api.updateIssueStatus(id, next).then(function (updated) {
      var idx = -1;
      state.issues.forEach(function (x, n) { if (x.id === id) idx = n; });
      if (idx !== -1) state.issues[idx] = updated;
      renderStats();
      renderIssues();
      toast('Issue ' + id + ' → ' + issueStatusLabel(next) + '.');
    }).catch(function (err) {
      toast(err && err.message ? err.message : 'Could not update the issue.');
      renderIssues();
    });
  }

  /* ---------- tabs ---------- */

  function selectTab(which) {
    var assetsActive = which === 'assets';
    $('tabAssets').classList.toggle('is-active', assetsActive);
    $('tabIssues').classList.toggle('is-active', !assetsActive);
    $('tabAssets').setAttribute('aria-selected', assetsActive ? 'true' : 'false');
    $('tabIssues').setAttribute('aria-selected', assetsActive ? 'false' : 'true');
    $('panelAssets').hidden = !assetsActive;
    $('panelIssues').hidden = assetsActive;
  }

  /* ---------- wiring ---------- */

  function wire() {
    $('apiUrlInput').value = apiBase();
    $('apiUrlForm').addEventListener('submit', function (e) {
      e.preventDefault();
      try {
        window.ARPassportConfig.setApiBaseUrl($('apiUrlInput').value);
        toast('API base URL saved.');
        loadAll();
      } catch (err) {
        toast(err && err.message ? err.message : 'That URL looks invalid.');
      }
    });

    $('searchInput').addEventListener('input', function (e) {
      state.search = e.target.value; state.page = 0; renderTable();
    });
    $('filterStatus').addEventListener('change', function (e) {
      state.statusFilter = e.target.value; state.page = 0; renderTable();
    });
    $('filterCategory').addEventListener('change', function (e) {
      state.categoryFilter = e.target.value; state.page = 0; renderTable();
    });
    $('filterLocation').addEventListener('change', function (e) {
      state.locationFilter = e.target.value; state.page = 0; renderTable();
    });
    $('issueStatusFilter').addEventListener('change', function (e) {
      state.issueStatusFilter = e.target.value; renderIssues();
    });

    $('refreshBtn').addEventListener('click', loadAll);
    $('retryBtn').addEventListener('click', loadAll);
    $('assetsRetryBtn').addEventListener('click', loadAll);
    $('issuesRetryBtn').addEventListener('click', loadAll);
    $('addAssetBtn').addEventListener('click', function () { openForm(null); });
    $('assetRows').addEventListener('click', onTableClick);
    $('issueList').addEventListener('change', onIssueChange);
    $('prevPageBtn').addEventListener('click', function () {
      if (state.page > 0) { state.page -= 1; renderTable(); }
    });
    $('nextPageBtn').addEventListener('click', function () {
      state.page += 1; renderTable();
    });

    $('tabAssets').addEventListener('click', function () { selectTab('assets'); });
    $('tabIssues').addEventListener('click', function () { selectTab('issues'); });

    $('assetForm').addEventListener('submit', submitForm);
    $('deleteConfirmBtn').addEventListener('click', confirmDelete);
    $('detailEditBtn').addEventListener('click', function () {
      if (state.detailId) { closeModal('assetDetailModal'); openForm(state.detailId); }
    });
    $('detailDeleteBtn').addEventListener('click', function () {
      if (state.detailId) { closeModal('assetDetailModal'); askDelete(state.detailId); }
    });

    wireModalCloses();
  }

  wire();
  loadAll();
})();
