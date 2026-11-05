namespace Melanopsin.UI.Input;

public interface ITextInputDeleteHandler {
    bool DeleteSurroundingText(TextInputDeleteSurroundingEvent input);
}
