namespace Scada.Core.Intelligence.Experimental;

// Candidate calculation only. No runtime/API registration or field validation is implied.
// Inputs must already be contextual residuals from a separately fitted, fixed context model.
internal sealed class InnovationReference
{
    private readonly double[] centers;
    private readonly double[] scales;
    private readonly double[] ar;
    private readonly double[] innovationCenters;
    private readonly double[][] lower;

    private InnovationReference(string binding, double[] centers, double[] scales, double[] ar,
        double[] innovationCenters, double[][] lower)
    {
        Binding = binding;
        this.centers = centers;
        this.scales = scales;
        this.ar = ar;
        this.innovationCenters = innovationCenters;
        this.lower = lower;
    }

    public string Binding { get; }
    public int Dimensions => centers.Length;
    public InnovationReferenceData Describe() => new(Binding, centers.ToArray(), scales.ToArray(), ar.ToArray(),
        innovationCenters.ToArray(), lower.Select(row => row.ToArray()).ToArray());

    public static InnovationReference Restore(InnovationReferenceData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Binding);
        var p = data.Centers.Length;
        if (p is < 2 or > 8) throw new ArgumentException("Incompatible reference shape.");
        foreach (var values in new[] { data.Centers, data.Scales, data.Ar, data.InnovationCenters }) RequireVector(values, p);
        if (data.Scales.Any(value => value <= 0) || data.Ar.Any(value => Math.Abs(value) >= .98) || data.Lower.Length != p)
            throw new ArgumentException("Invalid reference scale, AR or covariance shape.");
        for (var i = 0; i < p; i++)
        {
            RequireVector(data.Lower[i], p);
            if (data.Lower[i][i] <= 0 || Enumerable.Range(i + 1, p - i - 1).Any(j => data.Lower[i][j] != 0))
                throw new ArgumentException("Invalid covariance factor.");
        }
        var covariance = Enumerable.Range(0, p).Select(i => Enumerable.Range(0, p).Select(j =>
            Enumerable.Range(0, p).Sum(k => data.Lower[i][k] * data.Lower[j][k])).ToArray()).ToArray();
        if (LinearAlgebra.ConditionInfinity(covariance, data.Lower) > 1e8) throw new ArgumentException("Ill-conditioned reference.");
        return new(data.Binding, data.Centers.ToArray(), data.Scales.ToArray(), data.Ar.ToArray(),
            data.InnovationCenters.ToArray(), data.Lower.Select(row => row.ToArray()).ToArray());
    }

    public static InnovationReference Fit(string binding, IReadOnlyList<IReadOnlyList<double[]>> runs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding);
        ArgumentNullException.ThrowIfNull(runs);
        if (runs.Count == 0 || runs.Any(run => run is null || run.Count < 2))
            throw new ArgumentException("Each explicit fit segment needs at least two residual vectors.");
        var p = runs[0][0].Length;
        var count = runs.Sum(run => run.Count);
        if (p is < 2 or > 8 || count > 50000 || count - runs.Count <= p)
            throw new ArgumentException("The bounded 2..8-dimensional fit needs full-rank innovation evidence.");
        var moments = new VectorMoments(p);
        foreach (var run in runs)
            foreach (var row in run)
            {
                RequireVector(row, p);
                moments.Add(row);
            }
        var centers = moments.Mean.ToArray();
        var covariance = moments.Covariance();
        var scales = Enumerable.Range(0, p).Select(j => Math.Sqrt(covariance[j][j])).ToArray();
        if (scales.Any(s => !double.IsFinite(s) || s <= 0))
            throw new ArgumentException("Fit residual scale is zero or nonfinite.");
        double[] Standardize(double[] row) => Enumerable.Range(0, p)
            .Select(j => (row[j] - centers[j]) / scales[j]).ToArray();
        var cross = new double[p];
        var lagSquares = new double[p];
        foreach (var run in runs)
        {
            var previous = Standardize(run[0]);
            for (var i = 1; i < run.Count; i++)
            {
                var current = Standardize(run[i]);
                for (var j = 0; j < p; j++)
                {
                    cross[j] += previous[j] * current[j];
                    lagSquares[j] += previous[j] * previous[j];
                }
                previous = current;
            }
        }
        var ar = Enumerable.Range(0, p).Select(j => cross[j] / lagSquares[j]).ToArray();
        if (ar.Any(value => !double.IsFinite(value) || Math.Abs(value) >= .98))
            throw new ArgumentException("Reference is nonstationary or has invalid AR evidence.");
        var innovations = new VectorMoments(p);
        foreach (var run in runs)
        {
            var previous = Standardize(run[0]);
            for (var i = 1; i < run.Count; i++)
            {
                var current = Standardize(run[i]);
                var innovation = Enumerable.Range(0, p).Select(j => current[j] - ar[j] * previous[j]).ToArray();
                RequireVector(innovation, p);
                innovations.Add(innovation);
                previous = current;
            }
        }
        var innovationCovariance = innovations.Covariance();
        var lower = LinearAlgebra.Cholesky(innovationCovariance);
        if (LinearAlgebra.ConditionInfinity(innovationCovariance, lower) > 1e8)
            throw new ArgumentException("Innovation covariance is ill-conditioned.");
        return new(binding, centers, scales, ar, innovations.Mean.ToArray(), lower);
    }

    internal double[] Transform(double[] current, double[] previous)
    {
        RequireVector(current, Dimensions);
        RequireVector(previous, Dimensions);
        var innovation = Enumerable.Range(0, Dimensions).Select(j =>
            (current[j] - centers[j]) / scales[j] - ar[j] * (previous[j] - centers[j]) / scales[j]
            - innovationCenters[j]).ToArray();
        var result = LinearAlgebra.SolveLower(lower, innovation);
        RequireVector(result, Dimensions);
        return result;
    }

    internal static void RequireVector(double[] row, int dimensions)
    {
        if (row is null || row.Length != dimensions || row.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("Residual vector has incompatible shape or nonfinite values.");
    }
}

