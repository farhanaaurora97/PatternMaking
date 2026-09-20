// dashboard.js — PatternPro dashboard: stats, category tabs, table search/sort/CRUD

(function initDashboard() {

  function animateCount(el, target, suffix) {
    if (!el) return;
    let n = 0;
    const step = Math.max(1, Math.ceil(target / 45));
    const tick = () => {
      n = Math.min(n + step, target);
      el.textContent = n + (suffix || '');
      if (n < target) requestAnimationFrame(tick);
    };
    tick();
  }

  function applyTrafficLight() {
    document.querySelectorAll('.prog-fill[data-target]').forEach((el) => {
      const pct = parseInt(el.dataset.target || '0', 10);
      el.style.width = Math.min(100, Math.max(0, pct)) + '%';
      el.classList.remove('prog-fill--tl-low', 'prog-fill--tl-mid', 'prog-fill--tl-high');
      if (pct < 30) el.classList.add('prog-fill--tl-low');
      else if (pct <= 70) el.classList.add('prog-fill--tl-mid');
      else el.classList.add('prog-fill--tl-high');
    });
  }

  // ── Animate stats on load ───────────────────────────────────────────
  setTimeout(() => {
    const statActive = document.getElementById('stat-active');
    const tActive = parseInt(statActive?.dataset.target || '0', 10);
    if (statActive) animateCount(statActive, tActive, '');

    const statPieces = document.getElementById('stat-pieces');
    const tPieces = parseInt(statPieces?.dataset.target || '0', 10);
    if (statPieces) animateCount(statPieces, tPieces, '');

    const statCompletion = document.getElementById('stat-completion');
    const tComp = parseInt(statCompletion?.dataset.target || '0', 10);
    if (statCompletion) {
      let n = 0;
      const step = Math.max(1, Math.ceil(tComp / 45));
      const tick = () => {
        n = Math.min(n + step, tComp);
        statCompletion.innerHTML = n + '<span class="stat-unit">%</span>';
        if (n < tComp) requestAnimationFrame(tick);
      };
      tick();
    }

    const statFactory = document.getElementById('stat-factory-ready');
    const tFactory = parseInt(statFactory?.dataset.target || '0', 10);
    if (statFactory) animateCount(statFactory, tFactory, '');

    const barActive = document.getElementById('bar-active');
    if (barActive) {
      const pct = parseInt(barActive.dataset.target || '0', 10);
      barActive.style.width = Math.min(100, Math.max(0, pct)) + '%';
    }

    const barCompletion = document.getElementById('bar-completion');
    if (barCompletion) {
      const pct = parseInt(barCompletion.dataset.target || '0', 10);
      barCompletion.style.width = Math.min(100, Math.max(0, pct)) + '%';
    }

    const barFactory = document.getElementById('bar-factory-ready');
    if (barFactory) {
      const pct = parseInt(barFactory.dataset.target || '0', 10);
      barFactory.style.width = Math.min(100, Math.max(0, pct)) + '%';
    }

    applyTrafficLight();
  }, 300);

  // ── Welcome toast ───────────────────────────────────────────────────
  setTimeout(() => toast('Welcome back', 'PatternPro — bottom wear workspace ready', 'success', '👋'), 800);

  const searchInput = document.getElementById('tbl-search-input');
  const tblCount = document.getElementById('tbl-count');
  const btnClearStatus = document.getElementById('btn-clear-status-filter');
  const btnClearPant = document.getElementById('btn-clear-pant-filter');
  const btnClearPlm = document.getElementById('btn-clear-plm-filter');
  const pageSizeSelect = document.getElementById('tbl-page-size');
  const btnPagePrev = document.getElementById('tbl-page-prev');
  const btnPageNext = document.getElementById('tbl-page-next');
  const pageInfo = document.getElementById('tbl-page-info');
  let currentPage = 1;
  /** @type {number|'all'} */
  let pageSize = 10;

  function readPageSize() {
    const v = pageSizeSelect?.value || '10';
    pageSize = v === 'all' ? 'all' : Math.max(10, parseInt(v, 10) || 10);
  }

  function effectivePageSize(totalMatched) {
    return pageSize === 'all' ? Math.max(totalMatched, 1) : pageSize;
  }

  readPageSize();

  function syncPantTileActive() {
    document.querySelectorAll('#pant-board-grid .pant-tile').forEach((t) => {
      t.classList.toggle('pant-tile--active', currentCat !== 'All' && t.dataset.category === currentCat);
    });
    if (btnClearPant) btnClearPant.hidden = currentCat === 'All';
  }

  // ── Table search ────────────────────────────────────────────────────
  searchInput?.addEventListener('input', debounce(async () => {
    await refreshTable(searchInput.value, currentSort.col, currentSort.asc);
  }, 250));

  // ── Category tabs ─────────────────────────────────────────────────────
  let currentCat = 'All';
  document.getElementById('cat-tabs')?.addEventListener('click', (e) => {
    const btn = e.target.closest('[data-cat]');
    if (!btn) return;
    currentCat = btn.dataset.cat || 'All';
    document.querySelectorAll('#cat-tabs .cat-tab').forEach((t) => t.classList.toggle('active', t === btn));
    syncPantTileActive();
    applyFilters(true);
  });

  document.getElementById('pant-board-grid')?.addEventListener('click', (e) => {
    const tile = e.target.closest('.pant-tile[data-category]');
    if (!tile) return;
    const cat = tile.dataset.category || 'All';
    currentCat = currentCat === cat ? 'All' : cat;
    document.querySelectorAll('#cat-tabs .cat-tab').forEach((t) => {
      t.classList.toggle('active', (t.dataset.cat || 'All') === currentCat);
    });
    syncPantTileActive();
    applyFilters(true);
    document.querySelector('.tbl-wrap')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });

  btnClearPant?.addEventListener('click', () => {
    currentCat = 'All';
    document.querySelectorAll('#cat-tabs .cat-tab').forEach((t) => {
      t.classList.toggle('active', (t.dataset.cat || 'All') === 'All');
    });
    syncPantTileActive();
    applyFilters(true);
  });

  /** @type {string|null} status key: Pending, Draft, InProgress, … */
  let currentStatusFilter = null;
  /** @type {string|null} overdue | due-week | bulk-ready */
  let currentPlmFilter = null;

  function syncPlmAlertActive() {
    document.querySelectorAll('.plm-alerts [data-plm-filter]').forEach((el) => {
      el.classList.toggle('plm-alert--active', el.dataset.plmFilter === currentPlmFilter);
    });
    if (btnClearPlm) btnClearPlm.hidden = !currentPlmFilter;
  }

  document.querySelector('.plm-alerts')?.addEventListener('click', (e) => {
    if (e.target.closest('a')) return;
    const btn = e.target.closest('[data-plm-filter]');
    if (!btn) return;
    const f = btn.dataset.plmFilter || null;
    currentPlmFilter = currentPlmFilter === f ? null : f;
    syncPlmAlertActive();
    applyFilters(true);
    document.querySelector('.tbl-wrap')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });

  btnClearPlm?.addEventListener('click', () => {
    currentPlmFilter = null;
    syncPlmAlertActive();
    applyFilters(true);
  });

  window.applyDashboardStatusFilter = function (statusKey) {
    currentStatusFilter = statusKey || null;
    if (btnClearStatus) btnClearStatus.hidden = !currentStatusFilter;
    applyFilters(true);
  };

  btnClearStatus?.addEventListener('click', () => {
    window.applyDashboardStatusFilter(null);
  });

  pageSizeSelect?.addEventListener('change', () => {
    readPageSize();
    applyFilters(true);
  });

  btnPagePrev?.addEventListener('click', () => {
    if (currentPage > 1) {
      currentPage -= 1;
      applyFilters(false);
    }
  });

  btnPageNext?.addEventListener('click', () => {
    currentPage += 1;
    applyFilters(false);
  });

  function rowMatchesFilter(row) {
    const c = row.dataset.category || '';
    const st = row.dataset.status || '';
    const showCat = currentCat === 'All' || c === currentCat;
    const showSt = !currentStatusFilter || st === currentStatusFilter;
    let showPlm = true;
    if (currentPlmFilter === 'overdue') showPlm = row.dataset.overdue === '1';
    else if (currentPlmFilter === 'due-week') showPlm = row.dataset.dueWeek === '1';
    else if (currentPlmFilter === 'bulk-ready') showPlm = row.dataset.bulkNudge === '1';
    return showCat && showSt && showPlm;
  }

  function applyFilters(resetPage) {
    if (resetPage) currentPage = 1;
    const rows = [...document.querySelectorAll('#patterns-tbody tr')];
    const matched = rows.filter(rowMatchesFilter);
    const totalMatched = matched.length;
    const size = effectivePageSize(totalMatched);
    const totalPages = pageSize === 'all' ? 1 : Math.max(1, Math.ceil(totalMatched / size));
    if (currentPage > totalPages) currentPage = totalPages;
    const startIdx = (currentPage - 1) * size;
    const endIdx = startIdx + size;
    const visibleSet = new Set(matched.slice(startIdx, endIdx));
    rows.forEach((row) => {
      row.style.display = visibleSet.has(row) ? '' : 'none';
    });
    updatePaginationUI(totalMatched, startIdx, endIdx, totalPages);
  }

  function updatePaginationUI(totalMatched, startIdx, endIdx, totalPages) {
    const total = parseInt(tblCount?.dataset.total || '0', 10);
    if (tblCount) {
      if (totalMatched === 0) {
        tblCount.textContent = `0 of ${total}`;
      } else {
        const from = startIdx + 1;
        const to = Math.min(endIdx, totalMatched);
        tblCount.textContent = totalMatched === total
          ? `${from}–${to} of ${total}`
          : `${from}–${to} of ${totalMatched} (${total} total)`;
      }
    }
    const showAll = pageSize === 'all';
    if (btnPagePrev) btnPagePrev.disabled = showAll || currentPage <= 1 || totalMatched === 0;
    if (btnPageNext) btnPageNext.disabled = showAll || currentPage >= totalPages || totalMatched === 0;
    if (pageInfo) {
      if (totalMatched === 0) pageInfo.textContent = 'No rows';
      else if (showAll) pageInfo.textContent = `All ${totalMatched} rows`;
      else pageInfo.textContent = `Page ${currentPage} of ${totalPages}`;
    }
  }

  // ── Table sort ──────────────────────────────────────────────────────
  const currentSort = { col: '', asc: true };
  document.querySelectorAll('th[data-sort]').forEach((th) => {
    th.addEventListener('click', async () => {
      const col = th.dataset.sort;
      if (currentSort.col === col) currentSort.asc = !currentSort.asc;
      else { currentSort.col = col; currentSort.asc = true; }

      document.querySelectorAll('th[data-sort]').forEach((t) => {
        t.classList.remove('sorted');
        const icon = t.querySelector('.sort-icon');
        if (icon) icon.textContent = '↕';
      });
      th.classList.add('sorted');
      const icon = th.querySelector('.sort-icon');
      if (icon) icon.textContent = currentSort.asc ? '↑' : '↓';

      await refreshTable(searchInput?.value || '', currentSort.col, currentSort.asc);
    });
  });

  function buildStatusOptions(current) {
    const opts = [
      ['Pending', 'Pending'],
      ['Draft', 'Draft'],
      ['InProgress', 'In Progress'],
      ['Graded', 'Graded'],
      ['Done', 'Done'],
    ];
    return opts.map(([val, lab]) =>
      `<option value="${val}"${current === val ? ' selected' : ''}>${lab}</option>`).join('');
  }

  // ── Table actions (status, delete, due date) ─────────────────────
  document.getElementById('patterns-tbody')?.addEventListener('change', async (e) => {
    const sel = e.target;

    // Due date input
    if (sel.matches?.('.due-date-input')) {
      const id = parseInt(sel.dataset.id, 10);
      const dateVal = sel.value || null;
      const cell = sel.closest('td');
      const res = await fetch('/Home/SetDueDate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ id, date: dateVal }),
      });
      if (res.ok) {
        const p = await res.json();
        if (cell) {
          cell.classList.toggle('due-cell--empty', !p.dueDateIso);
        }
        sel.value = p.dueDateIso || '';
        const label = p.dueDateLabel && p.dueDateLabel !== '—' ? p.dueDateLabel : 'cleared';
        toast('Due date updated', `${p.displayName} — due ${label}`, 'success', '📅');
      } else {
        toast('Update failed', 'Could not set due date', 'error', '⚠️');
      }
      return;
    }

    if (!sel.matches?.('[data-action="set-status"]')) return;
    const id = parseInt(sel.dataset.id, 10);
    const status = sel.value;
    const prev = sel.dataset.prevStatus || '';
    const res = await fetch('/Home/SetStatus', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ id, status }),
    });
    if (res.ok) {
      const p = await res.json();
      const row = document.getElementById(`row-${id}`);
      if (row) {
        row.dataset.status = p.status;
        sel.className = `status-select st-${p.status}`;
        sel.value = p.status;
        sel.dataset.prevStatus = p.status;
        const tds = row.querySelectorAll('td');
        const dateEl = tds[9];
        if (dateEl) dateEl.textContent = p.date;
      }
      toast('Status updated', `${p.displayName} is now ${p.statusLabel}`, 'success', '🔄');
      window.refreshDashboardCharts?.();
    } else {
      sel.value = prev;
      toast('Update failed', 'Could not change status', 'error', '⚠️');
    }
  });

  document.getElementById('patterns-tbody')?.addEventListener('focusin', (e) => {
    const sel = e.target;
    if (sel.matches?.('[data-action="set-status"]')) sel.dataset.prevStatus = sel.value;
  });

  let selectedPatternId = null;

  document.getElementById('patterns-tbody')?.addEventListener('click', async (e) => {
    // Row selection — clicking anywhere on a row that isn't a button/link/select
    if (!e.target.closest('[data-action]') && !e.target.closest('a,select')) {
      const row = e.target.closest('tr[id^="row-"]');
      if (row) {
        document.querySelectorAll('#patterns-tbody tr.row-selected').forEach((r) => r.classList.remove('row-selected'));
        row.classList.add('row-selected');
        selectedPatternId = parseInt(row.id.replace('row-', ''), 10);
      }
    }

    const el = e.target.closest('[data-action]');
    if (!el || el.matches?.('[data-action="set-status"]')) return;
    const action = el.dataset.action;
    const id = parseInt(el.dataset.id, 10);

    if (action === 'duplicate-pattern') {
      const res = await fetch(`/Home/Duplicate/${id}`, { method: 'POST' });
      if (res.ok) {
        const p = await res.json();
        window.dispatchEvent(new Event('pattern:created'));
        toast('Duplicated', `"${p.displayName}" added as Draft`, 'success', '⊕');
        window.refreshDashboardCharts?.();
      } else {
        toast('Error', 'Could not duplicate pattern', 'error', '⚠️');
      }
    }

    if (action === 'delete-pattern') {
      const row = document.getElementById(`row-${id}`);
      row?.classList.add('row-removing');
      setTimeout(async () => {
        const res = await fetch(`/Home/Delete/${id}`, { method: 'DELETE', headers: { 'RequestVerificationToken': getToken() } });
        if (res.ok) {
          row?.remove();
          if (selectedPatternId === id) selectedPatternId = null;
          toast('Deleted', 'Pattern removed', 'error', '🗑️');
          if (tblCount) {
            const t = Math.max(0, parseInt(tblCount.dataset.total || '0', 10) - 1);
            tblCount.dataset.total = String(t);
            applyFilters(false);
          }
          window.refreshDashboardCharts?.();
        }
      }, 350);
    }
  });

  window.addEventListener('pattern:created', async () => {
    currentCat = 'All';
    window.applyDashboardStatusFilter?.(null);
    document.querySelectorAll('#cat-tabs .cat-tab').forEach((t) => t.classList.toggle('active', t.dataset.cat === 'All'));
    await refreshTable('', currentSort.col, currentSort.asc);
  });

  document.getElementById('btn-duplicate')?.addEventListener('click', async () => {
    if (!selectedPatternId) {
      toast('No pattern selected', 'Click a row in the table to select a pattern first', 'info', 'ℹ️');
      return;
    }
    const res = await fetch(`/Home/Duplicate/${selectedPatternId}`, { method: 'POST' });
    if (res.ok) {
      const p = await res.json();
      window.dispatchEvent(new Event('pattern:created'));
      toast('Duplicated', `"${p.displayName}" added as Draft`, 'success', '⊕');
      window.refreshDashboardCharts?.();
    } else {
      toast('Error', 'Could not duplicate pattern', 'error', '⚠️');
    }
  });

  document.getElementById('btn-add-table')?.addEventListener('click', () => {
    document.getElementById('btn-new-pattern')?.click();
  });

  document.getElementById('qa-new')?.addEventListener('click', () => {
    document.getElementById('btn-new-pattern')?.click();
  });

  document.addEventListener('keydown', (e) => {
    if (e.target.matches('input,textarea,select') || e.target.isContentEditable) return;
    if (e.key === 'f' || e.key === 'F') {
      e.preventDefault();
      searchInput?.focus();
    }
    if (e.key === 'n' || e.key === 'N') {
      e.preventDefault();
      document.getElementById('btn-new-pattern')?.click();
    }
    if (e.key === 'g' || e.key === 'G') {
      e.preventDefault();
      window.location.href = '/Grading';
    }
    if (e.key === 'e' || e.key === 'E') {
      e.preventDefault();
      window.location.href = '/Export';
    }
  });

  async function refreshTable(q, sort, asc) {
    const params = new URLSearchParams();
    if (q) params.set('q', q);
    if (sort) { params.set('sort', sort); params.set('asc', asc); }
    const res = await fetch(`/Home/Patterns?${params}`);
    if (!res.ok) return;
    const patterns = await res.json();
    renderTableRows(patterns);
  }

  function renderTableRows(patterns) {
    const tbody = document.getElementById('patterns-tbody');
    if (!tbody) return;
    selectedPatternId = null;
    if (tblCount) tblCount.dataset.total = String(patterns.length);
    tbody.innerHTML = patterns.map((p) => `
      <tr id="row-${p.id}" data-category="${escapeAttr(p.category || 'Denim')}" data-status="${escapeAttr(p.status)}" data-lifecycle="${escapeAttr(p.lifecycleStatus)}" data-overdue="${p.isOverdue ? '1' : '0'}" data-due-week="${p.isDueThisWeek ? '1' : '0'}" data-bulk-nudge="${p.needsBulkLifecycle ? '1' : '0'}" class="${p.isOverdue ? 'row-overdue' : ''}">
        <td class="td-mono td-bold">${escapeHtml(p.code)}</td>
        <td class="td-bold">${escapeHtml(p.name)}${productionBadgeHtml(p)}</td>
        <td class="td-mono">${escapeHtml(p.season || '')}</td>
        <td><span class="tag ${escapeAttr(p.lifecycleCssClass)}">${escapeHtml(p.lifecycleLabel || '')}</span></td>
        <td class="td-mono">${escapeHtml(p.style)}</td>
        <td class="td-mono">${escapeHtml(p.baseSize)}</td>
        <td class="td-mono">${p.pieceCount}</td>
        <td>
          <select class="status-select st-${escapeAttr(p.status)}" data-action="set-status" data-id="${p.id}" data-prev-status="${escapeAttr(p.status)}" aria-label="Pattern status">
            ${buildStatusOptions(p.status)}
          </select>
        </td>
        <td class="td-mono due-cell ${!p.dueDateIso ? 'due-cell--empty' : ''}">
          <input type="date" class="due-date-input" data-id="${p.id}"
                 value="${escapeAttr(p.dueDateIso || '')}" title="Click to set due date"
                 aria-label="Due date for ${escapeAttr(p.code)}" />
        </td>
        <td class="td-mono" style="color:#b0a898">${escapeHtml(p.date)}</td>
        <td>
          <div class="action-btns">
            <a class="btn-open" href="/Pieces?patternId=${p.id}&style=${encodeURIComponent(p.styleKey || 'skinny')}">Pieces</a>
            <a class="btn-canvas-row" href="/Canvas?patternId=${p.id}&style=${encodeURIComponent(p.styleKey || 'skinny')}" title="Canvas editor">✏</a>
            <a class="btn-dl" href="/Export?patternId=${p.id}&style=${encodeURIComponent(p.styleKey || 'skinny')}" title="Export">Export</a>
            <button type="button" class="btn-dup-row" data-action="duplicate-pattern" data-id="${p.id}" title="Duplicate pattern">⊕</button>
            <button type="button" class="btn-del" data-action="delete-pattern" data-id="${p.id}">×</button>
          </div>
        </td>
      </tr>`).join('');
    applyFilters(true);
  }

  applyFilters(true);

  document.getElementById('dash-analytics')?.addEventListener('toggle', (e) => {
    const panel = e.target;
    if (panel?.open) {
      window.refreshDashboardCharts?.();
      applyTrafficLight();
    }
  });

  function productionBadgeHtml(p) {
    const label = p.productionBadgeLabel || '';
    if (!label) return '';
    const css = p.productionBadgeCss || 'tag-gold';
    return ` <span class="tag ${escapeAttr(css)}" style="margin-left:6px;font-size:10px;vertical-align:middle" title="Production certification">${escapeHtml(label)}</span>`;
  }

  function escapeHtml(s) {
    if (!s) return '';
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
  }
  function escapeAttr(s) {
    if (!s) return '';
    return String(s).replace(/"/g, '&quot;');
  }

  function getToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
  }

  function debounce(fn, ms) {
    let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
  }

})();
