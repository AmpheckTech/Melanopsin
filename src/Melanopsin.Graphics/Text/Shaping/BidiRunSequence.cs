// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal sealed class BidiRunSequence {
    public BidiRunSequence(
        int level,
        BidiClass sos,
        BidiClass eos,
        int[] characters) {
        Level = level;
        Sos = sos;
        Eos = eos;
        Characters = characters;
    }

    public int Level { get; }

    public BidiClass Sos { get; }

    public BidiClass Eos { get; }

    public int[] Characters { get; }
}
