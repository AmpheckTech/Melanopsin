using System.Globalization;
using Melanopsin.UI;

namespace Melanopsin.UI.Demo.Views;

public sealed partial class MainView : Component {
    private string _name = "Ada";
    private string _theme = "Forest";
    private double _tone = 64;
    private bool _showDetails = true;

    public string Name => _name;

    public string Theme => _theme;

    public double Tone => _tone;

    public bool ShowDetails => _showDetails;

    public int ThemeIndex => _theme switch {
        "Forest" => 0,
        "Ocean" => 1,
        "Sunset" => 2,
        _ => 0,
    };

    public string Greeting {
        get {
            var displayName = string.IsNullOrWhiteSpace(_name)
                ? "there"
                : _name.Trim();
            return $"Hello, {displayName}.";
        }
    }

    public string ThemeSummary => $"{_theme} at {_tone:0}%";

    public IReadOnlyList<string> Tags => _theme switch {
        "Forest" => ["native", "calm", "green"],
        "Ocean" => ["native", "fluid", "blue"],
        "Sunset" => ["native", "warm", "violet"],
        _ => ["native"],
    };

    protected override void OnBuildCompleted(Node root) {
        base.OnBuildCompleted(root);
        NameInput.TextChanged += value => {
            _name = value;
            PreviewText.Text = Greeting;
        };
        ThemeSelect.SelectionChanged += (_, value) => {
            _theme = value;
            Invalidate();
        };
        ToneSlider.ValueChanged += value => {
            _tone = value;
            ToneValue.Text = $"{value:0}%";
            PreviewProgress.SetAttributeValue("value", value);
            DetailsText.Text = ThemeSummary;
        };
        DetailsCheck.CheckedChanged += value => {
            _showDetails = value;
            Invalidate();
        };
        ApplyButton.Clicked += () => {
            StateBadge.Text = "Applied";
            DetailsText.Text =
                $"Saved at {DateTime.Now.ToString(
                    "HH:mm:ss",
                    CultureInfo.InvariantCulture)}";
        };
    }
}
