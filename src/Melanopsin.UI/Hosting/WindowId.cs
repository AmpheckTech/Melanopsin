namespace Melanopsin.UI.Hosting;

public readonly record struct WindowId(long Value) {
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
