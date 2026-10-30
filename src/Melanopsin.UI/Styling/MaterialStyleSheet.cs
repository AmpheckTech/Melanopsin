namespace Melanopsin.UI.Styling;

public static class MaterialStyleSheet {
    public static StyleSheet Create() {
        return StyleSheetParser.Parse("""
            Page { background: #fffbfe; color: #1c1b1f; }
            *:disabled { cursor: not-allowed; }
            TextBlock { background: transparent; color: #1c1b1f; font-size: 16fu; text-align: start; vertical-align: center; }
            Button { background: #6750a4; color: #ffffff; border-radius: 5ru; padding: 3su; font-size: 15fu; text-align: center; vertical-align: center; cursor: pointer; transition-duration: 3du; transition-easing: ease-in-out; }
            Button:hover { background: #7c66b3; }
            Button:pressed { background: #58418f; }
            Button:focused { border-color: #1c1b1f; border-width: 2hx; }
            Button:disabled { opacity: 0.38; cursor: not-allowed; }
            Badge { background: #e7e0ec; color: #1c1b1f; border-radius: 3ru; padding: 2su; font-size: 14fu; text-align: center; vertical-align: center; }
            ListRow { background: #e7e0ec; color: #1c1b1f; border-radius: 3ru; padding: 2su; }
            ScrollViewport { background: #f3edf7; border-color: #79747e; border-width: 1hx; }
            Panel { background: transparent; padding: 0px; }
            Input { background: #ffffff; color: #1c1b1f; border-color: #aab8b2; border-width: 1hx; border-radius: 2ru; padding: 2su; font-size: 15fu; cursor: text; }
            Input:focused { border-color: #27745f; border-width: 2hx; }
            Input:disabled { cursor: not-allowed; }
            CheckBox { background: transparent; color: #1c1b1f; padding: 1su; font-size: 15fu; }
            RadioButton { background: transparent; color: #1c1b1f; padding: 1su; font-size: 15fu; }
            Option { background: transparent; color: #1c1b1f; padding: 2su; font-size: 15fu; }
            Switch { background: transparent; color: #1c1b1f; padding: 1su; font-size: 15fu; }
            ComboBox { background: #ffffff; color: #1c1b1f; border-color: #aab8b2; border-width: 1hx; border-radius: 2ru; padding: 2su; font-size: 15fu; cursor: pointer; transition-duration: 2du; transition-easing: ease-in-out; }
            ComboBox:hover { background: #f3f8f5; border-color: #6f9a8c; }
            ComboBox:pressed { background: #e4eee8; }
            ComboBox:expanded { border-color: #1f6d59; }
            ComboBox:focused { border-color: #1f6d59; }
            ComboBox:disabled { background: #f1f3f2; color: #6f7774; border-color: #c9cfcc; opacity: 0.6; cursor: not-allowed; }
            ComboBoxPopup { background: #ffffff; color: #1c1b1f; border-color: #b7c5bf; border-width: 1hx; border-radius: 2ru; padding: 2su; font-size: 15fu; }
            Slider { background: transparent; padding: 2su; }
            ProgressBar { background: transparent; padding: 1su; }
            Image { background: #e7e0ec; border-radius: 3ru; }
            Canvas { background: transparent; }
            TabControl { background: transparent; padding: 0px; }
            TabItem { background: transparent; padding: 0px; }
            Button.tab-button { background: #e3ece7; color: #405a51; border-radius: 2ru; padding: 1su; font-size: 13fu; }
            Button.tab-button:selected { background: #1f6d59; color: #ffffff; }
            Separator { background: #c9d6d0; padding: 0px; }
            Icon { background: transparent; color: #1c1b1f; font-size: 18iu; text-align: center; vertical-align: center; }
            OverlayHost { background: transparent; padding: 0px; }
            Dialog { background: #fffbfe; color: #1c1b1f; border-color: #cac4d0; border-width: 1hx; border-radius: 6ru; padding: 5su; }
            .dialog-title { color: #1c1b1f; font-size: 22fu; padding: 1su; }
            .dialog-message { color: #49454f; font-size: 15fu; padding: 2su; }
            Button.dialog-button { background: #e8e0ec; color: #1c1b1f; border-radius: 3ru; font-size: 14fu; }
            """);
    }
}
