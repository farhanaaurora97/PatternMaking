// dashboard-charts.js — Chart.js for PatternPro Desktop dashboard

window.patternProDashboard = (function () {
  if (typeof Chart === 'undefined') {
    return { initFromElement: function () {}, renderFromJson: function () {} };
  }

  const font = "'Source Sans 3', system-ui, sans-serif";
  Chart.defaults.font.family = font;
  Chart.defaults.color = '#64748b';

  const grid = '#f1f5f9';
  const tick = '#94a3b8';

  function colorForPantCategory(label) {
    const k = String(label || '').trim().toLowerCase();
    const palette = {
      denim: '#1e3a5f', trousers: '#2d5282', chinos: '#3a6491', cargo: '#1e5f5a', linen: '#2a7a6e',
      leather: '#4a3320', palazzo: '#5a3a5c', corduroy: '#5e3d1e', workwear: '#4a3a1e',
      joggers: '#3d5166', shorts: '#5c6f82', sweatpants: '#4a5568', dress: '#2d3748', other: '#718096',
    };
    return palette[k] ?? palette.other;
  }

  function destroyChart(id) {
    const el = document.getElementById(id);
    if (!el) return;
    Chart.getChart(el)?.destroy();
  }

  function destroyAll() {
    destroyChart('chart-status');
    destroyChart('chart-styles');
    destroyChart('chart-pant-types');
  }

  function fillPantLegend(list) {
    const el = document.getElementById('chart-pant-types-legend');
    if (!el) return;
    if (!list?.length) { el.innerHTML = ''; return; }
    const labels = [...new Set(list.map(s => s.label))].sort((a, b) => a.localeCompare(b));
    el.innerHTML = labels.map(lab => {
      const c = colorForPantCategory(lab);
      return `<span class="pt-leg"><span class="pt-swatch" style="background:${c}"></span>${lab}</span>`;
    }).join('');
  }

  function renderCharts(payload) {
    const statusList = payload.status || [];
    const fit = payload.stylesByFit || payload.StylesByFit;
    const pantTypeList = payload.pantTypes || [];

    // Status doughnut
    const statusFiltered = statusList.filter(s => s.count > 0);
    const canvasStatus = document.getElementById('chart-status');
    const emptyStatus = document.getElementById('chart-status-empty');
    if (canvasStatus) {
      if (statusFiltered.length === 0) {
        canvasStatus.style.display = 'none';
        if (emptyStatus) { emptyStatus.hidden = false; }
      } else {
        canvasStatus.style.display = 'block';
        if (emptyStatus) emptyStatus.hidden = true;
        const total = statusFiltered.reduce((a, s) => a + s.count, 0);
        new Chart(canvasStatus, {
          type: 'doughnut',
          data: {
            labels: statusFiltered.map(s => s.label),
            datasets: [{
              data: statusFiltered.map(s => s.count),
              backgroundColor: statusFiltered.map(s => s.color),
              borderWidth: 2,
              borderColor: '#fff',
            }],
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '65%',
            plugins: {
              legend: { position: 'bottom', labels: { boxWidth: 10, usePointStyle: true } },
              tooltip: {
                callbacks: {
                  label(ctx) {
                    const n = ctx.raw;
                    const pct = total ? Math.round((n / total) * 100) : 0;
                    return ` ${n} (${pct}%)`;
                  },
                },
              },
            },
          },
        });
      }
    }

    // Stacked fit bar
    const canvasStyles = document.getElementById('chart-styles');
    if (canvasStyles && fit?.labels?.length && fit?.datasets?.length) {
      new Chart(canvasStyles, {
        type: 'bar',
        data: {
          labels: fit.labels,
          datasets: fit.datasets.map(d => ({
            label: d.label,
            data: d.data,
            backgroundColor: d.backgroundColor,
            borderRadius: 3,
            maxBarThickness: 18,
          })),
        },
        options: {
          indexAxis: 'y',
          responsive: true,
          maintainAspectRatio: false,
          scales: {
            x: { stacked: true, beginAtZero: true, grid: { color: grid }, ticks: { stepSize: 1, color: tick } },
            y: { stacked: true, grid: { display: false }, ticks: { color: '#334155' } },
          },
          plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, usePointStyle: true } } },
        },
      });
    }

    // Pant types bar
    const canvasPant = document.getElementById('chart-pant-types');
    const pantHint = document.getElementById('chart-pant-types-hint');
    if (canvasPant && pantTypeList.length > 0) {
      const maxPt = Math.max(1, ...pantTypeList.map(s => s.count));
      const barColors = pantTypeList.map(s => colorForPantCategory(s.label));
      if (pantHint) {
        pantHint.textContent = `${pantTypeList.reduce((a, s) => a + s.count, 0)} patterns across ${pantTypeList.length} product lines`;
      }
      fillPantLegend(pantTypeList);
      new Chart(canvasPant, {
        type: 'bar',
        data: {
          labels: pantTypeList.map(s => s.label),
          datasets: [{
            data: pantTypeList.map(s => s.count),
            backgroundColor: barColors,
            borderRadius: 6,
            maxBarThickness: 24,
          }],
        },
        options: {
          indexAxis: 'y',
          responsive: true,
          maintainAspectRatio: false,
          scales: {
            x: { beginAtZero: true, suggestedMax: maxPt, grid: { color: grid }, ticks: { stepSize: 1, color: tick } },
            y: { grid: { display: false }, ticks: { color: '#334155' } },
          },
          plugins: { legend: { display: false } },
        },
      });
    } else {
      if (pantHint) pantHint.textContent = '';
      fillPantLegend([]);
    }
  }

  return {
    initFromElement() {
      const el = document.getElementById('dashboard-chart-data');
      if (!el) return;
      try {
        destroyAll();
        renderCharts(JSON.parse(el.textContent || '{}'));
      } catch { /* ignore */ }
    },
    renderFromJson(json) {
      try {
        const payload = typeof json === 'string' ? JSON.parse(json) : json;
        const el = document.getElementById('dashboard-chart-data');
        if (el) el.textContent = JSON.stringify(payload);
        destroyAll();
        renderCharts(payload);
      } catch { /* ignore */ }
    },
  };
})();
