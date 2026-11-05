namespace Melanopsin.UI.Hosting;

public readonly record struct NotificationId(long Value) {
    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
