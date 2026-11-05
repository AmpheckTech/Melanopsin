namespace Melanopsin.UI;

public abstract partial class Element {
    public void AppendContent(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return;
        }

        if (this is ITextContentControl contentControl) {
            contentControl.AppendText(value);
        } else {
            AppendChild(new TextNode(value));
        }
    }

    public void SetTextContent(string? value) {
        value ??= string.Empty;
        if (this is ITextContentControl contentControl) {
            contentControl.Text = value;
            return;
        }

        var children = _children;
        var index = children?.FindIndex(static child => child is TextNode) ?? -1;
        if (index < 0) {
            if (value.Length != 0) {
                InsertChild(0, new TextNode(value));
            }

            return;
        }

        var current = (TextNode)children![index];
        if (string.Equals(current.Text, value, StringComparison.Ordinal)) {
            return;
        }

        if (value.Length == 0) {
            RemoveChild(current);
            return;
        }

        ReplaceChild(current, new TextNode(value));
    }
}