internal sealed record InnovationReferenceData(string Binding, double[] Centers, double[] Scales,
    double[] Ar, double[] InnovationCenters, double[][] Lower);

internal sealed record InnovationObservation(DateTimeOffset SourceTime, double[] Residuals,
    bool Good, bool Synchronized, string Binding);

internal sealed record InnovationContrastResult(string Status, string? Reason,
    double? Level, double? Covariance, double? Score);

internal sealed class InnovationWindowContrast
{
    public const int WindowLength = 40;
    private readonly InnovationReference reference;
    private readonly TimeSpan cadence;
    private readonly Queue<double[]> window = new(WindowLength);
    private double[]? previous;
    private DateTimeOffset? previousTime;

    public InnovationWindowContrast(InnovationReference reference, TimeSpan cadence)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (cadence <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cadence));
        this.reference = reference;
        this.cadence = cadence;
    }

    public void Reset()
    {
        window.Clear();
        previous = null;
        previousTime = null;
    }

    public InnovationContrastResult Push(InnovationObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.Good || !observation.Synchronized || observation.SourceTime.Offset != TimeSpan.Zero ||
            !string.Equals(observation.Binding, reference.Binding, StringComparison.Ordinal) ||
            observation.Residuals is null || observation.Residuals.Length != reference.Dimensions ||
            observation.Residuals.Any(value => !double.IsFinite(value)))
        {
            Reset();
            return Unavailable("invalid_or_incompatible_source_evidence");
        }
        if (previousTime.HasValue && observation.SourceTime <= previousTime.Value)
        {
            Reset();
            return Unavailable("duplicate_or_out_of_order_source_time");
        }
        if (previousTime.HasValue && observation.SourceTime - previousTime.Value != cadence) Reset();
        if (previous is null)
        {
            previous = observation.Residuals.ToArray();
            previousTime = observation.SourceTime;
            return Unavailable("warmup");
        }
        try
        {
            var innovation = reference.Transform(observation.Residuals, previous);
            previous = observation.Residuals.ToArray();
            previousTime = observation.SourceTime;
            window.Enqueue(innovation);
            if (window.Count > WindowLength) window.Dequeue();
            if (window.Count < WindowLength) return Unavailable("warmup");
            return Calculate(window.ToArray());
        }
        catch (ArgumentException)
        {
            Reset();
            return Unavailable("nonfinite_or_degenerate_window");
        }
    }

    internal static InnovationContrastResult Calculate(IReadOnlyList<double[]> whitened)
    {
        if (whitened.Count != WindowLength) throw new ArgumentException("A complete innovation window is required.");
        var p = whitened[0].Length;
        if (p is < 2 or > 8) throw new ArgumentException("Only 2..8 targets are supported.");
        var moments = new VectorMoments(p);
        foreach (var row in whitened)
        {
            InnovationReference.RequireVector(row, p);
            moments.Add(row);
        }
        var covariance = moments.Covariance();
        var lower = LinearAlgebra.Cholesky(covariance);
        if (LinearAlgebra.ConditionInfinity(covariance, lower) > 1e8)
            throw new ArgumentException("Window covariance is ill-conditioned.");
        var level = WindowLength * moments.Mean.Sum(value => value * value);
        var trace = Enumerable.Range(0, p).Sum(j => covariance[j][j]);
        var logDet = Enumerable.Range(0, p).Sum(j => 2 * Math.Log(lower[j][j]));
        var scale = WindowLength * (trace - p - logDet);
        // The mathematical covariance term is nonnegative. Only roundoff at identity is tolerated.
        if (scale < -1e-9 || !double.IsFinite(level) || !double.IsFinite(scale) || !double.IsFinite(level + scale))
            throw new ArgumentException("Contrast is nonfinite or numerically invalid.");
        scale = Math.Max(0, scale);
        return new("available", null, level, scale, level + scale);
    }

    private static InnovationContrastResult Unavailable(string reason) =>
        new("insufficient_data", reason, null, null, null);
}

