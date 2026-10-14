window.patternPro = window.patternPro || {
  _observers: new Map(),
  _resizeHandler: null,
  _resizeDotNet: null,

  dismissBlazorError: function () {
    var el = document.getElementById('blazor-error-ui');
    if (el) el.style.display = 'none';
  },

  getElementRect: function (element) {
    if (!element || !element.getBoundingClientRect) {
      return { x: 0, y: 0, width: 0, height: 0 };
    }
    var r = element.getBoundingClientRect();
    return { x: r.left, y: r.top, width: r.width, height: r.height };
  },

  observeElement: function (element, dotNetRef) {
    if (!element || !window.ResizeObserver || !dotNetRef) return;
    this.disconnectElement(element);
    var obs = new ResizeObserver(function () {
      dotNetRef.invokeMethodAsync('OnSlotResized').catch(function () { });
    });
    obs.observe(element);
    this._observers.set(element, obs);
    this._resizeDotNet = dotNetRef;
    if (!this._resizeHandler) {
      var self = this;
      this._resizeHandler = function () {
        if (self._resizeDotNet) {
          self._resizeDotNet.invokeMethodAsync('OnSlotResized').catch(function () { });
        }
      };
      window.addEventListener('resize', this._resizeHandler);
    }
  },

  disconnectElement: function (element) {
    var obs = this._observers.get(element);
    if (!obs) return;
    obs.disconnect();
    this._observers.delete(element);
    if (this._observers.size === 0 && this._resizeHandler) {
      window.removeEventListener('resize', this._resizeHandler);
      this._resizeHandler = null;
      this._resizeDotNet = null;
    }
  },

  focusElement: function (element) {
    if (element && typeof element.focus === 'function') {
      element.focus();
      if (typeof element.select === 'function') element.select();
    }
  },

  mountToModalRoot: function (element) {
    var root = document.getElementById('pp-modal-root');
    if (!element || !root) return;
    if (element.parentNode !== root) root.appendChild(element);
    root.setAttribute('aria-hidden', 'false');
  },

  unmountModalRootIfEmpty: function () {
    var root = document.getElementById('pp-modal-root');
    if (!root) return;
    if (root.childElementCount === 0) root.setAttribute('aria-hidden', 'true');
  },

  openDialog: function (element) {
    if (element && typeof element.showModal === 'function') {
      element.showModal();
    }
  },

  closeDialog: function (element) {
    if (element && typeof element.close === 'function') {
      element.close();
    }
  },

  initBlazorErrorUi: function () {
    var el = document.getElementById('blazor-error-ui');
    if (!el) return;
    el.style.display = 'none';
    var reload = el.querySelector('.reload');
    if (reload) {
      reload.addEventListener('click', function (e) {
        e.preventDefault();
        location.reload();
      });
    }
    var dismiss = el.querySelector('.dismiss');
    if (dismiss) {
      dismiss.addEventListener('click', function () {
        el.style.display = 'none';
      });
    }
  }
};

(function () {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      window.patternPro.initBlazorErrorUi();
    });
  } else {
    window.patternPro.initBlazorErrorUi();
  }
})();
