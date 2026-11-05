namespace Melanopsin.UI;

public interface ITextContentControl {
    string Text { get; set; }

    void AppendText(string value);
}
