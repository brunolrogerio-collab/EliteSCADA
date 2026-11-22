namespace Scada.Core.Intelligence.Experimental;

/// <summary>One already-qualified, synchronized multi-signal source-time cohort.</summary>
public sealed record ExperimentalJointDeviationObservation(
    DateTimeOffset SourceTimestampUtc,
    IReadOnlyList<double> Values,
    bool Good,
    bool Synchronized,
    string ContextBinding);

/// <summary>
/// An experimental continuous score. It is not a fault classification, alarm, probability,
/// efficiency estimate, or field-validated operating limit.
/// </summary>
public sealed record ExperimentalJointDeviationScore(
    string Status,
    string? Reason,
    double? LevelComponent,
    double? RelationshipComponent,
    double? Score,
    IReadOnlyList<double>? TargetLevelContributions = null,
    IReadOnlyList<double>? TargetVarianceContributions = null,
    double? CrossSignalInteraction = null);

/// <summary>
/// Fixed reference fit for the simulation-validated Stage 4F experimental joint score.
/// Caller supplies separate contiguous runs so lag features never cross a known gap.
/// </summary>
public sealed class ExperimentalJointDeviationReference
{
    public const int MinimumSignals = 2;
    public const int MaximumSignals = 8;
    public const int MinimumReferenceInnovations = 100;
    public const int MinimumReferenceInnovationsPerSignal = 20;
    public const int MaximumReferenceRows = 50_000;
    public const double DiagonalShrinkage = 0.05;
    public const double MaximumConditionNumber = 1e8;

    private readonly double[] centers;
    private readonly double[] scales;
    private readonly double[] autoregressiveCoefficients;
    private readonly double[] innovationCenters;
    private readonly double[][] covarianceFactor;

    private ExperimentalJointDeviationReference(
        string contextBinding,
        double[] centers,
        double[] scales,
        double[] autoregressiveCoefficients,
        double[] innovationCenters,
        double[][] covarianceFactor)
    {
        ContextBinding = contextBinding;
        this.centers = centers;
        this.scales = scales;
        this.autoregressiveCoefficients = autoregressiveCoefficients;
        this.innovationCenters = innovationCenters;
        this.covarianceFactor = covarianceFactor;
    }

    public string ContextBinding { get; }
    public int SignalCount => centers.Length;

    public static ExperimentalJointDeviationReference Fit(
        string contextBinding,
        IReadOnlyList<IReadOnlyList<double[]>> contiguousRuns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextBinding);
        ArgumentNullException.ThrowIfNull(contiguousRuns);
        if (contiguousRuns.Count is < 1 or > 512 || contiguousRuns.Any(run => run is null || run.Count < 2))
            throw new ArgumentException("Each reference run must contain at least two complete vectors.", nameof(contiguousRuns));

        var signalCount = contiguousRuns[0][0]?.Length ?? 0;
        if (signalCount is < MinimumSignals or > MaximumSignals)
            throw new ArgumentException("The experimental joint score supports 2..8 signals.", nameof(contiguousRuns));

        var rowCount = contiguousRuns.Sum(run => run.Count);
        if (rowCount > MaximumReferenceRows || rowCount - contiguousRuns.Count < Math.Max(
                MinimumReferenceInnovations, signalCount * MinimumReferenceInnovationsPerSignal))
            throw new ArgumentException("Reference evidence is below the bounded experimental fit minimum or above its row limit.", nameof(contiguousRuns));

        foreach (var run in contiguousRuns)
            foreach (var row in run)
                RequireVector(row, signalCount);

        var centers = new double[signalCount];
        var scales = new double[signalCount];
        for (var signal = 0; signal < signalCount; signal++)
        {
            var values = contiguousRuns.SelectMany(run => run).Select(row => row[signal]).ToArray();
            centers[signal] = Median(values);
            var deviations = values.Select(value => Math.Abs(value - centers[signal])).ToArray();
            scales[signal] = 1.4826 * Median(deviations);
            if (!double.IsFinite(scales[signal]) || scales[signal] <= 0)
                throw new ArgumentException("A reference signal has zero or nonfinite robust scale.", nameof(contiguousRuns));
        }

