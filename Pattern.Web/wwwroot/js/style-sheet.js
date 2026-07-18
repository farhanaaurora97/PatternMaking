// style-sheet.js — PLM style register (season, owner, lifecycle)

(function initStyleSheet() {
  const searchInput = document.getElementById('ss-search');
  const tblCount = document.getElementById('ss-count');
  const btnClear = document.getElementById('ss-clear-lifecycle');
  let currentLifecycle = 'All';
  const sort = { col: '', asc: true };

  document.getElementById('ss-btn-add')?.addEventListener('click', () => {
    document.getElementById('btn-new-pattern')?.click();
  });

  document.getElementById('ss-lifecycle-tabs')?.addEventListener('click', (e) => {
    const btn = e.target.closest('[data-lifecycle]');
    if (!btn) return;
    currentLifecycle = btn.dataset.lifecycle || 'All';
    document.querySelectorAll('#ss-lifecycle-tabs .cat-tab').forEach((t) => t.classList.toggle('active', t === btn));
    if (btnClear) btnClear.hidden = currentLifecycle === 'All';
    applyFilters();
  });

  btnClear?.addEventListener('click', () => {
    currentLifecycle = 'All';
    if (btnClear) btnClear.hidden = true;
    document.querySelectorAll('#ss-lifecycle-tabs .cat-tab').forEach((t) => {
      t.classList.toggle('active', t.dataset.lifecycle === 'All');
    });
    applyFilters();
  });

  searchInput?.addEventListener('input', debounce(() => refreshRows(), 250));

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
    if (tblCount) {
      tblCount.dataset.total = String(patterns.length);
      tblCount.textContent = `${patterns.length} of ${patterns.length}`;
    }
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
    applyFilters();
  }

  function applyFilters() {
    document.querySelectorAll('#ss-tbody tr').forEach((row) => {
      const lc = row.dataset.lifecycle || '';
      const show = currentLifecycle === 'All' || lc === currentLifecycle;
      row.style.display = show ? '' : 'none';
    });
    if (!tblCount) return;
    const total = parseInt(tblCount.dataset.total || '0', 10);
    let n = 0;
    document.querySelectorAll('#ss-tbody tr').forEach((r) => { if (r.style.display !== 'none') n++; });
    tblCount.textContent = `${n} of ${total}`;
  }

  function esc(s) {
    if (!s) return '';
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
  }

  function debounce(fn, ms) {
    let t;
    return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
  }
})();
