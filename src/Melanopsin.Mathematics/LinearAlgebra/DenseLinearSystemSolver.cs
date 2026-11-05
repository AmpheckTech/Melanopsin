// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Structural properties supplied by the caller when selecting a dense linear
/// solver.
/// </summary>
/// <remarks>
/// These flags are assertions about the mathematical problem, not hints that
/// the solver silently verifies by scanning the matrix. Passing an incorrect
/// flag can therefore select an inappropriate algorithm. Properties that
/// cannot be established by the caller should be left as
/// <see cref="None"/>.
/// </remarks>
[Flags]
public enum MatrixStructure {
    /// <summary>No exploitable structure is asserted.</summary>
    None = 0,

    /// <summary>The square coefficient matrix is symmetric.</summary>
    Symmetric = 1,

    /// <summary>
    /// The square coefficient matrix is symmetric positive definite. This flag
    /// should be used together with <see cref="Symmetric"/>.
    /// </summary>
    PositiveDefinite = 2,

    /// <summary>The matrix has been independently established to have full column rank.</summary>
    FullColumnRank = 4,
}

/// <summary>Identifies a dense linear-system algorithm.</summary>
public enum LinearSolverMethod {
    /// <summary>Selects a method from the explicit problem structure.</summary>
    Auto,

    /// <summary>Partial-pivoting LU for square systems.</summary>
    Lu,

    /// <summary>Householder QR for square or overdetermined least-squares systems.</summary>
    Qr,

    /// <summary>Singular value decomposition with an explicit relative rank tolerance.</summary>
    Svd,

    /// <summary>Cholesky factorization for symmetric positive definite systems.</summary>
    Cholesky,
}

/// <summary>Explains why an automatic linear-solver method was selected.</summary>
public enum LinearSolverSelectionReason {
    /// <summary>No automatic selection was made.</summary>
    None,

    /// <summary>A symmetric positive definite square system selected Cholesky.</summary>
    SymmetricPositiveDefinite,

    /// <summary>A general square system selected LU.</summary>
    SquareGeneral,

    /// <summary>An overdetermined system selected QR least squares.</summary>
    OverdeterminedLeastSquares,

    /// <summary>The caller requested SVD explicitly.</summary>
    ExplicitSvd,
}

/// <summary>The termination status of a dense linear-system solve.</summary>
public enum LinearSolveStatus {
    /// <summary>A finite solution satisfying the selected numerical contract was produced.</summary>
    Succeeded,

    /// <summary>Underdetermined systems are not supported by this high-level API.</summary>
    Underdetermined,

    /// <summary>The requested method is incompatible with the matrix shape or asserted structure.</summary>
    Unsupported,

    /// <summary>A matrix, right-hand side, or operation input contains NaN or infinity.</summary>
    NonFiniteInput,

    /// <summary>A required pivot, diagonal, or singular value is exactly zero.</summary>
    Singular,

    /// <summary>
    /// The requested solve is rank deficient at the caller-supplied relative
    /// tolerance.
    /// </summary>
    RankDeficient,

    /// <summary>A Cholesky factorization encountered a non-positive pivot.</summary>
    NotPositiveDefinite,

    /// <summary>A finite input produced a non-finite intermediate or solution.</summary>
    NonFiniteOutput,

    /// <summary>A finite solution was not produced for a reason not covered by another status.</summary>
    NumericalFailure,
}

/// <summary>
/// A small diagnostic report returned by the dense linear-system solver.
/// </summary>
/// <remarks>
/// <see cref="RelativeResidual"/> is the backward-error-style quantity
/// <c>||Ax-b||inf / (||A||inf ||x||inf + ||b||inf)</c>. It is not a
/// condition number and does not estimate solution error. For ill-conditioned
/// systems use <see cref="SvdDecomposition.ConditionNumber"/> or
/// <see cref="DenseLinearSystemSolver.TryEstimateSpectralConditionNumber(ReadOnlySpan{double}, int, int, out double)"/>.
/// </remarks>
public readonly record struct LinearSolveReport {
    internal LinearSolveReport(
        LinearSolveStatus status,
        LinearSolverMethod method,
        LinearSolverSelectionReason selectionReason,
        double relativeResidual,
        int numericalRank) {
        Status = status;
        Method = method;
        SelectionReason = selectionReason;
        RelativeResidual = relativeResidual;
        NumericalRank = numericalRank;
    }

    /// <summary>Gets the termination status.</summary>
    public LinearSolveStatus Status { get; }

    /// <summary>Gets the method that was actually used.</summary>
    public LinearSolverMethod Method { get; }

    /// <summary>Gets the automatic-selection rationale, if applicable.</summary>
    public LinearSolverSelectionReason SelectionReason { get; }

    /// <summary>Gets the dimensionless normalized residual.</summary>
    public double RelativeResidual { get; }

    /// <summary>
    /// Gets the numerical rank using the solve tolerance, or zero when the
    /// selected method does not estimate rank.
    /// </summary>
    public int NumericalRank { get; }

    /// <summary>Gets whether the solve succeeded.</summary>
    public bool Succeeded => Status == LinearSolveStatus.Succeeded;
}

