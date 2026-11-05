// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Controls;

internal readonly record struct GridChildPlacement(
    int Row,
    int Column,
    int RowSpan,
    int ColumnSpan);
