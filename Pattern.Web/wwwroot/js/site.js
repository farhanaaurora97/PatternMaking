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
  if (!overlay) return;

  function open()  { overlay.classList.add('open'); setTimeout(() => document.getElementById('m-name')?.focus(), 200); }
  function close() { overlay.classList.remove('open'); errEl?.classList.remove('show'); }

  btnOpen?.addEventListener('click', open);
  btnAdd?.addEventListener('click', open);
  overlay.addEventListener('click', e => { if (e.target === overlay) close(); });
  btnCancel?.addEventListener('click', close);

  btnCreate?.addEventListener('click', async () => {
    const name     = document.getElementById('m-name')?.value.trim();
    const styleKey = document.getElementById('m-style')?.value;
    const base     = document.getElementById('m-base')?.value;
    const designer = document.getElementById('m-designer')?.value.trim();
    if (!name) { errEl?.classList.add('show'); return; }
    errEl?.classList.remove('show');

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const res = await fetch('/Home/Create', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token ?? '' },
      body: JSON.stringify({ name, styleKey, baseSize: base, designer }),
    });
    if (res.ok) {
      const pattern = await res.json();
      close();
      document.getElementById('m-name').value = '';
      window.dispatchEvent(new CustomEvent('pattern:created', { detail: pattern }));
      toast('Pattern Created', `${pattern.code} ${pattern.name} added to your workspace`, 'success', '✨');
    }
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
    if (!btn) return;
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