/// <summary>Describes the applicability of a dense linear solver.</summary>
public readonly record struct LinearSolverDescriptor {
    internal LinearSolverDescriptor(
        LinearSolverMethod method,
        string name,
        string requirements,
        string numericalCharacteristic) {
        Method = method;
        Name = name;
        Requirements = requirements;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the solver method.</summary>
    public LinearSolverMethod Method { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets the shape and structure requirements.</summary>
    public string Requirements { get; }

    /// <summary>Gets the principal numerical trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>
/// Explicit selection and execution of dense real linear-system solvers.
/// </summary>
/// <remarks>
/// <para>
/// The low-level decomposition types remain the primary allocation-explicit
/// API. This type adds a deterministic method catalog, an automatic selector
/// based on caller-asserted structure, and one report contract. It never uses
/// a determinant or an absolute near-singularity threshold. Exact singularity
/// is reported by LU, QR, or Cholesky. SVD accepts an explicit threshold
/// relative to the largest singular value and returns a minimum-norm
/// least-squares solution when rank deficient.
/// </para>
/// <para>
/// Auto selection uses LU for general square systems, Cholesky for asserted
/// symmetric positive definite systems, and QR for overdetermined systems.
/// Underdetermined systems are rejected rather than silently choosing a
/// minimum-norm solution. Request SVD explicitly for that behavior.
/// </para>
/// </remarks>
public static class DenseLinearSystemSolver {
    private static readonly LinearSolverDescriptor[] CatalogEntries = [
        new(
            LinearSolverMethod.Lu,
            "LU with partial pivoting",
            "Square matrix.",
            "O(n^3) work; exact zero pivots report singularity."),
        new(
            LinearSolverMethod.Cholesky,
            "Cholesky",
            "Square symmetric positive definite matrix.",
            "Half the factorization work of LU; exact non-positive pivots report failure."),
        new(
            LinearSolverMethod.Qr,
            "Householder QR",
            "Square or overdetermined matrix with full column rank.",
            "Stable least squares without forming normal equations."),
        new(
            LinearSolverMethod.Svd,
            "Singular value decomposition",
            "Any non-empty matrix; solve uses an explicit relative rank tolerance.",
            "Highest diagnostic value and cost; supports rank-deficient minimum-norm solves."),
    ];

    /// <summary>Gets the built-in dense solver catalog.</summary>
    public static ReadOnlySpan<LinearSolverDescriptor> Catalog => CatalogEntries;

    /// <summary>
    /// Selects a method from explicit structural information.
    /// </summary>
    /// <param name="rows">The number of matrix rows.</param>
    /// <param name="columns">The number of matrix columns.</param>
    /// <param name="structure">Caller-asserted matrix structure.</param>
    /// <param name="reason">Receives the selection rationale.</param>
    /// <returns>The selected method, or <see cref="LinearSolverMethod.Svd"/> for underdetermined systems.</returns>
    public static LinearSolverMethod Recommend(
        int rows,
        int columns,
        MatrixStructure structure,
        out LinearSolverSelectionReason reason) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        if (rows < columns) {
            reason = LinearSolverSelectionReason.None;
            return LinearSolverMethod.Svd;
        }

        if (rows == columns &&
            (structure & (MatrixStructure.Symmetric | MatrixStructure.PositiveDefinite)) ==
                (MatrixStructure.Symmetric | MatrixStructure.PositiveDefinite)) {
            reason = LinearSolverSelectionReason.SymmetricPositiveDefinite;
            return LinearSolverMethod.Cholesky;
        }

        if (rows == columns) {
            reason = LinearSolverSelectionReason.SquareGeneral;
            return LinearSolverMethod.Lu;
        }

        reason = LinearSolverSelectionReason.OverdeterminedLeastSquares;
        return LinearSolverMethod.Qr;
    }

    /// <summary>
    /// Solves a dense system into a caller-provided destination.
    /// </summary>
    /// <param name="matrix">At least <c>rows * columns</c> row-major values.</param>
    /// <param name="rows">The number of matrix rows.</param>
    /// <param name="columns">The number of matrix columns.</param>
    /// <param name="rightHandSide">At least <paramref name="rows"/> values.</param>
    /// <param name="solution">At least <paramref name="columns"/> values.</param>
    /// <param name="structure">Caller-asserted matrix structure.</param>
    /// <param name="report">Receives method, status, rank, and residual diagnostics.</param>
    /// <param name="method">The requested method, or automatic selection.</param>
    /// <param name="relativeRankTolerance">
    /// The relative singular-value threshold used only by SVD. Zero means exact
    /// nonzero. The value must be finite and non-negative.
    /// </param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    /// <exception cref="ArgumentException">
    /// A buffer is too short or overlaps the solution destination.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Dimensions or the relative rank tolerance are invalid.
    /// </exception>
    public static bool TrySolve(
        ReadOnlySpan<double> matrix,
        int rows,
        int columns,
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        MatrixStructure structure,
        out LinearSolveReport report,
        LinearSolverMethod method = LinearSolverMethod.Auto,
        double relativeRankTolerance = 0) {
        Validate(
            matrix,
            rows,
            columns,
            rightHandSide,
            solution,
            relativeRankTolerance);
        if (!AreFinite(matrix, checked(rows * columns)) ||
            !AreFinite(rightHandSide, rows)) {
            solution[..columns].Clear();
            report = Failure(
                LinearSolveStatus.NonFiniteInput,
                method,
                LinearSolverSelectionReason.None);
            return false;
        }

        LinearSolverSelectionReason reason;
        if (method == LinearSolverMethod.Auto) {
            method = Recommend(rows, columns, structure, out reason);
            if (rows < columns) {
                solution[..columns].Clear();
                report = Failure(
                    LinearSolveStatus.Underdetermined,
                    method,
                    reason);
                return false;
            }
        } else {
            reason = method == LinearSolverMethod.Svd
                ? LinearSolverSelectionReason.ExplicitSvd
                : LinearSolverSelectionReason.None;
        }

        if (!IsCompatible(method, rows, columns, structure)) {
            solution[..columns].Clear();
            report = Failure(
                LinearSolveStatus.Unsupported,
                method,
                reason);
            return false;
        }

        var status = LinearSolveStatus.NumericalFailure;
        var numericalRank = 0;
        var succeeded = method switch {
            LinearSolverMethod.Lu => TrySolveLu(
                matrix,
                rows,
                rightHandSide,
                solution,
                out status,
                out numericalRank),
            LinearSolverMethod.Cholesky => TrySolveCholesky(
                matrix,
                rows,
                rightHandSide,
                solution,
                out status,
                out numericalRank),
            LinearSolverMethod.Qr => TrySolveQr(
                matrix,
                rows,
                columns,
                rightHandSide,
                solution,
                out status,
                out numericalRank),
            LinearSolverMethod.Svd => TrySolveSvd(
                matrix,
                rows,
                columns,
                rightHandSide,
                solution,
                relativeRankTolerance,
                out status,
                out numericalRank),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

        if (!succeeded) {
            report = Failure(status, method, reason);
            return false;
        }

        var relativeResidual = ComputeRelativeResidual(
            matrix,
            rows,
            columns,
            rightHandSide,
            solution);
        if (!double.IsFinite(relativeResidual)) {
            solution[..columns].Clear();
            report = Failure(
                LinearSolveStatus.NonFiniteOutput,
                method,
                reason);
            return false;
        }

        report = new LinearSolveReport(
            LinearSolveStatus.Succeeded,
            method,
            reason,
            relativeResidual,
            numericalRank);
        return true;
    }

    /// <summary>
    /// Estimates the spectral 2-norm condition number through an SVD.
    /// </summary>
    /// <remarks>
    /// This is a relative, scale-invariant diagnostic. A zero smallest singular
    /// value produces positive infinity. The computation is intentionally
    /// separate from <see cref="TrySolve"/> so that its O(n^3) cost is never
    /// hidden inside a solve.
    /// </remarks>
    public static bool TryEstimateSpectralConditionNumber(
        ReadOnlySpan<double> matrix,
        int rows,
        int columns,
        out double conditionNumber) {
        return TryEstimateSpectralConditionNumber(
            matrix,
            rows,
            columns,
            out conditionNumber,
            out _);
    }

    /// <summary>
    /// Estimates the spectral 2-norm condition number and reports non-finite
    /// input or a failed decomposition.
    /// </summary>
    /// <remarks>
    /// A mathematically singular matrix has condition number positive
    /// infinity, which is a successful diagnostic result rather than a failure.
    /// Only NaN from the decomposition denotes failure.
    /// </remarks>
    public static bool TryEstimateSpectralConditionNumber(
        ReadOnlySpan<double> matrix,
        int rows,
        int columns,
        out double conditionNumber,
        out LinearAlgebraOperationReport report) {
        DenseMatrixValidation.Validate(rows, columns);
        var elementCount = checked(rows * columns);
        if (matrix.Length < elementCount) {
            throw new ArgumentException(
                "The matrix does not contain enough values.",
                nameof(matrix));
        }

        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(matrix[index])) {
                conditionNumber = double.NaN;
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ConditionEstimate,
                    LinearAlgebraStatus.NonFiniteInput);
                return false;
            }
        }

        try {
            var decomposition = new SvdDecomposition(matrix, rows, columns);
            conditionNumber = decomposition.ConditionNumber;
            if (double.IsNaN(conditionNumber)) {
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.ConditionEstimate,
                    LinearAlgebraStatus.NonFiniteOutput);
                return false;
            }

            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ConditionEstimate,
                LinearAlgebraStatus.Succeeded);
            return true;
        } catch (ArithmeticException) {
            conditionNumber = double.NaN;
            report = new LinearAlgebraOperationReport(
                LinearAlgebraOperation.ConditionEstimate,
                LinearAlgebraStatus.NumericalFailure);
            return false;
        }
    }

    private static bool TrySolveLu(
        ReadOnlySpan<double> matrix,
        int order,
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        out LinearSolveStatus status,
        out int numericalRank) {
        try {
            var decomposition = new LuDecomposition(matrix, order);
            var succeeded = decomposition.TrySolve(
                rightHandSide,
                solution,
                out var report);
            status = MapStatus(report.Status);
            numericalRank = succeeded ? order : 0;
            return succeeded;
        } catch (ArithmeticException) {
            solution[..order].Clear();
            status = LinearSolveStatus.NumericalFailure;
            numericalRank = 0;
            return false;
        }
    }

    private static bool TrySolveCholesky(
        ReadOnlySpan<double> matrix,
        int order,
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        out LinearSolveStatus status,
        out int numericalRank) {
        try {
            var decomposition = new CholeskyDecomposition(matrix, order);
            var succeeded = decomposition.TrySolve(
                rightHandSide,
                solution,
                out var report);
            status = MapStatus(report.Status);
            numericalRank = succeeded ? order : 0;
            return succeeded;
        } catch (ArithmeticException) {
            solution[..order].Clear();
            status = LinearSolveStatus.NumericalFailure;
            numericalRank = 0;
            return false;
        }
    }

    private static bool TrySolveQr(
        ReadOnlySpan<double> matrix,
        int rows,
        int columns,
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        out LinearSolveStatus status,
        out int numericalRank) {
        try {
            var decomposition = new QrDecomposition(matrix, rows, columns);
            var workspace = new double[rows];
            var succeeded = decomposition.TryLeastSquares(
                rightHandSide,
                solution,
                workspace,
                out var report);
            status = MapStatus(report.Status);
            numericalRank = succeeded ? columns : 0;
            return succeeded;
        } catch (ArithmeticException) {
            solution[..columns].Clear();
            status = LinearSolveStatus.NumericalFailure;
            numericalRank = 0;
            return false;
        }
    }

    private static bool TrySolveSvd(
        ReadOnlySpan<double> matrix,
        int rows,
        int columns,
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        double relativeRankTolerance,
        out LinearSolveStatus status,
        out int numericalRank) {
        try {
            var decomposition = new SvdDecomposition(matrix, rows, columns);
            numericalRank = decomposition.CountRank(relativeRankTolerance);
            var succeeded = decomposition.TryLeastSquares(
                rightHandSide,
                solution,
                out var report,
                relativeRankTolerance);
            status = MapStatus(report.Status);
            if (!succeeded) {
                numericalRank = 0;
            }

            return succeeded;
        } catch (ArithmeticException) {
            solution[..columns].Clear();
            status = LinearSolveStatus.NumericalFailure;
            numericalRank = 0;
            return false;
        }
    }

    private static LinearSolveStatus MapStatus(LinearAlgebraStatus status) {
        return status switch {
            LinearAlgebraStatus.Succeeded => LinearSolveStatus.Succeeded,
            LinearAlgebraStatus.NonFiniteInput => LinearSolveStatus.NonFiniteInput,
            LinearAlgebraStatus.DegenerateInput => LinearSolveStatus.Singular,
            LinearAlgebraStatus.Singular => LinearSolveStatus.Singular,
            LinearAlgebraStatus.RankDeficient => LinearSolveStatus.RankDeficient,
            LinearAlgebraStatus.NotPositiveDefinite =>
                LinearSolveStatus.NotPositiveDefinite,
            LinearAlgebraStatus.NonFiniteOutput => LinearSolveStatus.NonFiniteOutput,
            LinearAlgebraStatus.NumericalFailure => LinearSolveStatus.NumericalFailure,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown linear algebra status."),
        };
    }

    private static bool AreFinite(ReadOnlySpan<double> values, int count) {
        var effectiveCount = Math.Min(values.Length, count);
        for (var index = 0; index < effectiveCount; index++) {
            if (!double.IsFinite(values[index])) {
                return false;
            }
        }

        return true;
    }

    private static bool IsCompatible(
        LinearSolverMethod method,
        int rows,
        int columns,
        MatrixStructure structure) {
        return method switch {
            LinearSolverMethod.Lu => rows == columns,
            LinearSolverMethod.Cholesky =>
                rows == columns &&
                (structure & (MatrixStructure.Symmetric | MatrixStructure.PositiveDefinite)) ==
                    (MatrixStructure.Symmetric | MatrixStructure.PositiveDefinite),
            LinearSolverMethod.Qr => rows >= columns,
            LinearSolverMethod.Svd => true,
            _ => false,
        };
    }

    private static double ComputeRelativeResidual(
        ReadOnlySpan<double> matrix,
        int rows,
        int columns,
        ReadOnlySpan<double> rightHandSide,
        ReadOnlySpan<double> solution) {
        var maxResidual = 0.0;
        var matrixInfinityNorm = 0.0;
        for (var row = 0; row < rows; row++) {
            var rowSum = 0.0;
            var product = 0.0;
            var rowOffset = row * columns;
            for (var column = 0; column < columns; column++) {
                var value = matrix[rowOffset + column];
                rowSum += Math.Abs(value);
                product += value * solution[column];
            }

            matrixInfinityNorm = Math.Max(matrixInfinityNorm, rowSum);
            maxResidual = Math.Max(
                maxResidual,
                Math.Abs(product - rightHandSide[row]));
        }

        var solutionInfinityNorm = 0.0;
        for (var column = 0; column < columns; column++) {
            solutionInfinityNorm = Math.Max(
                solutionInfinityNorm,
                Math.Abs(solution[column]));
        }

        var rightHandSideInfinityNorm = 0.0;
        for (var row = 0; row < rows; row++) {
            rightHandSideInfinityNorm = Math.Max(
                rightHandSideInfinityNorm,
                Math.Abs(rightHandSide[row]));
        }

        var scale = (matrixInfinityNorm * solutionInfinityNorm) +
            rightHandSideInfinityNorm;
        return scale == 0
            ? maxResidual == 0 ? 0 : double.PositiveInfinity
            : maxResidual / scale;
    }

    private static LinearSolveReport Failure(
        LinearSolveStatus status,
        LinearSolverMethod method,
        LinearSolverSelectionReason reason) {
        return new LinearSolveReport(
            status,
            method,
            reason,
            double.NaN,
            0);
    }

    private static void Validate(
        ReadOnlySpan<double> matrix,
        int rows,
        int columns,
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        double relativeRankTolerance) {
        DenseMatrixValidation.Validate(rows, columns);
        var elementCount = checked(rows * columns);
        if (matrix.Length < elementCount) {
            throw new ArgumentException(
                "The matrix does not contain enough values.",
                nameof(matrix));
        }

        if (rightHandSide.Length < rows) {
            throw new ArgumentException(
                "The right-hand side does not contain enough values.",
                nameof(rightHandSide));
        }

        if (solution.Length < columns) {
            throw new ArgumentException(
                "The solution does not contain enough values.",
                nameof(solution));
        }

        if (rightHandSide.Overlaps(solution) || matrix.Overlaps(solution)) {
            throw new ArgumentException(
                "The solution must not overlap an input buffer.",
                nameof(solution));
        }

        if (!double.IsFinite(relativeRankTolerance) ||
            relativeRankTolerance < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(relativeRankTolerance));
        }

    }
}