internal sealed class InnovationEpisodeTracker
{
    private readonly Queue<bool> decisions = new(5);
    private bool active;
    private int below;

    public void Reset() { decisions.Clear(); active = false; below = 0; }

    public bool Push(double? score, double threshold)
    {
        if (!double.IsFinite(threshold) || threshold < 0) throw new ArgumentOutOfRangeException(nameof(threshold));
        if (!score.HasValue || !double.IsFinite(score.Value)) { Reset(); return false; }
        var above = score.Value > threshold;
        decisions.Enqueue(above);
        if (decisions.Count > 5) decisions.Dequeue();
        if (active)
        {
            below = above ? 0 : below + 1;
            if (below == 5) { active = false; below = 0; }
            return false;
        }
        if (decisions.Count == 5 && decisions.Count(value => value) >= 3)
        {
            active = true;
            below = 0;
            return true;
        }
        return false;
    }
}

internal sealed class VectorMoments(int dimensions)
{
    private int count;
    public double[] Mean { get; } = new double[dimensions];
    private readonly double[][] cross = Enumerable.Range(0, dimensions).Select(_ => new double[dimensions]).ToArray();

    public void Add(double[] vector)
    {
        count++;
        var delta = new double[dimensions];
        for (var j = 0; j < dimensions; j++)
        {
            delta[j] = vector[j] - Mean[j];
            Mean[j] += delta[j] / count;
        }
        for (var i = 0; i < dimensions; i++)
            for (var j = 0; j <= i; j++) cross[i][j] += delta[i] * (vector[j] - Mean[j]);
    }

    public double[][] Covariance()
    {
        if (count < 2) throw new ArgumentException("Covariance requires at least two vectors.");
        var matrix = Enumerable.Range(0, dimensions).Select(_ => new double[dimensions]).ToArray();
        for (var i = 0; i < dimensions; i++)
            for (var j = 0; j <= i; j++) matrix[i][j] = matrix[j][i] = cross[i][j] / count;
        return matrix;
    }
}

internal static class LinearAlgebra
{
    public static double[][] Cholesky(double[][] matrix)
    {
        var n = matrix.Length;
        if (n == 0 || matrix.Any(row => row.Length != n)) throw new ArgumentException("Square matrix required.");
        var result = Enumerable.Range(0, n).Select(_ => new double[n]).ToArray();
        for (var i = 0; i < n; i++)
            for (var j = 0; j <= i; j++)
            {
                var value = matrix[i][j];
                for (var k = 0; k < j; k++) value -= result[i][k] * result[j][k];
                if (!double.IsFinite(value) || (i == j && value <= 0))
                    throw new ArgumentException("Covariance is not finite positive definite.");
                result[i][j] = i == j ? Math.Sqrt(value) : value / result[j][j];
            }
        return result;
    }

    public static double[] SolveLower(double[][] lower, double[] right)
    {
        var result = new double[right.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var value = right[i];
            for (var k = 0; k < i; k++) value -= lower[i][k] * result[k];
            result[i] = value / lower[i][i];
        }
        return result;
    }

    public static double ConditionInfinity(double[][] matrix, double[][] lower)
    {
        var n = matrix.Length;
        var inverse = Enumerable.Range(0, n).Select(_ => new double[n]).ToArray();
        for (var column = 0; column < n; column++)
        {
            var unit = new double[n]; unit[column] = 1;
            var y = SolveLower(lower, unit);
            for (var i = n - 1; i >= 0; i--)
            {
                var value = y[i];
                for (var j = i + 1; j < n; j++) value -= lower[j][i] * inverse[j][column];
                inverse[i][column] = value / lower[i][i];
            }
        }
        var condition = matrix.Max(row => row.Sum(Math.Abs)) * inverse.Max(row => row.Sum(Math.Abs));
        if (!double.IsFinite(condition)) throw new ArgumentException("Nonfinite covariance condition.");
        return condition;
    }
}
