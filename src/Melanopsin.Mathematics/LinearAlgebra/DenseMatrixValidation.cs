namespace Melanopsin.Mathematics;

internal static class DenseMatrixValidation {
    public static void Validate(int rows, int columns) {
        if (rows <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(rows),
                rows,
                "The row count must be positive.");
        }

        if (columns <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                columns,
                "The column count must be positive.");
        }
    }
}
