namespace Melanopsin.Mathematics;

/// <summary>
/// Selects the populated triangle of a triangular matrix.
/// </summary>
public enum MatrixTriangle {
    /// <summary>
    /// Entries on and below the main diagonal are used.
    /// </summary>
    Lower,

    /// <summary>
    /// Entries on and above the main diagonal are used.
    /// </summary>
    Upper,
}
