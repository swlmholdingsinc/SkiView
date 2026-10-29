using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>Required name entry before the title menu, and the Settings "CHANGE NAME" path back into the same screen.</summary>
    public partial class SkiViewUI
    {
        VisualElement nameScreen;
        TextField nameField;
        Button nameContinue, nameBack;
        Label nameHint;
        bool nameRequired, nameSync;

        void BuildPlayerName()
        {
            nameScreen = El("sv-screen sv-menu", root, "player-name");
            menus.Add(nameScreen);
            El("sv-scrim-full", nameScreen);
            var safe = El("sv-safe", nameScreen);
            var plate = El("sv-plate sv-plate--card sv-name", safe);
            El("sv-strip", plate);
            var body = El("sv-body", plate);
            var head = El("sv-header", body);
            nameBack = Btn(null, "sv-btn sv-btn--icon", head, ClosePlayerName, "back");
            nameBack.style.minWidth = 72; nameBack.style.minHeight = 72; nameBack.style.width = 88; nameBack.style.height = 88;
            var titles = El(null, head);
            titles.style.flexGrow = 1; titles.style.flexShrink = 1;
            Lb("SKI VIEW", "t-micro", titles);
            Lb("ENTER YOUR NAME", "t-h1", titles).style.whiteSpace = WhiteSpace.Normal;
            nameHint = Lb(NameRules, "t-caption", body);
            nameHint.style.whiteSpace = WhiteSpace.Normal;
            nameHint.style.marginTop = 8; nameHint.style.marginBottom = 16;

            nameField = new TextField { multiline = false, maxLength = 16, hideMobileInput = false };
            nameField.keyboardType = TouchScreenKeyboardType.Default;
            nameField.AddToClassList("sv-name__field");
            nameField.style.minHeight = 88;
            nameField.style.height = 88;
            nameField.style.width = Length.Percent(100);
            nameField.style.fontSize = 40;
            nameField.style.marginBottom = 8;
            if (nameField.labelElement != null) nameField.labelElement.style.display = DisplayStyle.None;
            nameField.RegisterValueChangedCallback(OnNameEdited);
            nameField.RegisterCallback<KeyDownEvent>(OnNameKey);
            body.Add(nameField);
            var input = nameField.Q(className: "unity-base-text-field__input");
            if (input != null) { input.style.minHeight = 72; input.style.unityTextAlign = TextAnchor.MiddleLeft; }

            nameContinue = Btn("CONTINUE", "sv-btn sv-btn--primary sv-name__go", body, CommitPlayerName, null);
            nameContinue.style.minHeight = 88;
            nameContinue.style.height = 88;
            nameContinue.style.width = Length.Percent(100);
            nameContinue.style.marginTop = 16;
            nameContinue.SetEnabled(false);
        }

        const string NameRules = "1–12 characters. Letters, digits, spaces, hyphens, or apostrophes.";

        void OnNameEdited(ChangeEvent<string> e)
        {
            if (nameSync) return;
            string raw = e.newValue ?? "";
            string shown = raw.Replace('\u2019', '\'').Replace('\u2018', '\'').ToUpperInvariant();
            if (shown.Length > 16) shown = shown.Substring(0, 16);
            if (shown != raw)
            {
                nameSync = true;
                nameField.SetValueWithoutNotify(shown);
                nameSync = false;
            }
            RefreshNameContinue();
        }

        void OnNameKey(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
            if (nameContinue != null && nameContinue.enabledSelf) CommitPlayerName();
            e.StopPropagation();
        }

        void RefreshNameContinue()
        {
            bool ok = SkiViewPlayer.TryNormalize(nameField?.value, out _);
            if (nameContinue != null) nameContinue.SetEnabled(ok);
            if (nameHint == null) return;
            string raw = nameField != null ? nameField.value : "";
            nameHint.text = string.IsNullOrWhiteSpace(raw) || ok
                ? NameRules
                : "That name won't work. " + NameRules;
        }

        /// <param name="required">First launch and any missing or corrupt save. Back does not dismiss it.</param>
        public void OpenPlayerName(bool required)
        {
            if (nameScreen == null) return;
            nameRequired = required || !SkiViewPlayer.HasName;
            if (nameBack != null) nameBack.style.display = nameRequired ? DisplayStyle.None : DisplayStyle.Flex;
            nameSync = true;
            nameField.SetValueWithoutNotify(SkiViewPlayer.Name ?? "");
            nameSync = false;
            RefreshNameContinue();
            Open(nameScreen);
            nameField.schedule.Execute(() =>
            {
                nameField.Focus();
                if (Application.platform == RuntimePlatform.Android)
                    TouchScreenKeyboard.hideInput = false;
            }).StartingIn(40);
        }

        void ClosePlayerName()
        {
            if (nameRequired || !SkiViewPlayer.HasName) return;
            Open(settings);
        }

        void CommitPlayerName()
        {
            if (!SkiViewPlayer.TrySave(nameField.value)) { RefreshNameContinue(); return; }
            bool first = nameRequired;
            nameRequired = false;
            if (first && game != null) game.RefreshNameBanner();
            if (first && PlayerPrefs.GetInt(TUT_PREF, 0) == 0 && tutStep < 0
                && !SkiViewArgs.Has("at") && !SkiViewArgs.Has("ui") && Application.isPlaying)
                StartTutorial(0);
            if (!first) Open(settings);
            else OpenTitle();
        }
    }
}
