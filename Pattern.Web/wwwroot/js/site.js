// site.js — Shared layout behaviours: clock, toast, notifications, modal, style switcher

// ── Clock ─────────────────────────────────────────────────────────────────────
(function startClock() {
  function tick() {
    const now = new Date();
    const el = document.getElementById('tb-clock');
    if (el) el.textContent =
      String(now.getHours()).padStart(2,'0') + ':' +
      String(now.getMinutes()).padStart(2,'0') + ':' +
      String(now.getSeconds()).padStart(2,'0');
  }
  tick();
  setInterval(tick, 1000);
})();

// ── Toast ─────────────────────────────────────────────────────────────────────
window.toast = function(title, msg, type = 'success', icon = '✅') {
  const container = document.getElementById('toast-container');
  if (!container) return;
  const t = document.createElement('div');
  t.className = `toast toast-${type}`;
  t.innerHTML = `
    <div class="toast-icon">${icon}</div>
    <div style="flex:1"><div class="toast-title">${title}</div><div class="toast-msg">${msg}</div></div>
    <div class="toast-close" onclick="this.parentElement.remove()">×</div>`;
  container.appendChild(t);
  requestAnimationFrame(() => requestAnimationFrame(() => t.classList.add('show')));
  setTimeout(() => { t.classList.remove('show'); setTimeout(() => t.remove(), 350); }, 3500);
};

// ── Notification panel ────────────────────────────────────────────────────────
(function initNotifications() {
  const btn   = document.getElementById('notif-btn');
  const panel = document.getElementById('notif-panel');
  const clear = document.getElementById('notif-clear-btn');
  if (!btn || !panel) return;

  btn.addEventListener('click', e => { panel.classList.toggle('open'); e.stopPropagation(); });
  document.addEventListener('click', () => panel.classList.remove('open'));
  clear?.addEventListener('click', e => {
    e.stopPropagation();
    document.getElementById('notif-list').innerHTML =
      '<div style="padding:20px;text-align:center;color:var(--ink4);font-size:12.5px">No notifications</div>';
    document.getElementById('notif-dot')?.style && (document.getElementById('notif-dot').style.display = 'none');
  });
})();

