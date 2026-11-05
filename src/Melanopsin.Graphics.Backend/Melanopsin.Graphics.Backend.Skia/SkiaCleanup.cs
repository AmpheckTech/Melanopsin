// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.ExceptionServices;

namespace Melanopsin.Graphics.Backend.Skia;

internal static class SkiaCleanup {
    public static void Capture(
        ref Exception? failure,
        Action action) {
        try {
            action();
        } catch (Exception exception) {
            failure = Combine(failure, exception);
        }
    }

    public static void ThrowIfFailed(Exception? failure) {
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static Exception Combine(
        Exception? first,
        Exception second) {
        if (first is null) {
            return second;
        }

        return first is AggregateException aggregate
            ? new AggregateException(
                aggregate.InnerExceptions.Append(second))
            : new AggregateException(first, second);
    }
}
