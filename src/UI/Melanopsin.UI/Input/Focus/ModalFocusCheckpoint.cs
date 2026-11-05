namespace Melanopsin.UI.Input;

internal sealed record ModalFocusCheckpoint(
    Overlay Modal,
    Node? PreviousFocus);
