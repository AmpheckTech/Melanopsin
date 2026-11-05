namespace Melanopsin.Mathematics;

/// <summary>Identifies an implemented sparse iterative linear solver.</summary>
public enum SparseIterativeMethod {
    /// <summary>Jacobi stationary iteration.</summary>
    Jacobi,

    /// <summary>Gauss-Seidel stationary iteration.</summary>
    GaussSeidel,

    /// <summary>Conjugate gradient for symmetric positive-definite systems.</summary>
    ConjugateGradient,

    /// <summary>BiCGSTAB for general nonsymmetric systems.</summary>
    BicgStab,

    /// <summary>Restarted GMRES for general nonsymmetric systems.</summary>
    Gmres,
}

/// <summary>Describes caller-established properties of a sparse linear system.</summary>
public enum SparseIterativeProblemKind {
    /// <summary>The coefficient matrix is symmetric positive definite.</summary>
    SymmetricPositiveDefinite,

    /// <summary>The matrix is square and strongly diagonally dominant.</summary>
    DiagonallyDominant,

    /// <summary>A general square system should be solved with bounded-memory recurrence.</summary>
    GeneralLowMemory,

    /// <summary>A general square system favors the robustness of restarted GMRES.</summary>
    GeneralRobust,
}

/// <summary>Explains why the sparse iterative catalog selected a method.</summary>
public enum SparseIterativeSelectionReason {
    /// <summary>Positive definiteness selected conjugate gradients.</summary>
    SymmetricPositiveDefinite,

    /// <summary>Diagonal dominance selected Gauss-Seidel.</summary>
    DiagonalDominance,

    /// <summary>Bounded-memory general solving selected BiCGSTAB.</summary>
    LowMemoryGeneral,

    /// <summary>General-system robustness selected restarted GMRES.</summary>
    RobustGeneral,

    /// <summary>The caller explicitly selected the method.</summary>
    ExplicitMethod,
}

/// <summary>Describes one sparse iterative solver entry.</summary>
public readonly record struct SparseIterativeSolverDescriptor {
    internal SparseIterativeSolverDescriptor(
        SparseIterativeMethod method,
        string name,
        bool requiresPositiveDefinite,
        bool requiresNonzeroDiagonal,
        bool usesInitialGuess,
        bool usesRestart,
        string workspaceRequirement,
        string numericalCharacteristic) {
        Method = method;
        Name = name;
        RequiresPositiveDefinite = requiresPositiveDefinite;
        RequiresNonzeroDiagonal = requiresNonzeroDiagonal;
        UsesInitialGuess = usesInitialGuess;
        UsesRestart = usesRestart;
        WorkspaceRequirement = workspaceRequirement;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the method identifier.</summary>
    public SparseIterativeMethod Method { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets whether symmetry and positive definiteness are required.</summary>
    public bool RequiresPositiveDefinite { get; }

    /// <summary>Gets whether every row must contain a nonzero diagonal entry.</summary>
    public bool RequiresNonzeroDiagonal { get; }

    /// <summary>Gets whether the low-level API consumes a caller-supplied initial guess.</summary>
    public bool UsesInitialGuess { get; }

    /// <summary>Gets whether the algorithm uses an explicit restart dimension.</summary>
    public bool UsesRestart { get; }

    /// <summary>Gets the caller-owned workspace requirement.</summary>
    public string WorkspaceRequirement { get; }

    /// <summary>Gets the principal convergence, memory, and breakdown trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>
/// An immutable catalog of the implemented sparse iterative solvers.
/// </summary>
/// <remarks>
/// <para>
/// Recommendations depend only on properties explicitly asserted by the
/// caller. No method inspects matrix coefficients, estimates a condition
/// number, or treats an absolute residual as a near-singularity test except
/// where the low-level method documents it.
/// </para>
/// <para>
/// Conjugate gradients is selected for a symmetric positive-definite system.
/// BiCGSTAB is the bounded-memory choice for a general system. Restarted GMRES
/// is the general robust choice but can require workspace quadratic in the
/// system order. Jacobi and Gauss-Seidel are retained when a caller explicitly
/// wants a stationary iteration or diagonal-dominance argument.
/// </para>
/// </remarks>
public static class SparseIterativeSolverCatalog {
    private static readonly SparseIterativeSolverDescriptor[] Entries = [
        new(
            SparseIterativeMethod.ConjugateGradient,
            "Conjugate gradient",
            requiresPositiveDefinite: true,
            requiresNonzeroDiagonal: false,
            usesInitialGuess: false,
            usesRestart: false,
            "At least 3 * order values.",
            "Short recurrence for SPD systems; non-positive curvature is reported as failure."),
        new(
            SparseIterativeMethod.Gmres,
            "Restarted GMRES",
            requiresPositiveDefinite: false,
            requiresNonzeroDiagonal: false,
            usesInitialGuess: false,
            usesRestart: true,
            "(order + 1) * order + (restart + 1) * restart + 3 * restart + 1 values.",
            "Robust general-system convergence with quadratic-order workspace."),
        new(
            SparseIterativeMethod.BicgStab,
            "BiCGSTAB",
            requiresPositiveDefinite: false,
            requiresNonzeroDiagonal: false,
            usesInitialGuess: false,
            usesRestart: false,
            "At least 6 * order values.",
            "Bounded-memory general-system recurrence; exact breakdown can terminate early."),
        new(
            SparseIterativeMethod.GaussSeidel,
            "Gauss-Seidel",
            requiresPositiveDefinite: false,
            requiresNonzeroDiagonal: true,
            usesInitialGuess: true,
            usesRestart: false,
            "No additional workspace in the low-level API.",
            "Sequential updates with simple diagonal-dominance intuition and scalar dependency."),
        new(
            SparseIterativeMethod.Jacobi,
            "Jacobi",
            requiresPositiveDefinite: false,
            requiresNonzeroDiagonal: true,
            usesInitialGuess: true,
            usesRestart: false,
            "At least order values for the next iterate.",
            "Parallel-friendly stationary iteration with slower and less robust convergence."),
    ];

    /// <summary>Gets the built-in sparse iterative solver catalog.</summary>
    public static ReadOnlySpan<SparseIterativeSolverDescriptor> Methods => Entries;

    /// <summary>
    /// Selects a sparse iterative method from an explicitly established
    /// problem class.
    /// </summary>
    public static SparseIterativeMethod Recommend(
        SparseIterativeProblemKind problemKind,
        out SparseIterativeSelectionReason reason) {
        switch (problemKind) {
            case SparseIterativeProblemKind.SymmetricPositiveDefinite:
                reason = SparseIterativeSelectionReason.SymmetricPositiveDefinite;
                return SparseIterativeMethod.ConjugateGradient;
            case SparseIterativeProblemKind.DiagonallyDominant:
                reason = SparseIterativeSelectionReason.DiagonalDominance;
                return SparseIterativeMethod.GaussSeidel;
            case SparseIterativeProblemKind.GeneralLowMemory:
                reason = SparseIterativeSelectionReason.LowMemoryGeneral;
                return SparseIterativeMethod.BicgStab;
            case SparseIterativeProblemKind.GeneralRobust:
                reason = SparseIterativeSelectionReason.RobustGeneral;
                return SparseIterativeMethod.Gmres;
            default:
                throw new ArgumentOutOfRangeException(nameof(problemKind));
        }
    }
}
