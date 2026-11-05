namespace Melanopsin.UI.Controls;

internal readonly record struct GridChildPlacement(
    int Row,
    int Column,
    int RowSpan,
    int ColumnSpan);
