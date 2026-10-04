(function initSizeChartPage() {
  const root = document.getElementById('size-chart-root');
  const patternId = parseInt(root?.dataset.patternId ?? '0', 10) || 0;
  const baseSize = (root?.dataset.baseSize ?? 'M').trim();

  function scopeBody() {
    return patternId > 0 ? { patternId } : {};
  }

  function scopeUrl(path) {
    return patternId > 0 ? `${path}?patternId=${patternId}` : path;
  }

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

  const patternSelect = document.getElementById('sc-pattern-select');
  const scopeSearch = document.getElementById('sc-scope-search');
  const useCustomCheckbox = document.getElementById('sc-use-custom');
  const copyGlobalBtn = document.getElementById('sc-copy-global');
  const garmentTemplateBtn = document.getElementById('sc-garment-template');

  function navigateToPattern(id) {
    window.location.href = id > 0 ? `/SizeChart?patternId=${id}` : '/SizeChart';
  }

  function filterPatternOptions() {
    if (!patternSelect || !scopeSearch) return;
    const term = scopeSearch.value.trim().toLowerCase();
    let visibleCount = 0;
    Array.from(patternSelect.options).forEach((opt, idx) => {
      if (idx === 0) {
        opt.hidden = false;
        return;
      }
      const code = (opt.dataset.code ?? '').toLowerCase();
      const name = (opt.dataset.name ?? '').toLowerCase();
      const season = (opt.dataset.season ?? '').toLowerCase();
      const match = !term || code.includes(term) || name.includes(term) || season.includes(term);
      opt.hidden = !match;
      if (match) visibleCount += 1;
    });
  }

  patternSelect?.addEventListener('change', () => {
    navigateToPattern(parseInt(patternSelect.value ?? '0', 10) || 0);
  });

  scopeSearch?.addEventListener('input', filterPatternOptions);

  scopeSearch?.addEventListener('keydown', (e) => {
    if (e.key !== 'Enter' || !patternSelect) return;
    const term = scopeSearch.value.trim().toLowerCase();
    if (!term) return;
    const match = Array.from(patternSelect.options).find((opt, idx) => {
      if (idx === 0 || opt.hidden) return false;
      const code = (opt.dataset.code ?? '').toLowerCase();
      return code === term;
    }) ?? Array.from(patternSelect.options).find((opt, idx) => idx > 0 && !opt.hidden);
    if (match) navigateToPattern(parseInt(match.value ?? '0', 10) || 0);
  });

  async function postJson(url, body) {
    return fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(body),
    });
  }

  async function postSettings(useCustomChart, chartMode) {
    if (patternId <= 0) return;
    const res = await postJson('/SizeChart/SetChartSettings', {
      patternId,
      useCustomChart,
      chartMode,
    });
    if (!res.ok) {
      let msg = 'Could not update chart settings.';
      try {
        const data = await res.json();
        if (data?.error) msg = data.error;
      } catch { /* ignore */ }
      window.toast?.('Size chart', msg, 'error', '⚠️');
      return false;
    }
    window.toast?.('Chart settings saved', useCustomChart ? 'Custom chart enabled' : 'Using global chart', 'success', '✓');
    window.location.reload();
    return true;
  }

  document.querySelectorAll('[data-action="set-chart-mode"]').forEach((input) => {
    input.addEventListener('change', async () => {
      if (!input.checked || patternId <= 0) return;
      const useCustom = useCustomCheckbox?.checked ?? false;
      await postSettings(useCustom, input.value);
    });
  });

  useCustomCheckbox?.addEventListener('change', async () => {
    if (patternId <= 0) return;
    const chartMode = document.querySelector('[name="chartMode"]:checked')?.value ?? 'Body';
    await postSettings(useCustomCheckbox.checked, chartMode);
  });

  copyGlobalBtn?.addEventListener('click', async () => {
    if (patternId <= 0) return;
    if (!window.confirm('Copy the global size chart to this style as a custom chart?')) return;
    const res = await postJson('/SizeChart/CopyGlobal', { patternId });
    if (res.ok) {
      window.toast?.('Copied', 'Global chart copied to this style.', 'success', '✓');
      window.location.reload();
      return;
    }
    let msg = 'Copy failed.';
    try {
      const data = await res.json();
      if (data?.error) msg = data.error;
    } catch { /* ignore */ }
    window.toast?.('Copy failed', msg, 'error', '⚠️');
  });

  garmentTemplateBtn?.addEventListener('click', async () => {
    if (patternId <= 0) return;
    if (!window.confirm('Load garment template (BO, C1, sizes 36–54)? This replaces the style custom chart.')) return;
    const res = await postJson('/SizeChart/InitializeGarmentTemplate', { patternId });
    if (res.ok) {
      window.toast?.('Template loaded', 'Garment chart template applied.', 'success', '✓');
      window.location.reload();
      return;
    }
    let msg = 'Template load failed.';
    try {
      const data = await res.json();
      if (data?.error) msg = data.error;
    } catch { /* ignore */ }
    window.toast?.('Template failed', msg, 'error', '⚠️');
  });

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

    const res = await postJson('/SizeChart/AddColumn', { label, ...scopeBody() });

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

    const res = await postJson('/SizeChart/AddRow', { name, copyFrom, ...scopeBody() });

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

      const res = await postJson('/SizeChart/UpdateCell', {
        measurementPoint: measurement,
        columnIndex,
        value,
        ...scopeBody(),
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

    const res = await postJson('/SizeChart/UpdateRowMeta', {
      measurementPoint: measurement,
      toleranceCm,
      measurementMethod,
      ...scopeBody(),
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
    const res = await postJson(url, body);
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
      await postDelete('/SizeChart/DeleteRow', { measurementPoint: measurement, ...scopeBody() }, 'Row deleted', measurement);
    });
  });

  document.querySelectorAll('[data-action="delete-column"]').forEach((btn) => {
    btn.addEventListener('click', async () => {
      const col = parseInt(btn.dataset.col ?? '-1', 10);
      const label = btn.dataset.label ?? '';
      if (col < 0) return;
      if (!window.confirm(`Delete size column "${label}"?`)) return;
      await postDelete('/SizeChart/DeleteColumn', { columnIndex: col, ...scopeBody() }, 'Column deleted', label);
    });
  });

  // Keep export link in sync if pattern changes via back/forward
  const exportLink = document.getElementById('sc-export-csv');
  if (exportLink && patternId > 0) {
    exportLink.setAttribute('href', scopeUrl('/SizeChart/ExportCsv'));
  }

  filterPatternOptions();
})();