        double[] Standardize(double[] row) => Enumerable.Range(0, signalCount)
            .Select(signal => (row[signal] - centers[signal]) / scales[signal]).ToArray();

        var crossProducts = new double[signalCount];
        var lagSquares = new double[signalCount];
        foreach (var run in contiguousRuns)
        {
            var previous = Standardize(run[0]);
            for (var rowIndex = 1; rowIndex < run.Count; rowIndex++)
            {
                var current = Standardize(run[rowIndex]);
                for (var signal = 0; signal < signalCount; signal++)
                {
                    crossProducts[signal] += previous[signal] * current[signal];
                    lagSquares[signal] += previous[signal] * previous[signal];
                }
                previous = current;
            }
        }

        var autoregressiveCoefficients = Enumerable.Range(0, signalCount)
            .Select(signal => crossProducts[signal] / lagSquares[signal]).ToArray();
        if (autoregressiveCoefficients.Any(value => !double.IsFinite(value) || Math.Abs(value) >= 0.98))
            throw new ArgumentException("Reference dynamics are nonfinite or outside the experimental AR(1) stability bound.", nameof(contiguousRuns));

        var innovationMoments = new VectorMoments(signalCount);
        foreach (var run in contiguousRuns)
        {
            var previous = Standardize(run[0]);
            for (var rowIndex = 1; rowIndex < run.Count; rowIndex++)
            {
                var current = Standardize(run[rowIndex]);
                var innovation = Enumerable.Range(0, signalCount)
                    .Select(signal => current[signal] - autoregressiveCoefficients[signal] * previous[signal]).ToArray();
                RequireVector(innovation, signalCount);
                innovationMoments.Add(innovation);
                previous = current;
            }
        }

        var innovationCovariance = RegularizeTowardDiagonal(innovationMoments.Covariance(), DiagonalShrinkage);
        var factor = LinearAlgebra.Cholesky(innovationCovariance);
        if (LinearAlgebra.ConditionInfinity(innovationCovariance, factor) > MaximumConditionNumber)
            throw new ArgumentException("Reference innovation covariance is ill-conditioned.", nameof(contiguousRuns));

        return new ExperimentalJointDeviationReference(
            contextBinding,
            centers,
            scales,
            autoregressiveCoefficients,
            innovationMoments.Mean.ToArray(),
            factor);
    }

    internal double[] Transform(double[] current, double[] previous)
    {
        RequireVector(current, SignalCount);
        RequireVector(previous, SignalCount);
        var innovation = Enumerable.Range(0, SignalCount).Select(signal =>
        {
            var currentStandardized = (current[signal] - centers[signal]) / scales[signal];
            var previousStandardized = (previous[signal] - centers[signal]) / scales[signal];
            return currentStandardized - autoregressiveCoefficients[signal] * previousStandardized - innovationCenters[signal];
        }).ToArray();
        var transformed = LinearAlgebra.SolveLower(covarianceFactor, innovation);
        RequireVector(transformed, SignalCount);
        return transformed;
    }

    internal static void RequireVector(double[]? values, int expectedLength)
    {
        if (values is null || values.Length != expectedLength || values.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("A vector has an incompatible shape or nonfinite value.");
    }

    private static double[][] RegularizeTowardDiagonal(double[][] covariance, double shrinkage)
    {
        var size = covariance.Length;
        var result = Enumerable.Range(0, size).Select(_ => new double[size]).ToArray();
        for (var row = 0; row < size; row++)
            for (var column = 0; column < size; column++)
                result[row][column] = row == column
                    ? covariance[row][column]
                    : (1 - shrinkage) * covariance[row][column];
        return result;
    }

    private static double Median(double[] values)
    {
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        var median = ordered.Length % 2 == 0
            ? ordered[middle - 1] / 2 + ordered[middle] / 2
            : ordered[middle];
        if (!double.IsFinite(median)) throw new ArgumentException("Reference median is nonfinite.");
        return median;
    }
}

/// <summary>
/// On-demand experimental scorer over a complete stream of pre-qualified cohorts.
/// A cadence gap, invalid row, duplicate time or context change clears warm-up state.
/// No threshold or classification is applied by this scorer.
/// </summary>
public sealed class ExperimentalJointDeviationDetector
{
    public const int WindowLength = 40;
    private readonly ExperimentalJointDeviationReference reference;
    private readonly TimeSpan expectedCadence;
    private readonly Queue<double[]> window = new(WindowLength);
    private double[]? previousValues;
    private DateTimeOffset? previousTimestamp;

