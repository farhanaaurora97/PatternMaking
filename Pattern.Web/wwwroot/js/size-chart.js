(function initSizeChartModals() {
  const addSizeOverlay = document.getElementById('modal-add-size');
  const btnAddSize = document.getElementById('btn-add-size');
  const btnCancelSize = document.getElementById('btn-add-size-cancel');
  const btnConfirmSize = document.getElementById('btn-add-size-confirm');
  const inputSizeLabel = document.getElementById('add-size-label');
  const errSize = document.getElementById('add-size-error');

  const addMpOverlay = document.getElementById('modal-add-measurement');
  const btnAddMp = document.getElementById('btn-add-measurement');
  const btnCancelMp = document.getElementById('btn-add-mp-cancel');
  const btnConfirmMp = document.getElementById('btn-add-mp-confirm');
  const inputMpName = document.getElementById('add-mp-name');
  const selectMpCopy = document.getElementById('add-mp-copy');
  const errMp = document.getElementById('add-mp-error');

  function openSize() {
    addSizeOverlay?.classList.add('open');
    errSize?.classList.remove('show');
    if (inputSizeLabel) inputSizeLabel.value = '';
    setTimeout(() => inputSizeLabel?.focus(), 200);
  }

  function closeSize() {
    addSizeOverlay?.classList.remove('open');
    errSize?.classList.remove('show');
  }

  function openMp() {
    addMpOverlay?.classList.add('open');
    errMp?.classList.remove('show');
    if (inputMpName) inputMpName.value = '';
    setTimeout(() => inputMpName?.focus(), 200);
  }

  function closeMp() {
    addMpOverlay?.classList.remove('open');
    errMp?.classList.remove('show');
  }

  btnAddSize?.addEventListener('click', openSize);
  btnCancelSize?.addEventListener('click', closeSize);
  addSizeOverlay?.addEventListener('click', e => { if (e.target === addSizeOverlay) closeSize(); });

  btnAddMp?.addEventListener('click', openMp);
  btnCancelMp?.addEventListener('click', closeMp);
  addMpOverlay?.addEventListener('click', e => { if (e.target === addMpOverlay) closeMp(); });

  btnConfirmSize?.addEventListener('click', async () => {
    const label = inputSizeLabel?.value.trim() ?? '';
    if (!label) {
      errSize?.classList.add('show');
      return;
    }
    errSize?.classList.remove('show');

    const res = await fetch('/SizeChart/AddColumn', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ label }),
    });

    if (!res.ok) {
      let msg = 'Could not add column.';
      try {
        const data = await res.json();
        if (data?.error) msg = data.error;
      } catch { /* ignore */ }
      window.toast?.('Size chart', msg, 'error', '⚠️');
      return;
    }

    closeSize();
    window.toast?.('Column added', `${label} — values extrapolated from the last two sizes.`, 'success', '✨');
    window.location.reload();
  });

  btnConfirmMp?.addEventListener('click', async () => {
    const name = inputMpName?.value.trim() ?? '';
    if (!name) {
      errMp?.classList.add('show');
      return;
    }
    errMp?.classList.remove('show');

    const copyFrom = selectMpCopy?.value ?? '';

    const res = await fetch('/SizeChart/AddRow', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ name, copyFrom }),
    });

    if (!res.ok) {
      let msg = 'Could not add row.';
      try {
        const data = await res.json();
        if (data?.error) msg = data.error;
      } catch { /* ignore */ }
      window.toast?.('Size chart', msg, 'error', '⚠️');
      return;
    }

    closeMp();
    window.toast?.('Row added', `${name} — copied grade from ${copyFrom || 'reference row'}.`, 'success', '✨');
    window.location.reload();
  });

  document.addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    closeSize();
    closeMp();
  });

  document.querySelectorAll('.sc-cell-input').forEach((input) => {
    input.addEventListener('change', async () => {
      const measurement = input.dataset.measurement ?? '';
      const columnIndex = parseInt(input.dataset.col ?? '-1', 10);
      const value = parseFloat(input.value);
      if (!measurement || columnIndex < 0 || Number.isNaN(value)) return;

      const res = await fetch('/SizeChart/UpdateCell', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ measurementPoint: measurement, columnIndex, value }),
      });

      if (res.ok) {
        window.toast?.('Size chart saved', `${measurement} updated`, 'success', '✓');
      } else {
        let msg = 'Could not save cell.';
        try {
          const data = await res.json();
          if (data?.error) msg = data.error;
        } catch { /* ignore */ }
        window.toast?.('Save failed', msg, 'error', '⚠️');
      }
    });
  });

  async function saveRowMetaFromRow(row) {
    if (!row) return;
    const tolEl = row.querySelector('.sc-tolerance-input');
    const methodEl = row.querySelector('.sc-method-input');
    const measurement = tolEl?.dataset.measurement ?? methodEl?.dataset.measurement ?? '';
    const toleranceCm = parseFloat(tolEl?.value ?? '0');
    const measurementMethod = methodEl?.value ?? '';
    if (!measurement || Number.isNaN(toleranceCm)) return;

    const res = await fetch('/SizeChart/UpdateRowMeta', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ measurementPoint: measurement, toleranceCm, measurementMethod }),
    });

    if (res.ok) {
      window.toast?.('Row meta saved', `${measurement} tolerance / method`, 'success', '✓');
    } else {
      window.toast?.('Save failed', 'Could not update row meta', 'error', '⚠️');
    }
  }

  document.querySelectorAll('.sc-tolerance-input, .sc-method-input').forEach((input) => {
    input.addEventListener('change', () => saveRowMetaFromRow(input.closest('tr')));
  });

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
      if (!window.confirm(`Delete measurement row "${measurement}"?`)) return;
      await postDelete('/SizeChart/DeleteRow', { measurementPoint: measurement }, 'Row deleted', measurement);
    });
  });

  document.querySelectorAll('[data-action="delete-column"]').forEach((btn) => {
    btn.addEventListener('click', async () => {
      const col = parseInt(btn.dataset.col ?? '-1', 10);
      const label = btn.dataset.label ?? '';
      if (col < 0) return;
      if (!window.confirm(`Delete size column "${label}"?`)) return;
      await postDelete('/SizeChart/DeleteColumn', { columnIndex: col }, 'Column deleted', label);
    });
  });
})();
