(function initGradingPage() {
  const styleKey = window.__gradingStyleKey ?? '';

  // ── Add column modal ──
  (function initAddColumn() {
    const overlay = document.getElementById('modal-add-col');
    const input = document.getElementById('col-label');
    const errBox = document.getElementById('add-col-error');
    const newChip = document.getElementById('col-preview-new');

    const open = () => { overlay?.classList.add('open'); input?.focus(); };
    const close = () => { overlay?.classList.remove('open'); resetForm(); };

    document.getElementById('btn-add-col')?.addEventListener('click', open);
    document.getElementById('modal-add-col-close')?.addEventListener('click', close);
    document.getElementById('modal-add-col-cancel')?.addEventListener('click', close);
    overlay?.addEventListener('click', e => { if (e.target === overlay) close(); });

    input?.addEventListener('input', () => {
      const val = input.value.trim().toUpperCase();
      if (val && newChip) { newChip.textContent = val; newChip.style.display = ''; }
      else if (newChip) { newChip.style.display = 'none'; }
    });

    input?.addEventListener('keydown', e => { if (e.key === 'Enter') submit(); });
    document.getElementById('col-submit')?.addEventListener('click', submit);

    async function submit() {
      const label = input?.value.trim() ?? '';
      errBox?.classList.remove('show');
      if (!label) {
        if (errBox) { errBox.textContent = 'Size label is required.'; errBox.classList.add('show'); }
        input?.focus();
        return;
      }

      const btn = document.getElementById('col-submit');
      if (btn) { btn.disabled = true; btn.textContent = 'Adding…'; }

      try {
        const body = new URLSearchParams({ label });
        const resp = await fetch('/Grading/AddColumn', {
          method: 'POST',
          headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
          body: body.toString(),
        });

        if (!resp.ok) {
          const data = await resp.json().catch(() => ({}));
          if (errBox) { errBox.textContent = data.error ?? 'Failed to add column.'; errBox.classList.add('show'); }
          return;
        }

        window.location.reload();
      } finally {
        if (btn) { btn.disabled = false; btn.textContent = 'Add column'; }
      }
    }

    function resetForm() {
      if (input) input.value = '';
      if (newChip) newChip.style.display = 'none';
      errBox?.classList.remove('show');
    }
  })();

  // ── Add row modal ──
  (function initAddRow() {
    const overlay = document.getElementById('modal-add-row');
    const inputEl = document.getElementById('row-label');
    const errBox = document.getElementById('add-row-error');
    const copyFrom = document.getElementById('row-copy-from');

    const open = () => { overlay?.classList.add('open'); inputEl?.focus(); };
    const close = () => { overlay?.classList.remove('open'); resetForm(); };

    document.getElementById('btn-add-row')?.addEventListener('click', open);
    document.getElementById('modal-add-row-close')?.addEventListener('click', close);
    document.getElementById('modal-add-row-cancel')?.addEventListener('click', close);
    overlay?.addEventListener('click', e => { if (e.target === overlay) close(); });

    inputEl?.addEventListener('keydown', e => { if (e.key === 'Enter') submit(); });
    document.getElementById('row-submit')?.addEventListener('click', submit);

    async function submit() {
      const label = inputEl?.value.trim() ?? '';
      errBox?.classList.remove('show');
      if (!label) {
        if (errBox) { errBox.textContent = 'Measurement point name is required.'; errBox.classList.add('show'); }
        inputEl?.focus();
        return;
      }

      const btn = document.getElementById('row-submit');
      if (btn) { btn.disabled = true; btn.textContent = 'Adding…'; }

      try {
        const body = new URLSearchParams({ style: styleKey, measurementPoint: label });
        if (copyFrom?.value) body.append('copyFrom', copyFrom.value);
        const resp = await fetch('/Grading/AddRow', {
          method: 'POST',
          headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
          body: body.toString(),
        });

        if (!resp.ok) {
          const data = await resp.json().catch(() => ({}));
          if (errBox) { errBox.textContent = data.error ?? 'Failed to add row.'; errBox.classList.add('show'); }
          return;
        }

        window.location.reload();
      } finally {
        if (btn) { btn.disabled = false; btn.textContent = 'Add row'; }
      }
    }

    function resetForm() {
      if (inputEl) inputEl.value = '';
      if (copyFrom) copyFrom.value = '';
      errBox?.classList.remove('show');
    }
  })();

  // ── Edit grade delta ──
  document.querySelectorAll('.gr-delta-input').forEach((input) => {
    input.addEventListener('focusin', () => { input.dataset.prevValue = input.value; });

    input.addEventListener('change', async () => {
      const sk = input.dataset.style ?? styleKey;
      const measurementPoint = input.dataset.measurement ?? '';
      const columnIndex = parseInt(input.dataset.col ?? '-1', 10);
      const delta = parseFloat(input.value);
      const prev = input.dataset.prevValue ?? input.value;
      if (!sk || !measurementPoint || columnIndex < 0 || Number.isNaN(delta)) return;

      const res = await fetch('/Grading/UpdateDelta', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ styleKey: sk, measurementPoint, columnIndex, delta }),
      });

      if (res.ok) {
        input.dataset.prevValue = String(delta);
        window.toast?.('Grading saved', `${measurementPoint} updated`, 'success', '✓');
      } else {
        input.value = prev;
        let msg = 'Could not save delta.';
        try {
          const data = await res.json();
          if (data?.error) msg = data.error;
        } catch { /* ignore */ }
        window.toast?.('Save failed', msg, 'error', '⚠️');
      }
    });
  });

  // ── Delete row / column ──
  async function postDelete(url, body, successTitle, successMsg) {
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(body),
    });
    if (res.ok) {
      window.toast?.(successTitle, successMsg, 'success', '✓');
      window.location.reload();
      return true;
    }
    let msg = 'Delete failed.';
    try {
      const data = await res.json();
      if (data?.error) msg = data.error;
    } catch { /* ignore */ }
    window.toast?.('Delete failed', msg, 'error', '⚠️');
    return false;
  }

  document.querySelectorAll('[data-action="delete-row"]').forEach((btn) => {
    btn.addEventListener('click', async () => {
      const measurement = btn.dataset.measurement ?? '';
      if (!measurement) return;
      if (!window.confirm(`Delete grading row "${measurement}"?`)) return;
      await postDelete('/Grading/DeleteRow', { styleKey, measurementPoint: measurement }, 'Row deleted', measurement);
    });
  });

  document.querySelectorAll('[data-action="delete-column"]').forEach((btn) => {
    btn.addEventListener('click', async () => {
      const col = parseInt(btn.dataset.col ?? '-1', 10);
      const label = btn.dataset.label ?? '';
      if (col < 0) return;
      if (!window.confirm(`Delete size column "${label}"?`)) return;
      await postDelete('/Grading/DeleteColumn', { columnIndex: col }, 'Column deleted', label);
    });
  });
})();
