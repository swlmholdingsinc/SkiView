using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>b9 nav graph (Scott Oct 23): title > event panel > run setup > lake select > run > pause / results, and Back (header
    /// arrow, Esc, Android back) always goes up one level; nothing dead-ends. Lake select has a TITLE button; pause MAIN MENU goes to the title.</summary>
    public partial class SkiViewUI
    {
        bool lakesFromSetup, pauseReturn;
        bool TitleFlow => runSetup != null && IsOpen(runSetup);
        static bool IsOpen(VisualElement m) => m != null && m.ClassListContains("sv-menu--open");

        public void ToTitle() { titleReturn = false; pauseReturn = false; foreach (var x in menus) x?.RemoveFromClassList("sv-menu--open"); OpenTitle(); }

        public void NavBack()
        {
            if (IsOpen(lakes)) { if (lakesFromSetup) OpenRunSetup(); else Open(pause); return; }
            if (IsOpen(runSetup)) { ToTitle(); OpenEventPanel(panelJump, panelTricks); return; }
            if (IsOpen(credits)) { Open(settings); return; }
            if ((IsOpen(settings) || IsOpen(help)) && pauseReturn && !titleReturn) { pauseReturn = false; Open(pause); return; }
            pauseReturn = false; CloseMenus();
        }
    }
}
