namespace Melanopsin.Mathematics;

/// <summary>
/// A Dirichlet distribution over probability vectors of a fixed dimension.
/// </summary>
/// <remarks>
/// Every concentration parameter is finite and strictly positive. Density
/// arguments are probabilities: each component is non-negative and their sum
/// is one. A relative support tolerance is accepted explicitly by the density
/// methods to account for floating-point summation.
/// </remarks>
public sealed class DirichletDistribution {
    private readonly double[] _concentrations;
    private readonly double[] _mean;
    private readonly double _totalConcentration;
    private readonly double _logNormalization;

    public DirichletDistribution(ReadOnlySpan<double> concentrations) {
        if (concentrations.Length == 0) {
            throw new ArgumentException(
                "At least one concentration parameter is required.",
                nameof(concentrations));
        }

        _concentrations = new double[concentrations.Length];
        _mean = new double[concentrations.Length];
        var total = 0.0;
        var logBeta = 0.0;
        for (var index = 0; index < concentrations.Length; index++) {
            var value = concentrations[index];
            DistributionValidation.Scale(value, nameof(concentrations));
            _concentrations[index] = value;
            total += value;
            logBeta += SpecialFunctions.LogGamma(value);
        }

        if (!double.IsFinite(total)) {
            throw new ArgumentOutOfRangeException(
                nameof(concentrations),
                "The concentration sum must be finite.");
        }

        _totalConcentration = total;
        _logNormalization = SpecialFunctions.LogGamma(total) - logBeta;
        for (var index = 0; index < _concentrations.Length; index++) {
            _mean[index] = _concentrations[index] / total;
        }
    }

    public int Dimension => _concentrations.Length;

    public ReadOnlySpan<double> Concentrations => _concentrations;

    public ReadOnlySpan<double> Mean => _mean;

    /// <summary>
    /// Computes the entropy of the distribution in natural units.
    /// </summary>
    public double Entropy {
        get {
            var result = _logNormalization +
                ((_totalConcentration - Dimension) *
                    SpecialFunctions.Digamma(_totalConcentration));
            for (var index = 0; index < _concentrations.Length; index++) {
                result -= (_concentrations[index] - 1) *
                    SpecialFunctions.Digamma(_concentrations[index]);
            }

            return result;
        }
    }

    /// <summary>
    /// Computes the probability density at a point on the simplex.
    /// </summary>
    public double Pdf(
        ReadOnlySpan<double> probabilities,
        double simplexTolerance = 1e-12) {
        return Math.Exp(LogPdf(probabilities, simplexTolerance));
    }

    /// <summary>
    /// Computes the log probability density at a point on the simplex.
    /// </summary>
    /// <param name="probabilities">
    /// At least <see cref="Dimension"/> values.
    /// </param>
    /// <param name="simplexTolerance">
    /// Non-negative mixed absolute/relative tolerance used for the unit-sum
    /// check. The scale is <c>max(1, |sum|)</c>, anchored to the fixed target
    /// simplex sum of one rather than to an absolute determinant or epsilon.
    /// </param>
    public double LogPdf(
        ReadOnlySpan<double> probabilities,
        double simplexTolerance = 1e-12) {
        if (probabilities.Length < Dimension) {
            throw new ArgumentException(
                "The probability vector is too small.",
                nameof(probabilities));
        }

        if (!double.IsFinite(simplexTolerance) || simplexTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(simplexTolerance));
        }

        var sum = 0.0;
        for (var index = 0; index < Dimension; index++) {
            var probability = probabilities[index];
            if (!double.IsFinite(probability) || probability < 0) {
                return double.NegativeInfinity;
            }

            sum += probability;
        }

        if (Math.Abs(sum - 1) > simplexTolerance * Math.Max(1, Math.Abs(sum))) {
            return double.NegativeInfinity;
        }

