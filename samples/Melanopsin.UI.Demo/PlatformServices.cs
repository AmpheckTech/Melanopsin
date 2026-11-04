using Melanopsin.UI;
using Melanopsin.UI.Hosting;

namespace Melanopsin.UI.Demo;

public sealed record PlatformServices(
    IPlatformClipboard? Clipboard,
    IPlatformNotifications? Notifications);

public static class PlatformServicesContext {
    public static Context<PlatformServices> Value { get; } = new();
}
