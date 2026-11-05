namespace Melanopsin.UI.Input;

public interface ITextInputSource {
    TextInputState CreateTextInputState(WindowId windowId, Rect bounds);
}
