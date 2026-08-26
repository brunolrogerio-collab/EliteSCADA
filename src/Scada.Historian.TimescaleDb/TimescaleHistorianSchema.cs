using Scada.Core.Tags;
using Scada.Historian.Policies;

namespace Scada.Historian.TimescaleDb;

internal static class TimescaleHistorianSchema
{
    public const string RawTable = "elitescada.tag_history";
    public const string PolicyStateTable = "elitescada.historian_storage_policy_state";

    public const string RawInfrastructureSql = """
        CREATE EXTENSION IF NOT EXISTS timescaledb;
        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.tag_history (
            tag_id uuid NOT NULL,
            ts timestamptz NOT NULL,
            quality integer NOT NULL,
            source text NULL,
            value jsonb NOT NULL,
            data_type smallint NULL
        );

        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS data_type smallint NULL;

        SELECT create_hypertable(
            'elitescada.tag_history',
            by_range('ts'),
            if_not_exists => TRUE);

        CREATE INDEX IF NOT EXISTS ix_tag_history_tag_time
            ON elitescada.tag_history (tag_id, ts DESC);

        CREATE TABLE IF NOT EXISTS elitescada.historian_storage_policy_state (
            singleton_id smallint PRIMARY KEY CHECK (singleton_id = 1),
            policy_json jsonb NOT NULL,
            applied_at timestamptz NOT NULL
        );
        """;

    public static IReadOnlyCollection<HistorianBucketWidth> SupportedBuckets { get; } =
    [
        HistorianBucketWidth.OneMinute,
        HistorianBucketWidth.FiveMinutes,
        HistorianBucketWidth.FifteenMinutes,
        HistorianBucketWidth.OneHour
    ];

    public static string AggregateViewName(HistorianBucketWidth bucket) => bucket switch
    {
        HistorianBucketWidth.OneMinute => "elitescada.tag_history_agg_1m",
        HistorianBucketWidth.FiveMinutes => "elitescada.tag_history_agg_5m",
        HistorianBucketWidth.FifteenMinutes => "elitescada.tag_history_agg_15m",
        HistorianBucketWidth.OneHour => "elitescada.tag_history_agg_1h",
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unsupported historian bucket width.")
    };

    public static string AggregateIndexName(HistorianBucketWidth bucket) => bucket switch
    {
        HistorianBucketWidth.OneMinute => "ix_tag_history_agg_1m_tag_time",
        HistorianBucketWidth.FiveMinutes => "ix_tag_history_agg_5m_tag_time",
        HistorianBucketWidth.FifteenMinutes => "ix_tag_history_agg_15m_tag_time",
        HistorianBucketWidth.OneHour => "ix_tag_history_agg_1h_tag_time",
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unsupported historian bucket width.")
    };

    public static string IntervalLiteral(HistorianBucketWidth bucket) => bucket switch
    {
        HistorianBucketWidth.OneMinute => "1 minute",
        HistorianBucketWidth.FiveMinutes => "5 minutes",
        HistorianBucketWidth.FifteenMinutes => "15 minutes",
        HistorianBucketWidth.OneHour => "1 hour",
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unsupported historian bucket width.")
    };

    public static string BuildAggregateInfrastructureSql(HistorianBucketWidth bucket)
    {
        var view = AggregateViewName(bucket);
        var index = AggregateIndexName(bucket);
        var interval = IntervalLiteral(bucket);
        var good = (int)TagQuality.Good;
        var uncertain = (int)TagQuality.Uncertain;
        var numericTypes = string.Join(", ", new[]
        {
            (int)TagDataType.Int16,
            (int)TagDataType.Int32,
            (int)TagDataType.Int64,
            (int)TagDataType.Float,
            (int)TagDataType.Double
        });

        return $"""
            CREATE MATERIALIZED VIEW IF NOT EXISTS {view}
            WITH (timescaledb.continuous) AS
            SELECT
                time_bucket(INTERVAL '{interval}', ts) AS bucket_start,
                tag_id,
                count(*)::bigint AS sample_count,
                count(*) FILTER (WHERE quality = {good})::bigint AS good_count,
                count(*) FILTER (WHERE quality = {uncertain})::bigint AS uncertain_count,
                count(*) FILTER (WHERE quality <> {good} AND quality <> {uncertain})::bigint AS bad_count,
                count(*) FILTER (
                    WHERE quality = {good}
                      AND data_type IN ({numericTypes})
                      AND jsonb_typeof(value) = 'number')::bigint AS numeric_good_count,
                min(CASE
                    WHEN quality = {good}
                     AND data_type IN ({numericTypes})
                     AND jsonb_typeof(value) = 'number'
                    THEN value::text::double precision
                    ELSE NULL
                END) AS numeric_minimum,
                max(CASE
                    WHEN quality = {good}
                     AND data_type IN ({numericTypes})
                     AND jsonb_typeof(value) = 'number'
                    THEN value::text::double precision
                    ELSE NULL
                END) AS numeric_maximum,
                avg(CASE
                    WHEN quality = {good}
                     AND data_type IN ({numericTypes})
                     AND jsonb_typeof(value) = 'number'
                    THEN value::text::double precision
                    ELSE NULL
                END) AS numeric_average,
                first(value, ts) AS first_value,
                first(quality, ts)::integer AS first_quality,
                first(data_type, ts)::smallint AS first_data_type,
                last(value, ts) AS last_value,
                last(quality, ts)::integer AS last_quality,
                last(data_type, ts)::smallint AS last_data_type,
                min(data_type)::smallint AS min_data_type,
                max(data_type)::smallint AS max_data_type
            FROM elitescada.tag_history
            GROUP BY bucket_start, tag_id
            WITH NO DATA;

            CREATE INDEX IF NOT EXISTS {index}
                ON {view} (tag_id, bucket_start DESC);
            """;
    }
}
