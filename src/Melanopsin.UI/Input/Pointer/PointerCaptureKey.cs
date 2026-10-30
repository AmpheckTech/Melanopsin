namespace Melanopsin.UI.Input;

internal readonly record struct PointerCaptureKey(
    WindowId WindowId,
    InputDeviceKind Device,
    PointerId PointerId);