    public ExperimentalJointDeviationDetector(ExperimentalJointDeviationReference reference, TimeSpan expectedCadence)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (expectedCadence <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(expectedCadence));
        this.reference = reference;
        this.expectedCadence = expectedCadence;
    }

    public void Reset()
    {
        window.Clear();
        previousValues = null;
        previousTimestamp = null;
    }

    public ExperimentalJointDeviationScore Push(ExperimentalJointDeviationObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.Good || !observation.Synchronized || observation.SourceTimestampUtc.Offset != TimeSpan.Zero ||
            !string.Equals(observation.ContextBinding, reference.ContextBinding, StringComparison.Ordinal))
        {
            Reset();
            return Unavailable("invalid_or_incompatible_source_evidence");
        }

        if (observation.Values is null)
        {
            Reset();
            return Unavailable("invalid_or_incompatible_source_evidence");
        }
        var values = observation.Values.ToArray();
        try
        {
            ExperimentalJointDeviationReference.RequireVector(values, reference.SignalCount);
        }
        catch (ArgumentException)
        {
            Reset();
            return Unavailable("invalid_or_incompatible_source_evidence");
        }

        if (previousTimestamp.HasValue && observation.SourceTimestampUtc <= previousTimestamp.Value)
        {
            Reset();
            return Unavailable("duplicate_or_out_of_order_source_time");
        }

        if (previousTimestamp.HasValue && observation.SourceTimestampUtc - previousTimestamp.Value != expectedCadence)
            Reset();

        if (previousValues is null)
        {
            previousValues = values;
            previousTimestamp = observation.SourceTimestampUtc;
            return Unavailable("warmup");
        }

        try
        {
            var innovation = reference.Transform(values, previousValues);
            previousValues = values;
            previousTimestamp = observation.SourceTimestampUtc;
            window.Enqueue(innovation);
            if (window.Count > WindowLength) window.Dequeue();
            return window.Count < WindowLength ? Unavailable("warmup") : CalculateWindow(window.ToArray());
        }
        catch (ArgumentException)
        {
            Reset();
            return Unavailable("nonfinite_or_degenerate_window");
        }
    }

    public static ExperimentalJointDeviationScore CalculateWindow(IReadOnlyList<double[]> innovations)
    {
        ArgumentNullException.ThrowIfNull(innovations);
        if (innovations.Count != WindowLength)
            throw new ArgumentException("A complete 40-cohort innovation window is required.", nameof(innovations));

        var signalCount = innovations[0]?.Length ?? 0;
        if (signalCount is < ExperimentalJointDeviationReference.MinimumSignals or > ExperimentalJointDeviationReference.MaximumSignals)
            throw new ArgumentException("The experimental joint score supports 2..8 signals.", nameof(innovations));

        var moments = new VectorMoments(signalCount);
        foreach (var innovation in innovations)
        {
            ExperimentalJointDeviationReference.RequireVector(innovation, signalCount);
            moments.Add(innovation);
        }

        var covariance = moments.Covariance();
        var factor = LinearAlgebra.Cholesky(covariance);
        if (LinearAlgebra.ConditionInfinity(covariance, factor) > ExperimentalJointDeviationReference.MaximumConditionNumber)
            throw new ArgumentException("Window covariance is ill-conditioned.", nameof(innovations));

        var level = WindowLength * moments.Mean.Sum(value => value * value);
        var trace = Enumerable.Range(0, signalCount).Sum(signal => covariance[signal][signal]);
        var logDeterminant = Enumerable.Range(0, signalCount).Sum(signal => 2 * Math.Log(factor[signal][signal]));
        var relationship = WindowLength * (trace - signalCount - logDeterminant);
        if (relationship < -1e-9 || !double.IsFinite(level) || !double.IsFinite(relationship) || !double.IsFinite(level + relationship))
            throw new ArgumentException("The joint deviation score is nonfinite or numerically invalid.", nameof(innovations));

        relationship = Math.Max(0, relationship);
        var targetLevel = moments.Mean.Select(value => WindowLength * value * value).ToArray();
        var targetVariance = Enumerable.Range(0, signalCount).Select(signal =>
        {
            var variance = covariance[signal][signal];
            if (!double.IsFinite(variance) || variance <= 0)
                throw new ArgumentException("A signal has degenerate variance in the score window.", nameof(innovations));
            return WindowLength * (variance - 1 - Math.Log(variance));
        }).ToArray();
        var interaction = relationship - targetVariance.Sum();
        if (interaction < -1e-8 || !double.IsFinite(interaction))
            throw new ArgumentException("The cross-signal contribution is numerically invalid.", nameof(innovations));

        return new ExperimentalJointDeviationScore("available", null, level, relationship, level + relationship,
            Array.AsReadOnly(targetLevel), Array.AsReadOnly(targetVariance), Math.Max(0, interaction));
    }

    private static ExperimentalJointDeviationScore Unavailable(string reason) =>
        new("insufficient_data", reason, null, null, null);
}

