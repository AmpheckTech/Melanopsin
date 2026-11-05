namespace Melanopsin.Mathematics;

/// <summary>
/// A log-normal distribution whose logarithm has the supplied normal
/// parameters.
/// </summary>
public readonly record struct LogNormalDistribution : IProbabilityDistribution {
    private readonly NormalDistribution _normal;

    public LogNormalDistribution(double logMean, double logStandardDeviation) {
        _normal = new NormalDistribution(logMean, logStandardDeviation);
    }

    public double LogMean => _normal.Mean;

    public double LogStandardDeviation => _normal.StandardDeviation;

    public double Mean => Math.Exp(
        LogMean + (0.5 * LogStandardDeviation * LogStandardDeviation));

    public double Variance {
        get {
            var squared = LogStandardDeviation * LogStandardDeviation;
            return FloatingPointMath.Expm1(squared) *
                Math.Exp((2 * LogMean) + squared);
        }
    }

    public double Pdf(double value) {
        if (value <= 0 || !double.IsFinite(value)) {
            return 0;
        }

        var logarithm = Math.Log(value);
        return _normal.Pdf(logarithm) / value;
    }

    public double Cdf(double value) {
        return value <= 0 ? 0 : _normal.Cdf(Math.Log(value));
    }

    public double Quantile(double probability) {
        return Math.Exp(_normal.Quantile(probability));
    }

    public double Sample(ref Xoshiro256StarStar generator) {
        return Math.Exp(_normal.Sample(ref generator));
    }
}
