using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>Insets every ".sv-safe" element of a UIDocument to Screen.safeArea, never less than the design minimum
    /// (phone 88 L/R, 24 T/B; desktop 48). Values are panel px (PanelSettings reference 1920x1080). Re-applies when
    /// the safe area, resolution or orientation changes. Also toggles ".input-touch" on the root for focus-ring hiding.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class SafeAreaRoot : MonoBehaviour
    {
        public Vector4 minInsetPhone = new Vector4(88, 24, 88, 24);    // left, top, right, bottom
        public Vector4 minInsetDesktop = new Vector4(48, 48, 48, 48);
        public bool symmetricHorizontal = true;                          // landscape-left/right flip shouldn't move the layout

        VisualElement root; Rect lastArea; Vector2Int lastSize;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.RegisterCallback<GeometryChangedEvent>(_ => Apply(true));
            root.RegisterCallback<PointerDownEvent>(e => root.EnableInClassList("input-touch", e.pointerType == UnityEngine.UIElements.PointerType.touch), TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(_ => root.RemoveFromClassList("input-touch"), TrickleDown.TrickleDown);
        }

        void Update() => Apply(false);

        void Apply(bool force)
        {
            if (root == null || root.panel == null) return;
            var area = Screen.safeArea; var size = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == lastArea && size == lastSize) return;
            lastArea = area; lastSize = size;

            // Screen space is bottom-left origin; ScreenToPanel expects top-left, so flip y.
            var p = root.panel;
            Vector2 tl = RuntimePanelUtils.ScreenToPanel(p, new Vector2(area.xMin, size.y - area.yMax));
            Vector2 br = RuntimePanelUtils.ScreenToPanel(p, new Vector2(area.xMax, size.y - area.yMin));
            Vector2 full = RuntimePanelUtils.ScreenToPanel(p, new Vector2(size.x, size.y));

            var m = Application.isMobilePlatform ? minInsetPhone : minInsetDesktop;
            float l = Mathf.Max(tl.x, m.x), t = Mathf.Max(tl.y, m.y);
            float r = Mathf.Max(full.x - br.x, m.z), b = Mathf.Max(full.y - br.y, m.w);
            if (symmetricHorizontal) l = r = Mathf.Max(l, r);

            root.Query(className: "sv-safe").ForEach(safe =>
            {
                safe.style.left = l; safe.style.top = t; safe.style.right = r; safe.style.bottom = b;
            });
            // b10.27 T24: phone USS (.sv-skipick row hits, start-tall) + desktop 48 via SafeAreaRoot mins
            bool phone = Application.isMobilePlatform || Mathf.Min(size.x, size.y) <= 520;
            root.EnableInClassList("sv-phone", phone);
            root.EnableInClassList("sv-desktop", !phone);
        }
    }
}
