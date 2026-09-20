// style-sheet.js — PLM style register (season, owner, lifecycle)

(function initStyleSheet() {
  const searchInput = document.getElementById('ss-search');
  const tblCount = document.getElementById('ss-count');
  const btnClear = document.getElementById('ss-clear-lifecycle');
  const pageSizeSelect = document.getElementById('ss-page-size');
  const btnPagePrev = document.getElementById('ss-page-prev');
  const btnPageNext = document.getElementById('ss-page-next');
  const pageInfo = document.getElementById('ss-page-info');
  let currentLifecycle = 'All';
  let currentPage = 1;
  /** @type {number|'all'} */
  let pageSize = 10;
  const sort = { col: '', asc: true };

  function readPageSize() {
    const v = pageSizeSelect?.value || '10';
    pageSize = v === 'all' ? 'all' : Math.max(10, parseInt(v, 10) || 10);
  }

  function effectivePageSize(totalMatched) {
    return pageSize === 'all' ? Math.max(totalMatched, 1) : pageSize;
  }

  readPageSize();

  document.getElementById('ss-btn-add')?.addEventListener('click', () => {
    document.getElementById('btn-new-pattern')?.click();
  });

  document.getElementById('ss-lifecycle-tabs')?.addEventListener('click', (e) => {
    const btn = e.target.closest('[data-lifecycle]');
    if (!btn) return;
    currentLifecycle = btn.dataset.lifecycle || 'All';
    document.querySelectorAll('#ss-lifecycle-tabs .cat-tab').forEach((t) => t.classList.toggle('active', t === btn));
    if (btnClear) btnClear.hidden = currentLifecycle === 'All';
    applyFilters(true);
  });

  btnClear?.addEventListener('click', () => {
    currentLifecycle = 'All';
    if (btnClear) btnClear.hidden = true;
    document.querySelectorAll('#ss-lifecycle-tabs .cat-tab').forEach((t) => {
      t.classList.toggle('active', t.dataset.lifecycle === 'All');
    });
    applyFilters(true);
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

  searchInput?.addEventListener('input', debounce(async () => {
    await refreshRows();
  }, 250));

  document.querySelectorAll('th[data-sort]').forEach((th) => {
    th.addEventListener('click', async () => {
      const col = th.dataset.sort;
      if (sort.col === col) sort.asc = !sort.asc;
      else { sort.col = col; sort.asc = true; }
      await refreshRows();
    });
  });

  const tbody = document.getElementById('ss-tbody');
  tbody?.addEventListener('change', async (e) => {
    const el = e.target;
    if (el.matches?.('[data-action="set-lifecycle"]')) {
      const id = parseInt(el.dataset.id, 10);
      const prev = el.dataset.prevLifecycle || '';
      const res = await fetch('/Home/SetLifecycle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ id, lifecycleStatus: el.value }),
      });
      if (res.ok) {
        const p = await res.json();
        const row = document.getElementById(`ss-row-${id}`);
        if (row) {
          row.dataset.lifecycle = p.lifecycleStatus;
          el.className = `lifecycle-select ${p.lifecycleCssClass}`;
          el.dataset.prevLifecycle = p.lifecycleStatus;
        }
        toast('Lifecycle updated', `${p.code} → ${p.lifecycleLabel}`, 'success', '📋');
      } else {
        el.value = prev;
        toast('Update failed', 'Invalid lifecycle', 'error', '⚠️');
      }
      return;
    }

    if (el.matches?.('.season-input, .owner-input, .designer-input')) {
      const id = parseInt(el.dataset.id, 10);
      const row = el.closest('tr');
      const season = row?.querySelector('.season-input')?.value ?? null;
      const owner = row?.querySelector('.owner-input')?.value ?? null;
      const designer = row?.querySelector('.designer-input')?.value ?? null;
      const res = await fetch('/Home/UpdateStyleSheet', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ id, season, owner, designer }),
      });
      if (res.ok) {
        const p = await res.json();
        toast('Style sheet saved', `${p.code} updated`, 'success', '✓');
      } else {
        toast('Save failed', 'Could not update style row', 'error', '⚠️');
      }
    }
  });

  tbody?.addEventListener('focusin', (e) => {
    const sel = e.target;
    if (sel.matches?.('[data-action="set-lifecycle"]')) sel.dataset.prevLifecycle = sel.value;
  });

  async function refreshRows() {
    const params = new URLSearchParams();
    const q = searchInput?.value?.trim();
    if (q) params.set('q', q);
    if (sort.col) { params.set('sort', sort.col); params.set('asc', sort.asc); }
    const res = await fetch(`/StyleSheet/Rows?${params}`);
    if (!res.ok) return;
    const rows = await res.json();
    renderRows(rows);
  }

  function renderRows(patterns) {
    if (!tbody) return;
    if (tblCount) tblCount.dataset.total = String(patterns.length);
    const lifecycleOpts = [
      ['Idea', 'Idea'],
      ['Sampling', 'Sampling'],
      ['Bulk', 'Bulk'],
      ['Cancelled', 'Cancelled'],
    ];
    tbody.innerHTML = patterns.map((p) => `
      <tr id="ss-row-${p.id}" data-lifecycle="${esc(p.lifecycleStatus)}">
        <td class="td-mono td-bold">${esc(p.code)}</td>
        <td>${esc(p.name)}</td>
        <td><input type="text" class="ss-inline season-input" data-id="${p.id}" value="${esc(p.season)}" maxlength="16" /></td>
        <td><input type="text" class="ss-inline designer-input" data-id="${p.id}" value="${esc(p.designer)}" maxlength="128" /></td>
        <td><input type="text" class="ss-inline owner-input" data-id="${p.id}" value="${esc(p.owner)}" maxlength="128" /></td>
        <td>
          <select class="lifecycle-select ${esc(p.lifecycleCssClass)}" data-action="set-lifecycle" data-id="${p.id}"
                  data-prev-lifecycle="${esc(p.lifecycleStatus)}">
            ${lifecycleOpts.map(([v, l]) => `<option value="${v}"${p.lifecycleStatus === v ? ' selected' : ''}>${l}</option>`).join('')}
          </select>
        </td>
        <td><span class="tag st-${esc(p.status)}">${esc(p.statusLabel)}</span></td>
        <td class="td-mono">${esc(p.dueDateLabel)}</td>
        <td><a class="btn-open" href="/Pieces?patternId=${p.id}&style=${encodeURIComponent(p.styleKey || 'skinny')}">Pattern</a></td>
      </tr>`).join('');
    applyFilters(true);
  }

  function rowMatchesFilter(row) {
    const lc = row.dataset.lifecycle || '';
    return currentLifecycle === 'All' || lc === currentLifecycle;
  }

  function applyFilters(resetPage) {
    if (resetPage) currentPage = 1;
    const rows = [...document.querySelectorAll('#ss-tbody tr')];
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

  applyFilters(true);

  function esc(s) {
    if (!s) return '';
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
  }

  function debounce(fn, ms) {
    let t;
    return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
  }
})();
