mergeInto(LibraryManager.library, {
  SkiViewBootMark: function (ptr) {
    var msg = UTF8ToString(ptr);
    var el = document.getElementById("sv-boot");
    if (el) {
      el.style.display = "block";
      el.textContent = msg;
    }
    if (window.svBootMark) window.svBootMark(msg);
  },

  SkiViewPrepareWebLoad: function () {
    var secure = window.isSecureContext === true && typeof window.caches !== "undefined";
    if (secure) return 1;
    if (typeof Module === "undefined") return 0;
    Module.cacheControl = function () { return "no-store"; };
    Module.cachedFetch = function (resource, init) {
      if (typeof Module.fetchWithProgress === "function") return Module.fetchWithProgress(resource, init);
      return fetch(resource, init);
    };
    return 0;
  }
});
