mergeInto(LibraryManager.library, {
  JSRequestFullscreen: function() {
    try {
      var getCanvas = function() {
        if (typeof Module !== 'undefined' && Module['canvas']) return Module['canvas'];
        var byId = document.getElementById('unity-canvas');
        if (byId) return byId;
        var q = document.querySelector('canvas');
        return q || null;
      };
      var canvas = getCanvas();
      var elem = canvas || document.documentElement;

      if (elem && elem.requestFullscreen) { elem.requestFullscreen(); return; }
      if (elem && elem.webkitRequestFullscreen) { elem.webkitRequestFullscreen(); return; }
      if (elem && elem.mozRequestFullScreen) { elem.mozRequestFullScreen(); return; }
      if (elem && elem.msRequestFullscreen) { elem.msRequestFullscreen(); return; }

      // Fallback: try documentElement
      var de = document.documentElement;
      if (de.requestFullscreen) { de.requestFullscreen(); return; }
      if (de.webkitRequestFullscreen) { de.webkitRequestFullscreen(); return; }
      if (de.mozRequestFullScreen) { de.mozRequestFullScreen(); return; }
      if (de.msRequestFullscreen) { de.msRequestFullscreen(); return; }
    } catch (e) {
      console.warn('[FullScreen.jslib] JSRequestFullscreen error:', e);
    }
  },

  JSExitFullscreen: function() {
    try {
      var d = document;
      if (d.exitFullscreen) { d.exitFullscreen(); return; }
      if (d.webkitExitFullscreen) { d.webkitExitFullscreen(); return; }
      if (d.mozCancelFullScreen) { d.mozCancelFullScreen(); return; }
      if (d.msExitFullscreen) { d.msExitFullscreen(); return; }
    } catch (e) {
      console.warn('[FullScreen.jslib] JSExitFullscreen error:', e);
    }
  }
});
