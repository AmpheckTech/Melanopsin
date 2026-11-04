using Melanopsin.UI;
using Melanopsin.UI.Controls;
using Melanopsin.UI.Demo;
using Melanopsin.UI.Hosting;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Demo.Views;

[MarkupComponent]
public sealed partial class ControlsSample : Component {
    private string _primaryStatus = "Left-click this button";
    private string _middleStatus = "No middle-button activity";
    private string _contextStatus = "Right-click for actions";
    private string _platformStatus = "Choose a platform action";
    private string _displayName = string.Empty;
    private string _listSummary = "No row selected";
    private double _volume = 55;
    private int _densityIndex = 1;
    private bool _reduceMotion;
    private bool _showDiagnostics = true;
    private bool _isCompact;
    private bool _isComfortable = true;
    private Node? _contentRoot;

    public string PrimaryStatus => _primaryStatus;

    public string MiddleStatus => _middleStatus;

    public string ContextStatus => _contextStatus;

    public string PlatformStatus => _platformStatus;

    public string DisplayName => _displayName;

    public string VolumeLabel => $"Output volume · {_volume:0}%";

    public string ListSummary => _listSummary;

    public double Volume => _volume;

    public int DensityIndex => _densityIndex;

    public bool ReduceMotion => _reduceMotion;

    public bool ShowDiagnostics => _showDiagnostics;

    public bool IsCompact => _isCompact;

    public bool IsComfortable => _isComfortable;

    public void HandlePrimary() {
        _primaryStatus = "Clicked received";
    }

    public void HandlePointerPressed(PointerInputEvent input) {
        if (input.Phase == PointerPhase.Down && input.IsMiddleButton) {
            _middleStatus = $"Middle down at {input.X:0}, {input.Y:0}";
        }
    }

    public void HandleMiddle(MiddleClickRequest request) {
        _middleStatus = "MiddleClicked received";
        request.Handled = true;
    }

    public void OpenContextMenu(ContextMenuRequest request) {
        request.Handled = true;
        _contextStatus = "ContextRequested received";
        new ContextMenu(request)
            .AddItem("Copy name", CopyName)
            .AddItem("Show dialog", ShowDialog)
            .AddSeparator()
            .AddItem("Clear status", ClearContextStatus)
            .Show();
    }

    public void SetReduceMotion(bool value) => _reduceMotion = value;

    public void SetDiagnostics(bool value) => _showDiagnostics = value;

    public void ChooseCompact() {
        _isCompact = true;
        _isComfortable = false;
    }

    public void ChooseComfortable() {
        _isCompact = false;
        _isComfortable = true;
    }

    public void SetDisplayName(string value) => _displayName = value;

    public void SubmitName(string value) {
        _displayName = value;
        _primaryStatus = string.IsNullOrWhiteSpace(value)
            ? "Enter a name to submit"
            : $"Submitted {value}";
    }

    public void SetVolume(double value) => _volume = value;

    public void SelectListRow(int index) {
        _listSummary = index < 0 ? "No row selected" : $"Row {index + 1} selected";
    }

    public void CopyName() {
        _ = CopyNameAsync();
    }

    public void Notify() {
        _ = NotifyAsync();
    }

    public void ShowDialog() {
        var host = FindOverlayHost(_contentRoot);
        if (host is null) {
            _platformStatus = "No overlay host is available";
            return;
        }

        _platformStatus = "Dialog opened";
        var dialog = new Dialog {
            Title = "Native platform actions",
            Message = "Clipboard and notifications are capability-based and may be unavailable on a platform.",
        };
        dialog.AddAction("Close");
        _ = host.Show(dialog);
    }

    protected override void OnBuildCompleted(Node root) {
        _contentRoot = root;
    }

    private async Task CopyNameAsync() {
        if (!PlatformServicesContext.Value.TryGetValue(this, out var services) ||
            services.Clipboard is not { IsSupported: true } clipboard) {
            _platformStatus = "Clipboard is unavailable";
            Invalidate();
            return;
        }

        var text = string.IsNullOrWhiteSpace(_displayName)
            ? "Melanopsin UI"
            : _displayName;
        try {
            await clipboard.SetTextAsync(text);
            _platformStatus = $"Copied “{text}”";
        } catch (Exception exception) {
            _platformStatus = $"Clipboard failed: {exception.Message}";
        }

        Invalidate();
    }

    private async Task NotifyAsync() {
        if (!PlatformServicesContext.Value.TryGetValue(this, out var services) ||
            services.Notifications is not { } notifications ||
            !notifications.Capabilities.CanSubmit) {
            _platformStatus = "Notifications are unavailable";
            Invalidate();
            return;
        }

        try {
            var result = await notifications.ShowAsync(
                new PortableNotificationRequest(
                    new NotificationId(1),
                    "Melanopsin",
                    string.IsNullOrWhiteSpace(_displayName)
                        ? "Controls demo"
                        : _displayName));
            _platformStatus = result.Kind switch {
                NotificationResultKind.Accepted => "Notification accepted",
                NotificationResultKind.Degraded =>
                    $"Notification shown with omissions: {result.OmittedFeatures}",
                NotificationResultKind.Rejected =>
                    $"Notification rejected: {result.OmittedFeatures}",
                _ => "Notification service is unavailable",
            };
        } catch (Exception exception) {
            _platformStatus = $"Notification failed: {exception.Message}";
        }

        Invalidate();
    }

    private void ClearContextStatus() {
        _contextStatus = "Right-click for actions";
    }

    private static OverlayHost? FindOverlayHost(Node? node) {
        if (node is null) {
            return null;
        }

        var root = node;
        while (root.Parent is not null) {
            root = root.Parent;
        }

        return Find(root);

        static OverlayHost? Find(Node candidate) {
            if (candidate is OverlayHost host) {
                return host;
            }

            if (candidate is not Element element) {
                return null;
            }

            foreach (var child in element.Children) {
                if (Find(child) is { } found) {
                    return found;
                }
            }

            return null;
        }
    }
}
