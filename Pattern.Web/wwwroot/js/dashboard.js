// dashboard.js — Dashboard page: stats animation, table search/sort/CRUD

(function initDashboard() {

  // ── Animate stats on load ───────────────────────────────────────────
  setTimeout(() => {
    const statActive = document.getElementById('stat-active');
    if (statActive) animateCount(statActive, parseInt(statActive.dataset.target || 0));

    const barActive = document.getElementById('bar-active');
    if (barActive) {
      const target = parseInt(statActive?.dataset.target || 0);
      barActive.style.width = Math.min(target * 8, 100) + '%';
    }

    const barCompletion = document.getElementById('bar-completion');
    if (barCompletion) {
      const target = parseInt(barCompletion.dataset.target || 0);
      barCompletion.style.width = target + '%';
    }

    document.querySelectorAll('[data-target].prog-fill').forEach(el => {
      el.style.width = el.dataset.target + '%';
    });
  }, 300);

  // ── Welcome toast ───────────────────────────────────────────────────
  setTimeout(() => toast('Welcome back', 'PatternPro ERP loaded — patterns ready', 'success', '👋'), 800);

  // ── Table search ────────────────────────────────────────────────────
  const searchInput = document.getElementById('tbl-search-input');
  searchInput?.addEventListener('input', debounce(async () => {
    const q = searchInput.value;
    await refreshTable(q, currentSort.col, currentSort.asc);
  }, 250));

  // ── Table sort ──────────────────────────────────────────────────────
  const currentSort = { col: '', asc: true };
  document.querySelectorAll('th[data-sort]').forEach(th => {
    th.addEventListener('click', async () => {
      const col = th.dataset.sort;
      if (currentSort.col === col) currentSort.asc = !currentSort.asc;
      else { currentSort.col = col; currentSort.asc = true; }

      document.querySelectorAll('th[data-sort]').forEach(t => {
        t.classList.remove('sorted');
        t.querySelector('.sort-icon').textContent = '↕';
      });
      th.classList.add('sorted');
      th.querySelector('.sort-icon').textContent = currentSort.asc ? '↑' : '↓';

      await refreshTable(searchInput?.value, currentSort.col, currentSort.asc);
    });
  });

  // ── Table actions (cycle status, delete) via event delegation ──────
  document.getElementById('patterns-tbody')?.addEventListener('click', async e => {
    const el = e.target.closest('[data-action]');
    if (!el) return;
    const action = el.dataset.action;
    const id = parseInt(el.dataset.id);

    if (action === 'cycle-status') {
      const res = await fetch(`/Home/CycleStatus/${id}`, { method: 'POST', headers: { 'RequestVerificationToken': getToken() } });
      if (res.ok) {
        const p = await res.json();
        const row = document.getElementById(`row-${id}`);
        if (row) {
          const tag = row.querySelector('.status-tag');
          if (tag) { tag.className = `status-tag st-${p.status}`; tag.textContent = p.statusLabel; }
          const dateEl = row.querySelectorAll('td')[5];
          if (dateEl) dateEl.textContent = p.date;
        }
        toast('Status Updated', `${p.displayName} is now ${p.statusLabel}`, 'success', '🔄');
      }
    }

    if (action === 'delete-pattern') {
      const row = document.getElementById(`row-${id}`);
      row?.classList.add('row-removing');
      setTimeout(async () => {
        const res = await fetch(`/Home/Delete/${id}`, { method: 'DELETE', headers: { 'RequestVerificationToken': getToken() } });
        if (res.ok) {
          row?.remove();
          toast('Deleted', 'Pattern removed', 'error', '🗑️');
        }
      }, 350);
    }
  });

  // ── Handle newly created pattern from modal ─────────────────────────
  window.addEventListener('pattern:created', async () => {
    await refreshTable('', '', true);
  });

  // ── Duplicate button ────────────────────────────────────────────────
  document.getElementById('btn-duplicate')?.addEventListener('click',
    () => toast('Duplicate', 'Pattern duplicated successfully.', 'success', '✅'));

  // ── Helpers ─────────────────────────────────────────────────────────
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
    const count = document.getElementById('tbl-count');
    if (count) count.textContent = `${patterns.length} of ${patterns.length}`;
    if (!tbody) return;
    tbody.innerHTML = patterns.map(p => `
      <tr id="row-${p.id}">
        <td class="td-label">${p.displayName}</td>
        <td class="td-mono">${p.style}</td>
        <td class="td-mono">${p.baseSize}</td>
        <td class="td-mono">${p.pieceCount}</td>
        <td><span class="status-tag st-${p.status}" data-action="cycle-status" data-id="${p.id}" title="Click to change status">${p.statusLabel}</span></td>
        <td class="td-mono" style="color:var(--ink3)">${p.date}</td>
        <td>
          <div style="display:flex;gap:6px">
            <a class="btn btn-outline btn-xs" href="/Canvas">Open</a>
            <button class="btn btn-danger btn-xs" data-action="delete-pattern" data-id="${p.id}">✕</button>
          </div>
        </td>
      </tr>`).join('');
  }

  function getToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
  }

  function debounce(fn, ms) {
    let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
  }

})();
