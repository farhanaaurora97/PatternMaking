// blockgen.js — Block Generator page: inline ease editing, AJAX save/reset

(function initBlockGen() {

  // ── Inline ease value editing ──────────────────────────────────────
  document.getElementById('ease-list')?.addEventListener('click', e => {
    const el = e.target.closest('.ease-val');
    if (!el || el.contentEditable === 'true') return;
    el.contentEditable = 'true';
    el.focus();
    const range = document.createRange();
    range.selectNodeContents(el);
    window.getSelection().removeAllRanges();
    window.getSelection().addRange(range);

    el.addEventListener('blur', () => commitEase(el), { once: true });
    el.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); el.blur(); } });
  });

  async function commitEase(el) {
    el.contentEditable = 'false';
    const raw = parseFloat(el.textContent);
    if (isNaN(raw)) { location.reload(); return; }

    const styleKey = el.dataset.easeStyle;
    const key = el.dataset.easeKey;
    const res = await fetch('/BlockGenerator/SaveEase', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ styleKey, measurementKey: key, value: raw }),
    });
    if (res.ok) {
      const updated = await res.json();
      rerenderEase(styleKey, updated);
      toast('Ease Updated', `${key} ease set to ${raw > 0 ? '+' : ''}${raw} cm`, 'success', '✏️');
    }
  }

  // ── Reset ease ─────────────────────────────────────────────────────
  document.getElementById('btn-reset-ease')?.addEventListener('click', async () => {
    const styleKey = document.getElementById('ease-list')?.dataset.style;
    const res = await fetch(`/BlockGenerator/ResetEase?styleKey=${styleKey}`, { method: 'POST' });
    if (res.ok) {
      const updated = await res.json();
      rerenderEase(styleKey, updated);
      toast('Reset', 'Ease values reset to defaults', 'info', '↺');
    }
  });

  // ── Generate block ─────────────────────────────────────────────────
  document.getElementById('btn-generate-block')?.addEventListener('click', async () => {
    const styleKey = document.getElementById('ease-list')?.dataset.style;
    const res = await fetch(`/BlockGenerator/GenerateBlock?styleKey=${styleKey}`, { method: 'POST' });
    if (res.ok) {
      const { styleLabel } = await res.json();
      toast('Block Generated', `${styleLabel} block created with current ease values`, 'success', '⊞');
    }
  });

  function rerenderEase(styleKey, easeValues) {
    const list = document.getElementById('ease-list');
    if (!list) return;
    list.innerHTML = Object.entries(easeValues).map(([k, v]) => {
      const cls = v < 0 ? 'ease-neg' : v > 0 ? 'ease-pos' : 'ease-zero';
      const display = (v > 0 ? '+' : '') + v + ' cm';
      return `<div class="ease-row">
        <span class="ease-key">${k}</span>
        <span class="ease-val ${cls}" data-ease-key="${k}" data-ease-style="${styleKey}">${display}</span>
      </div>`;
    }).join('');
  }

})();
