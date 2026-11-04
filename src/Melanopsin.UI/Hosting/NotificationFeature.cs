namespace Melanopsin.UI.Hosting;

[Flags]
public enum NotificationFeature {
    None = 0,
    Icon = 1 << 0,
    Actions = 1 << 1,
    Progress = 1 << 2,
    Dismiss = 1 << 3,
    Replacement = 1 << 4,
    Expiry = 1 << 5,
    Body = 1 << 6,
    Sound = 1 << 7,
    CustomContent = 1 << 8,
}