        var result = _logNormalization;
        for (var index = 0; index < Dimension; index++) {
            var probability = probabilities[index];
            var exponent = _concentrations[index] - 1;
            if (probability == 0) {
                if (exponent < 0) {
                    return double.PositiveInfinity;
                }

                if (exponent > 0) {
                    return double.NegativeInfinity;
                }

                continue;
            }

            result += exponent * Math.Log(probability);
        }

        return result;
    }

    /// <summary>
    /// Computes the variance of one component.
    /// </summary>
    public double Variance(int dimension) {
        var concentration = GetConcentration(dimension);
        var denominator = _totalConcentration * _totalConcentration *
            (_totalConcentration + 1);
        return concentration * (_totalConcentration - concentration) / denominator;
    }

    /// <summary>
    /// Computes the covariance of two components.
    /// </summary>
    public double Covariance(int firstDimension, int secondDimension) {
        var first = GetConcentration(firstDimension);
        var second = GetConcentration(secondDimension);
        if (firstDimension == secondDimension) {
            return Variance(firstDimension);
        }

        var denominator = _totalConcentration * _totalConcentration *
            (_totalConcentration + 1);
        return -first * second / denominator;
    }

    /// <summary>
    /// Draws a probability vector. The destination is overwritten. Sampling
    /// uses normalized independent gamma variates and allocates nothing.
    /// </summary>
    public bool TrySample(
        Span<double> destination,
        ref Xoshiro256StarStar generator) {
        return TrySample(destination, ref generator, out _);
    }

    /// <summary>
    /// Draws a probability vector and returns a diagnostic report.
    /// </summary>
    /// <param name="destination">Receives a normalized probability vector.</param>
    /// <param name="generator">The pseudo-random generator state.</param>
    /// <param name="report">Receives the termination status.</param>
    /// <returns><see langword="true"/> when a finite probability vector was produced.</returns>
    public bool TrySample(
        Span<double> destination,
        ref Xoshiro256StarStar generator,
        out DistributionOperationReport report) {
        if (destination.Length < Dimension) {
            throw new ArgumentException(
                "The destination is too small.",
                nameof(destination));
        }

        var sawNonFinite = false;
        for (var attempt = 0; attempt < 8; attempt++) {
            var sum = 0.0;
            for (var index = 0; index < Dimension; index++) {
                var sample = DistributionValidation.SampleStandardGamma(
                    ref generator,
                    _concentrations[index]);
                destination[index] = sample;
                sum += sample;
            }

            if (!double.IsFinite(sum) || sum <= 0) {
                sawNonFinite |= !double.IsFinite(sum);
                continue;
            }

            var inverseSum = 1 / sum;
            for (var index = 0; index < Dimension; index++) {
                destination[index] *= inverseSum;
            }

            for (var index = 0; index < Dimension; index++) {
                if (!double.IsFinite(destination[index])) {
                    destination[..Dimension].Clear();
                    report = new DistributionOperationReport(
                        DistributionOperation.Sampling,
                        DistributionStatus.NonFiniteOutput);
                    return false;
                }
            }

            report = new DistributionOperationReport(
                DistributionOperation.Sampling,
                DistributionStatus.Succeeded);
            return true;
        }

        destination[..Dimension].Clear();
        report = new DistributionOperationReport(
            DistributionOperation.Sampling,
            sawNonFinite
                ? DistributionStatus.NonFiniteOutput
                : DistributionStatus.NumericalFailure);
        return false;
    }

    /// <summary>
    /// Draws a probability vector into a new array.
    /// </summary>
    public double[] Sample(ref Xoshiro256StarStar generator) {
        var result = new double[Dimension];
        if (!TrySample(result, ref generator)) {
            throw new ArithmeticException(
                "The gamma sampler could not produce a finite composition.");
        }

        return result;
    }

    private double GetConcentration(int dimension) {
        if ((uint)dimension >= (uint)Dimension) {
            throw new ArgumentOutOfRangeException(nameof(dimension));
        }

        return _concentrations[dimension];
    }
}
