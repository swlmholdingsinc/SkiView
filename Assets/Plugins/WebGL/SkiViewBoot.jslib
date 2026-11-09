mergeInto(LibraryManager.library, {
  SkiViewBootMark: function (ptr) {
    var msg = UTF8ToString(ptr);
    var el = document.getElementById("sv-boot");
    if (el) {
      el.style.display = "block";
      el.textContent = msg;
    }
    if (window.svBootMark) window.svBootMark(msg);
  }
});