// ── New Pattern Modal ─────────────────────────────────────────────────────────
(function initModal() {
  const overlay = document.getElementById('modal-overlay');
  const btnOpen = document.getElementById('btn-new-pattern');
  const btnAdd  = document.getElementById('btn-add-pattern');
  const btnCancel = document.getElementById('btn-modal-cancel');
  const btnCreate = document.getElementById('btn-modal-create');
  const errEl   = document.getElementById('modal-error');
  const styleSelect = document.getElementById('m-style');
  const categorySelect = document.getElementById('m-category');
  const customFitRow = document.getElementById('m-custom-fit-row');
  const customCategoryRow = document.getElementById('m-custom-category-row');
  const customBaseRow = document.getElementById('m-custom-base-row');
  const baseSelect = document.getElementById('m-base');
  if (!overlay) return;

  function syncCustomFields() {
    const customFit = styleSelect?.value === '__custom_fit__';
    const customCat = categorySelect?.value === '__custom_category__';
    const customBase = baseSelect?.value === '__custom_base__';
    if (customFitRow) customFitRow.hidden = !customFit;
    if (customCategoryRow) customCategoryRow.hidden = !customCat;
    if (customBaseRow) customBaseRow.hidden = !customBase;
  }

  styleSelect?.addEventListener('change', syncCustomFields);
  categorySelect?.addEventListener('change', syncCustomFields);
  baseSelect?.addEventListener('change', syncCustomFields);
  syncCustomFields();

  function open()  { overlay.classList.add('open'); setTimeout(() => document.getElementById('m-name')?.focus(), 200); }
  function close() { overlay.classList.remove('open'); errEl?.classList.remove('show'); }

  btnOpen?.addEventListener('click', open);
  btnAdd?.addEventListener('click', open);
  overlay.addEventListener('click', e => { if (e.target === overlay) close(); });
  btnCancel?.addEventListener('click', close);

  btnCreate?.addEventListener('click', async () => {
    const name       = document.getElementById('m-name')?.value.trim();
    const categoryKey = document.getElementById('m-category')?.value ?? 'denim';
    const styleKey   = document.getElementById('m-style')?.value;
    const customFitLabel = document.getElementById('m-custom-fit')?.value.trim() || null;
    const customCategoryLabel = document.getElementById('m-custom-category')?.value.trim() || null;
    const customBaseSizeLabel = document.getElementById('m-custom-base')?.value.trim() || null;
    const base       = document.getElementById('m-base')?.value;
    const designer   = document.getElementById('m-designer')?.value.trim();
    const season     = document.getElementById('m-season')?.value.trim() || null;
    const owner      = document.getElementById('m-owner')?.value.trim() || null;
    const lifecycleStatus = document.getElementById('m-lifecycle')?.value || 'Idea';
    if (!name) { errEl?.classList.add('show'); return; }
    if (styleKey === '__custom_fit__' && !customFitLabel) {
      if (errEl) { errEl.textContent = 'Enter a custom fit name.'; errEl.classList.add('show'); }
      return;
    }
    if (categoryKey === '__custom_category__' && !customCategoryLabel) {
      if (errEl) { errEl.textContent = 'Enter a custom pant type.'; errEl.classList.add('show'); }
      return;
    }
    if (base === '__custom_base__' && !customBaseSizeLabel) {
      if (errEl) { errEl.textContent = 'Enter a custom base size.'; errEl.classList.add('show'); }
      return;
    }
    errEl?.classList.remove('show');
    if (errEl) errEl.textContent = 'Please enter a pattern name.';

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const res = await fetch('/Home/Create', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        ...(token ? { 'RequestVerificationToken': token } : {}),
      },
      body: JSON.stringify({ name, categoryKey, styleKey, baseSize: base, designer, season, owner, lifecycleStatus, customFitLabel, customCategoryLabel, customBaseSizeLabel }),
    });
    if (!res.ok) {
      let msg = 'Check your connection and try again.';
      try {
        const err = await res.json();
        if (Array.isArray(err)) msg = err.join(' ');
        else if (typeof err === 'string') msg = err;
        else if (err?.title) msg = err.title;
      } catch { /* ignore */ }
      toast('Could not create pattern', msg, 'error', '⚠️');
      return;
    }
    const pattern = await res.json();
    close();
    const nameInput = document.getElementById('m-name');
    if (nameInput) nameInput.value = '';
    window.dispatchEvent(new CustomEvent('pattern:created', { detail: pattern }));
    toast('Pattern Created', `${pattern.code} ${pattern.name} added to your workspace`, 'success', '✨');
  });

  // Keyboard: N = open, Esc = close
  document.addEventListener('keydown', e => {
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT' || e.target.contentEditable === 'true') return;
    if (e.key === 'Escape') close();
    if (e.key === 'n' || e.key === 'N') open();
  });
})();

// ── Style switcher (navigates to current page with ?style= param) ─────────────
(function initStyleSwitcher() {
  const switcher = document.getElementById('style-switcher');
  if (!switcher) return;
  switcher.addEventListener('click', e => {
    const btn = e.target.closest('[data-style]');
    if (!btn || btn.disabled) return;
    if (switcher.dataset.lock === '1') {
      toast('Fit locked', 'This pattern has a fixed fit. Open another pattern from the Dashboard.', 'info', 'ℹ️');
      return;
    }
    const style = btn.dataset.style;
    const url = new URL(window.location.href);
    url.searchParams.set('style', style);
    window.location.href = url.toString();
  });
})();

// ── Toggle button helper ──────────────────────────────────────────────────────
document.querySelectorAll('.tog').forEach(tog => {
  if (!tog.dataset.managed) {
    tog.addEventListener('click', () => tog.classList.toggle('on'));
  }
});

// ── Import / Library placeholders ─────────────────────────────────────────────
document.getElementById('btn-import')?.addEventListener('click',  () => toast('Import', 'Feature coming soon — connect your PLM system.', 'info', 'ℹ️'));
document.getElementById('btn-library')?.addEventListener('click', () => toast('Library', 'Pattern library opens here.', 'info', '📚'));

// ── Animate stat bars on load ─────────────────────────────────────────────────
window.animateCount = function(el, target, duration = 800) {
  if (!el) return;
  const start = parseInt(el.textContent) || 0;
  const range = target - start;
  const t0 = performance.now();
  (function step(now) {
    const p = Math.min((now - t0) / duration, 1);
    const e = 1 - Math.pow(1 - p, 3);
    el.textContent = Math.round(start + range * e);
    if (p < 1) requestAnimationFrame(step);
  })(t0);
};
