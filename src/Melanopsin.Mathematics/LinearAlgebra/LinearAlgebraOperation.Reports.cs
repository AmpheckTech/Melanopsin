namespace Melanopsin.Mathematics;

/// <summary>Identifies a real or complex linear-algebra operation that can terminate without a result.</summary>
public enum LinearAlgebraOperation {
    /// <summary>Solution of a factorized real square system.</summary>
    LuSolve,

    /// <summary>Inversion through a real LU factorization.</summary>
    LuInvert,

    /// <summary>Solution of a factorized real symmetric positive-definite system.</summary>
    CholeskySolve,

    /// <summary>Inversion through a real Cholesky factorization.</summary>
    CholeskyInvert,

    /// <summary>Solution of a square real QR system.</summary>
    QrSolve,

    /// <summary>Solution of a real QR least-squares problem.</summary>
    QrLeastSquares,

    /// <summary>Extraction of a factor from a real QR decomposition.</summary>
    QrFactorExtraction,

    /// <summary>Solution of a square real SVD system.</summary>
    SvdSolve,

    /// <summary>Solution of a real SVD least-squares or minimum-norm problem.</summary>
    SvdLeastSquares,

    /// <summary>Construction of a real Moore-Penrose pseudoinverse.</summary>
    SvdPseudoinverse,

    /// <summary>Inversion through a real SVD.</summary>
    SvdInvert,

    /// <summary>Inversion through a real symmetric eigendecomposition.</summary>
    SymmetricEigenInvert,

    /// <summary>Solution of a real triangular system.</summary>
    TriangularSolve,

    /// <summary>Solution of a real tridiagonal system.</summary>
    TridiagonalSolve,

    /// <summary>Solution of a factorized complex square system.</summary>
    ComplexLuSolve,

    /// <summary>Inversion through a complex LU factorization.</summary>
    ComplexLuInvert,

    /// <summary>Solution of a factorized complex Hermitian positive-definite system.</summary>
    ComplexCholeskySolve,

    /// <summary>Inversion through a complex Cholesky factorization.</summary>
    ComplexCholeskyInvert,

    /// <summary>Solution of a square complex QR system.</summary>
    ComplexQrSolve,

    /// <summary>Solution of a complex QR least-squares problem.</summary>
    ComplexQrLeastSquares,

    /// <summary>Extraction of a factor from a complex QR decomposition.</summary>
    ComplexQrFactorExtraction,

    /// <summary>Solution of a square complex SVD system.</summary>
    ComplexSvdSolve,

    /// <summary>Solution of a complex SVD least-squares or minimum-norm problem.</summary>
    ComplexSvdLeastSquares,

    /// <summary>Construction of a complex Moore-Penrose pseudoinverse.</summary>
    ComplexSvdPseudoinverse,

    /// <summary>Inversion through a complex SVD.</summary>
    ComplexSvdInvert,

    /// <summary>Solution of a complex triangular system.</summary>
    ComplexTriangularSolve,

    /// <summary>Normalization of a complex vector.</summary>
    ComplexVectorNormalization,

    /// <summary>Estimation of a matrix spectral condition number.</summary>
    ConditionEstimate,
}

/// <summary>The termination status of a factorization-backed linear-algebra operation.</summary>
public enum LinearAlgebraStatus {
    /// <summary>The operation produced a finite result satisfying its numerical contract.</summary>
    Succeeded,

    /// <summary>A right-hand side or other operation input contains NaN or infinity.</summary>
    NonFiniteInput,

    /// <summary>A direction, vector, or scale required by the operation is exactly zero.</summary>
    DegenerateInput,

    /// <summary>A required pivot, diagonal, singular value, or determinant is exactly zero.</summary>
    Singular,

    /// <summary>The requested full-rank solve encountered rank deficiency at the stated tolerance.</summary>
    RankDeficient,

    /// <summary>A Cholesky factorization found that the source is not positive definite.</summary>
    NotPositiveDefinite,

    /// <summary>A finite input produced a non-finite intermediate or result.</summary>
    NonFiniteOutput,

    /// <summary>The operation failed numerically for a reason not covered by another status.</summary>
    NumericalFailure,
}

/// <summary>
/// A compact diagnostic report for factorization-backed linear-algebra
/// operations.
/// </summary>
/// <remarks>
/// <para>
/// Argument, shape, overlap, and buffer-size violations remain programming
/// errors and throw before an operation starts. Numerical conditions such as a
/// non-finite right-hand side, an exactly singular pivot, a non-positive
/// Cholesky diagonal, rank deficiency at an explicitly supplied relative
/// tolerance, or a non-finite result are reported through
/// <see cref="Status"/>.
/// </para>
/// <para>
/// Singularity means exactly zero in the working precision unless an operation
/// accepts a relative rank tolerance. The library does not classify a finite
/// non-zero pivot, determinant, or singular value as near-singular from an
/// absolute threshold. Accuracy-sensitive callers should use a condition
/// estimate or choose an application-specific relative tolerance.
/// </para>
/// </remarks>
public readonly record struct LinearAlgebraOperationReport {
    internal LinearAlgebraOperationReport(
        LinearAlgebraOperation operation,
        LinearAlgebraStatus status) {
        Operation = operation;
        Status = status;
    }

    /// <summary>Gets the executed operation.</summary>
    public LinearAlgebraOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public LinearAlgebraStatus Status { get; }

    /// <summary>Gets whether the operation produced a usable result.</summary>
    public bool Succeeded => Status == LinearAlgebraStatus.Succeeded;
}
