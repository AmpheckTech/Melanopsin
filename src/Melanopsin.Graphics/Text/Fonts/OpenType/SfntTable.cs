// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text.Fonts.OpenType;

internal readonly record struct SfntTable(string Tag, int Offset, int Length);
