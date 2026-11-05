namespace Melanopsin.Mathematics;

/// <summary>
/// A normal distribution conditioned on a finite or unbounded interval.
/// </summary>
/// <remarks>
/// The distribution uses the open statistical support
/// <c>LowerBound &lt; X &lt; UpperBound</c>, with explicitly infinite bounds
/// permitted. Tail probabilities are evaluated in log space so that extreme
/// truncation does not require forming <c>1 - CDF</c>. The reported mean and
/// variance are the moments of the truncated distribution, not the moments of
/// the underlying normal distribution.
/// </remarks>
public readonly record struct TruncatedNormalDistribution :
    IProbabilityDistribution {
    private const double LogSqrt2Pi =
        0.91893853320467274178032973640561763986139747363778;
    private const double LogTwo = 0.693147180559945309417232121458176568;

    public TruncatedNormalDistribution(
        double location,
        double scale,
        double lowerBound = double.NegativeInfinity,
        double upperBound = double.PositiveInfinity) {
        DistributionValidation.Location(location, nameof(location));
        DistributionValidation.Scale(scale, nameof(scale));
        if (double.IsNaN(lowerBound) || double.IsNaN(upperBound) ||
            lowerBound >= upperBound) {
            throw new ArgumentException(
                "The lower bound must be strictly less than the upper bound.",
                nameof(lowerBound));
        }

        Location = location;
        Scale = scale;
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public double Location { get; }

    public double Scale { get; }

    public double LowerBound { get; }

    public double UpperBound { get; }

    public double Alpha => (LowerBound - Location) / Scale;

    public double Beta => (UpperBound - Location) / Scale;

    public double Mean {
        get {
            var firstMoment = StandardFirstMoment();
            return Location + (Scale * firstMoment);
        }
    }

    public double Variance {
        get {
            var logMass = LogStandardInterval(Alpha, Beta);
            if (double.IsNegativeInfinity(logMass)) {
                return double.NaN;
            }

            var firstMoment = StandardFirstMoment();
            var scaledDensityTerm =
                ScaledDensityMoment(Alpha, logMass) -
                ScaledDensityMoment(Beta, logMass);
            var variance = 1 + scaledDensityTerm - (firstMoment * firstMoment);
            return Math.Clamp(variance, 0, 1) * Scale * Scale;
        }
    }

    public double Pdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < LowerBound || value > UpperBound) {
            return 0;
        }

        return Math.Exp(LogPdf(value));
    }

    /// <summary>
    /// Computes the natural logarithm of the truncated density.
    /// </summary>
    public double LogPdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value < LowerBound || value > UpperBound) {
            return double.NegativeInfinity;
        }

        var standardized = (value - Location) / Scale;
        var logMass = LogStandardInterval(Alpha, Beta);
        return LogStandardNormalDensity(standardized) - logMass - Math.Log(Scale);
    }

    public double Cdf(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= LowerBound) {
            return 0;
        }

        if (value >= UpperBound) {
            return 1;
        }

        var alpha = Alpha;
        var beta = Beta;
        var standardized = (value - Location) / Scale;
        var logMass = LogStandardInterval(alpha, beta);
        var logLower = LogStandardInterval(alpha, standardized);
        var logUpper = LogStandardInterval(standardized, beta);
        return ProbabilityFromPartition(logLower, logUpper, logMass);
    }

    public double Survival(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value <= LowerBound) {
            return 1;
        }

        if (value >= UpperBound) {
            return 0;
        }

        var alpha = Alpha;
        var beta = Beta;
        var standardized = (value - Location) / Scale;
        var logMass = LogStandardInterval(alpha, beta);
        var logLower = LogStandardInterval(alpha, standardized);
        var logUpper = LogStandardInterval(standardized, beta);
        return ProbabilityFromPartition(logUpper, logLower, logMass);
    }

    public double Quantile(double probability) {
        DistributionValidation.Probability(probability);
        if (probability == 0) {
            return LowerBound;
        }

        if (probability == 1) {
            return UpperBound;
        }

        var alpha = Alpha;
        var beta = Beta;
        var logMass = LogStandardInterval(alpha, beta);
        double standardized;
        if (alpha >= 0) {
            var logSurvival = UpperTailMixtureLogProbability(
                alpha,
                beta,
                probability);
            standardized = SpecialFunctions.InverseNormalLogSurvival(
                logSurvival);
        } else if (beta <= 0) {
            var logTarget = LogAdd(
                SpecialFunctions.NormalLogCdf(alpha),
                Math.Log(probability) + logMass);
            standardized = SpecialFunctions.InverseNormalLogCdf(logTarget);
        } else if (probability <= 0.5) {
            var logTarget = LogAdd(
                SpecialFunctions.NormalLogCdf(alpha),
                Math.Log(probability) + logMass);
            standardized = SpecialFunctions.InverseNormalLogCdf(logTarget);
        } else {
            var logTarget = UpperTailMixtureLogProbability(
                alpha,
                beta,
                probability);
            standardized = SpecialFunctions.InverseNormalLogSurvival(
                logTarget);
        }

        return Math.Clamp(
            Location + (Scale * standardized),
            LowerBound,
            UpperBound);
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Quantile(generator.NextDouble());
    }

    private double StandardFirstMoment() {
        var alpha = Alpha;
        var beta = Beta;
        var logMass = LogStandardInterval(alpha, beta);
        if (double.IsNegativeInfinity(logMass)) {
            return double.NaN;
        }

        var logNumerator = LogAbsDifference(
            LogStandardNormalDensity(alpha),
            LogStandardNormalDensity(beta));
        if (double.IsNegativeInfinity(logNumerator)) {
            return 0;
        }

        var magnitude = Math.Exp(logNumerator - logMass);
        return LogStandardNormalDensity(alpha) >=
            LogStandardNormalDensity(beta)
            ? magnitude
            : -magnitude;
    }

    private static double ScaledDensityMoment(double value, double logMass) {
        if (value == 0 || !double.IsFinite(value)) {
            return 0;
        }

        var logMagnitude = Math.Log(Math.Abs(value)) +
            LogStandardNormalDensity(value) -
            logMass;
        var magnitude = Math.Exp(logMagnitude);
        return value > 0 ? magnitude : -magnitude;
    }

    private static double ProbabilityFromPartition(
        double logPart,
        double logComplement,
        double logTotal) {
        if (double.IsNegativeInfinity(logPart)) {
            return 0;
        }

        if (double.IsNegativeInfinity(logComplement)) {
            return 1;
        }

        var logPartRatio = logPart - logTotal;
        if (logPartRatio <= -LogTwo) {
            return Math.Exp(logPartRatio);
        }

        return -FloatingPointMath.Expm1(logComplement - logTotal);
    }

    private static double LogStandardInterval(double lower, double upper) {
        if (lower >= 0) {
            return LogDifferenceOfLogs(
                SpecialFunctions.NormalLogSurvival(lower),
                SpecialFunctions.NormalLogSurvival(upper));
        }

        if (upper <= 0) {
            return LogDifferenceOfLogs(
                SpecialFunctions.NormalLogCdf(lower),
                SpecialFunctions.NormalLogCdf(upper));
        }

        return LogDifferenceOfLogs(
            SpecialFunctions.NormalLogCdf(lower),
            SpecialFunctions.NormalLogCdf(upper));
    }

    private static double LogStandardNormalDensity(double value) {
        return -0.5 * value * value - LogSqrt2Pi;
    }

    private static double LogDifferenceOfLogs(double lower, double upper) {
        if (double.IsNegativeInfinity(lower) &&
            double.IsNegativeInfinity(upper)) {
            return double.NegativeInfinity;
        }

        var maximum = Math.Max(lower, upper);
        var minimum = Math.Min(lower, upper);
        if (double.IsNegativeInfinity(minimum)) {
            return maximum;
        }

        return maximum + FloatingPointMath.Log1p(-Math.Exp(minimum - maximum));
    }

    private static double LogAbsDifference(double first, double second) {
        if (first == second) {
            return double.NegativeInfinity;
        }

        var maximum = Math.Max(first, second);
        var minimum = Math.Min(first, second);
        return maximum + FloatingPointMath.Log1p(-Math.Exp(minimum - maximum));
    }

    private static double LogAdd(double first, double second) {
        if (double.IsNegativeInfinity(first)) {
            return second;
        }

        if (double.IsNegativeInfinity(second)) {
            return first;
        }

        var maximum = Math.Max(first, second);
        var minimum = Math.Min(first, second);
        return maximum + FloatingPointMath.Log1p(Math.Exp(minimum - maximum));
    }

    private static double UpperTailMixtureLogProbability(
        double alpha,
        double beta,
        double probability) {
        // S(X) = (1 - p) S(alpha) + p S(beta), where X is the requested
        // truncated quantile. Keeping both terms avoids cancellation and
        // remains valid when the interval lies entirely in one tail.
        return LogAdd(
            SpecialFunctions.NormalLogSurvival(alpha) +
                FloatingPointMath.Log1p(-probability),
            SpecialFunctions.NormalLogSurvival(beta) +
                Math.Log(probability));
    }
}
