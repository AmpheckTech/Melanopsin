using Melanopsin.UI;

namespace Melanopsin.UI.Demo.Views;

public sealed partial class MainView : Component {
    private const string MotionPage = "motion";
    private const string ScenePage = "scene";
    private const string TypographyPage = "typography";
    private const string LayoutPage = "layout";
    private const string CodePage = "markup";
    private string _selectedPage = MotionPage;

    public bool ShowMotion => _selectedPage == MotionPage;

    public bool ShowScene => _selectedPage == ScenePage;

    public bool ShowTypography => _selectedPage == TypographyPage;

    public bool ShowLayout => _selectedPage == LayoutPage;

    public string MotionTabClass => TabClass(MotionPage);

    public string SceneTabClass => TabClass(ScenePage);

    public string TypographyTabClass => TabClass(TypographyPage);

    public string LayoutTabClass => TabClass(LayoutPage);

    public string CodeTabClass => TabClass(CodePage);

    public void SelectMotion() => Select(MotionPage);

    public void SelectScene() => Select(ScenePage);

    public void SelectTypography() => Select(TypographyPage);

    public void SelectLayout() => Select(LayoutPage);

    public void SelectCode() => Select(CodePage);

    private void Select(string page) {
        if (_selectedPage == page) {
            return;
        }

        _selectedPage = page;
    }

    private string TabClass(string page) =>
        _selectedPage == page ? "nav-tab selected" : "nav-tab";
}
