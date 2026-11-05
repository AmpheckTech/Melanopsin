namespace Melanopsin.UI.Controls;

public readonly record struct GridPlacement(
    int Row,
    int Column,
    int RowSpan = 1,
    int ColumnSpan = 1);
