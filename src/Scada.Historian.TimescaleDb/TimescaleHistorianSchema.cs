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
            data_type smallint NULL,
            source_ts timestamptz NULL,
            source_ts_evidence smallint NULL,
            server_ts timestamptz NULL,
            server_ts_evidence smallint NULL,
            capture_profile_id uuid NULL,
            capture_policy_state smallint NULL,
            capture_policy_fingerprint bytea NULL
        );

        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS data_type smallint NULL;
        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS source_ts timestamptz NULL;
        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS source_ts_evidence smallint NULL;
        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS server_ts timestamptz NULL;
        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS server_ts_evidence smallint NULL;
        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS capture_profile_id uuid NULL;
        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS capture_policy_state smallint NULL;
        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS capture_policy_fingerprint bytea NULL;

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

        CREATE TABLE IF NOT EXISTS elitescada.historian_archive_jobs (
            archive_id uuid PRIMARY KEY,
            from_utc timestamptz NOT NULL,
            to_utc timestamptz NOT NULL,
            next_segment_from_utc timestamptz NOT NULL,
            next_sequence integer NOT NULL DEFAULT 0,
            state text NOT NULL,
            delete_source_after_verification boolean NOT NULL DEFAULT FALSE,
            samples_archived bigint NOT NULL DEFAULT 0,
            failure_code text NULL,
            package_sha256 char(64) NULL,
            archive_length_bytes bigint NULL,
            archive_file_removed_at_utc timestamptz NULL,
            schedule_id smallint NULL,
            created_at_utc timestamptz NOT NULL,
            updated_at_utc timestamptz NOT NULL,
            completed_at_utc timestamptz NULL,
            CHECK (to_utc > from_utc),
            CHECK (next_segment_from_utc >= from_utc AND next_segment_from_utc <= to_utc)
        );
        ALTER TABLE elitescada.historian_archive_jobs
            ADD COLUMN IF NOT EXISTS archive_file_removed_at_utc timestamptz NULL;
        ALTER TABLE elitescada.historian_archive_jobs
            ADD COLUMN IF NOT EXISTS schedule_id smallint NULL;
        CREATE INDEX IF NOT EXISTS ix_historian_archive_jobs_queue
            ON elitescada.historian_archive_jobs (state, created_at_utc);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_historian_archive_jobs_active_schedule
            ON elitescada.historian_archive_jobs (schedule_id)
            WHERE schedule_id IS NOT NULL AND state <> 'Completed';

        CREATE TABLE IF NOT EXISTS elitescada.historian_archive_schedule (
            singleton_id smallint PRIMARY KEY CHECK (singleton_id = 1),
            enabled boolean NOT NULL,
            start_from_utc timestamptz NOT NULL,
            next_from_utc timestamptz NOT NULL,
            archive_after_days integer NOT NULL,
            interval_days integer NOT NULL,
            window_days integer NOT NULL,
            keep_archives_days integer NULL,
            delete_source_after_verification boolean NOT NULL DEFAULT FALSE,
            next_run_at_utc timestamptz NOT NULL,
            last_scheduled_at_utc timestamptz NULL,
            updated_at_utc timestamptz NOT NULL,
            CHECK (archive_after_days BETWEEN 1 AND 36500),
            CHECK (interval_days BETWEEN 1 AND 3650),
            CHECK (window_days BETWEEN 1 AND 31),
            CHECK (keep_archives_days IS NULL OR keep_archives_days BETWEEN 1 AND 36500)
        );

        CREATE TABLE IF NOT EXISTS elitescada.historian_archive_job_segments (
            archive_id uuid NOT NULL REFERENCES elitescada.historian_archive_jobs(archive_id) ON DELETE CASCADE,
            sequence integer NOT NULL,
            from_utc timestamptz NOT NULL,
            to_utc timestamptz NOT NULL,
            sample_count bigint NOT NULL,
            sha256 char(64) NOT NULL,
            file_name text NOT NULL,
            source_cleanup_completed_at_utc timestamptz NULL,
            PRIMARY KEY (archive_id, sequence),
            CHECK (to_utc > from_utc),
            CHECK (sample_count >= 0)
        );
        ALTER TABLE elitescada.historian_archive_job_segments
            ADD COLUMN IF NOT EXISTS source_cleanup_completed_at_utc timestamptz NULL;

        CREATE TABLE IF NOT EXISTS elitescada.historian_archive_restored_segments (
            archive_id uuid NOT NULL,
            sequence integer NOT NULL,
            sha256 char(64) NOT NULL,
            sample_count bigint NOT NULL,
            restored_at_utc timestamptz NOT NULL,
            PRIMARY KEY (archive_id, sequence),
            CHECK (sample_count >= 0)
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
