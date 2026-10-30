namespace Melanopsin.UI;

internal static class ElementMerger {
    public static InvalidationKind ReplaceAttributes(
        Element current,
        Element incoming,
        out LayoutInvalidationReason layoutReason) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(incoming);
        layoutReason = LayoutInvalidationReason.None;
        var invalidation = current.InlineStyle.CopyFrom(incoming.InlineStyle)
            ? InvalidationKind.Layout
            : InvalidationKind.None;
        if (invalidation != InvalidationKind.None) {
            layoutReason = LayoutInvalidationReason.LayoutProperty;
        }

        foreach (var name in current.State.AttributeNames.ToArray()) {
            if (incoming.Attributes.ContainsKey(name)) {
                continue;
            }

            current.State.RemoveAttribute(name);
            var attributeInvalidation =
                current.ApplyReconciledAttribute(name, null);
            invalidation |= attributeInvalidation;
            if ((attributeInvalidation & InvalidationKind.Layout) != 0) {
                layoutReason = LayoutInvalidationReason.LayoutProperty;
            }
        }

        foreach (var attribute in incoming.Attributes) {
            if (!current.State.TrySetAttribute(attribute.Key, attribute.Value)) {
                continue;
            }

            var attributeInvalidation = current.ApplyReconciledAttribute(
                attribute.Key,
                attribute.Value);
            invalidation |= attributeInvalidation;
            if ((attributeInvalidation & InvalidationKind.Layout) != 0) {
                layoutReason = LayoutInvalidationReason.LayoutProperty;
            }
        }

        return invalidation;
    }

    public static void ReplaceText(Element current, Element incoming) {
        if (current is ITextContentControl currentText &&
            incoming is ITextContentControl incomingText &&
            !string.Equals(currentText.Text, incomingText.Text, StringComparison.Ordinal)) {
            currentText.Text = incomingText.Text;
        }
    }

    public static void ReplaceBindings(Element current, Element incoming) {
        current.State.ReplaceBindingsFrom(
            incoming.State,
            current,
            current.IsMounted || current.Parent is not null);
    }
}