internal sealed class VectorMoments(int dimensions)
{
    private int count;
    public double[] Mean { get; } = new double[dimensions];
    private readonly double[][] crossProducts = Enumerable.Range(0, dimensions).Select(_ => new double[dimensions]).ToArray();

    public void Add(double[] vector)
    {
        count++;
        var delta = new double[dimensions];
        for (var signal = 0; signal < dimensions; signal++)
        {
            delta[signal] = vector[signal] - Mean[signal];
            Mean[signal] += delta[signal] / count;
        }
        for (var row = 0; row < dimensions; row++)
            for (var column = 0; column <= row; column++)
                crossProducts[row][column] += delta[row] * (vector[column] - Mean[column]);
    }

    public double[][] Covariance()
    {
        if (count < 2) throw new ArgumentException("Covariance requires at least two vectors.");
        var covariance = Enumerable.Range(0, dimensions).Select(_ => new double[dimensions]).ToArray();
        for (var row = 0; row < dimensions; row++)
            for (var column = 0; column <= row; column++)
                covariance[row][column] = covariance[column][row] = crossProducts[row][column] / count;
        return covariance;
    }
}

internal static class LinearAlgebra
{
    public static double[][] Cholesky(double[][] matrix)
    {
        var size = matrix.Length;
        if (size == 0 || matrix.Any(row => row.Length != size)) throw new ArgumentException("A square matrix is required.");
        var lower = Enumerable.Range(0, size).Select(_ => new double[size]).ToArray();
        for (var row = 0; row < size; row++)
            for (var column = 0; column <= row; column++)
            {
                var value = matrix[row][column];
                for (var index = 0; index < column; index++) value -= lower[row][index] * lower[column][index];
                if (!double.IsFinite(value) || (row == column && value <= 0))
                    throw new ArgumentException("The covariance matrix is not finite positive-definite.");
                lower[row][column] = row == column ? Math.Sqrt(value) : value / lower[column][column];
            }
        return lower;
    }

    public static double[] SolveLower(double[][] lower, double[] right)
    {
        var solution = new double[right.Length];
        for (var row = 0; row < solution.Length; row++)
        {
            var value = right[row];
            for (var column = 0; column < row; column++) value -= lower[row][column] * solution[column];
            solution[row] = value / lower[row][row];
        }
        return solution;
    }

    public static double ConditionInfinity(double[][] matrix, double[][] lower)
    {
        var size = matrix.Length;
        var inverse = Enumerable.Range(0, size).Select(_ => new double[size]).ToArray();
        for (var column = 0; column < size; column++)
        {
            var unit = new double[size];
            unit[column] = 1;
            var solution = SolveLower(lower, unit);
            for (var row = size - 1; row >= 0; row--)
            {
                var value = solution[row];
                for (var next = row + 1; next < size; next++) value -= lower[next][row] * inverse[next][column];
                inverse[row][column] = value / lower[row][row];
            }
        }

        var condition = matrix.Max(row => row.Sum(Math.Abs)) * inverse.Max(row => row.Sum(Math.Abs));
        if (!double.IsFinite(condition)) throw new ArgumentException("The covariance condition number is nonfinite.");
        return condition;
    }
}
