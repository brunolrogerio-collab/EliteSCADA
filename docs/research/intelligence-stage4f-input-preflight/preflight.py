#!/usr/bin/env python3
"""Bounded, offline structural preflight for an explicitly supplied Stage 4F CSV.

This utility is intentionally not an analytical eligibility or product-readiness
validator. It accepts one local path, makes no network calls, and never echoes
input values, identifiers, timestamps, or paths into its report.
"""

from __future__ import annotations

import argparse
import csv
import io
import json
import math
import os
import re
import struct
import sys
import uuid
from collections import Counter, defaultdict
from datetime import date
from pathlib import Path
from typing import Any, Sequence


EXPECTED_COLUMNS = (
    "export.id",
    "cohort.id",
    "tag.id",
    "tag.path",
    "quality",
    "value.kind",
    "value",
    "timestamp",
    "source.timestamp",
    "source.timestamp.status",
    "server.timestamp",
    "server.timestamp.status",
    "capture.policy.profileId",
    "capture.policy.fingerprint",
    "capture.policy.status",
)

QUALITIES = frozenset(
    {
        "Good",
        "Uncertain",
        "Bad",
        "BadCommunication",
        "BadConfiguration",
        "BadDevice",
        "Stale",
        "Disabled",
        "Unavailable",
    }
)
SOURCE_STATES = frozenset(
    {"NotRecorded", "NotProvided", "PresentUnverified", "Unsynchronized", "Synchronized"}
)
SERVER_STATES = frozenset({"NotRecorded", "NotProvided", "PresentUnverified"})
CAPTURE_STATES = frozenset({"NotRecorded", "LegacyDefault", "Explicit"})
VALUE_KINDS = frozenset(
    {
        "guid",
        "string",
        "enum",
        "int16",
        "int32",
        "int64",
        "float",
        "double",
        "number",
        "boolean",
        "dateTime",
        "null",
    }
)

# Deliberate preflight resource bounds. These do not replace product query limits.
MAX_INPUT_BYTES = 256 * 1024 * 1024
MAX_DATA_ROWS = 1_000_000
MAX_FIELD_CHARS = 65_536
MAX_DIAGNOSTICS = 100
MAX_IDENTIFIER_CHARS = 256
MAX_TAG_PATH_CHARS = 2_048
MAX_NUMERIC_CHARS = 128
MAX_DATETIME_CHARS = 40
FINGERPRINT_PREFIX = "sha256-128-v1:"
UTC_TIMESTAMP_RE = re.compile(
    r"^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?(Z|\+00:00)$"
)
GUID_D_RE = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
INTEGER_RE = re.compile(r"^[+-]?\d+$")
NUMBER_RE = re.compile(r"^[+-]?(?:(?:\d+(?:\.\d*)?)|(?:\.\d+))(?:[eE][+-]?\d+)?$")
FINGERPRINT_RE = re.compile(r"^[0-9a-f]{32}$")


class PreflightFailure(Exception):
    """Sanitized failure carrying a stable process exit code and reason."""

    def __init__(self, exit_code: int, reason: str) -> None:
        super().__init__(reason)
        self.exit_code = exit_code
        self.reason = reason


class Diagnostics:
    def __init__(self) -> None:
        self.total = 0
        self.reasons: Counter[str] = Counter()
        self.items: list[dict[str, Any]] = []

    def add(self, row: int, column: str, reason: str) -> None:
        self.total += 1
        self.reasons[reason] += 1
        if len(self.items) < MAX_DIAGNOSTICS:
            self.items.append({"row": row, "column": column, "reason": reason})

    def as_dict(self) -> dict[str, Any]:
        return {
            "total": self.total,
            "truncated": self.total > len(self.items),
            "reason_counts": dict(sorted(self.reasons.items())),
            "items": self.items,
        }


def _base_report() -> dict[str, Any]:
    return {
        "report_version": 1,
        "scope": "local_input_preflight_only",
        "structure_status": "not_assessed",
        "content_status": "not_assessed",
        "analytical_eligibility": "NOT_ASSESSED",
        "product_readiness": "NOT_ASSESSED",
        "assessments": {
            "required_signal_set": "NOT_ASSESSED",
            "operating_context": "NOT_ASSESSED",
            "independent_labels": "NOT_ASSESSED",
            "completeness_and_cadence": "NOT_ASSESSED",
            "physical_clock_synchronization": "NOT_ASSESSED",
            "exposure_targets": "NOT_ASSESSED",
            "data_leakage": "NOT_ASSESSED",
            "authorization_and_analytical_use": "NOT_ASSESSED",
            "statistical_sufficiency": "NOT_ASSESSED",
            "query_and_detector_performance": "NOT_ASSESSED",
        },
        "bounds": {
            "max_input_bytes": MAX_INPUT_BYTES,
            "max_data_rows": MAX_DATA_ROWS,
            "max_field_characters": MAX_FIELD_CHARS,
            "max_diagnostics": MAX_DIAGNOSTICS,
        },
        "counts": {
            "data_rows": 0,
            "cohorts": 0,
            "tags": 0,
            "non_good_rows": 0,
            "missing_source_timestamp_rows": 0,
            "duplicate_source_time_groups": 0,
            "duplicate_source_time_rows": 0,
        },
        "quality_counts": {},
        "value_kind_counts": {},
        "source_timestamp_status_counts": {},
        "server_timestamp_status_counts": {},
        "capture_policy_status_counts": {},
        "diagnostics": {"total": 0, "truncated": False, "reason_counts": {}, "items": []},
        "limits_and_nonclaims": [
            "This report checks bounded file structure and row-level contract shape only.",
            "It does not assess analytical eligibility, statistical sufficiency, cadence, synchronization, labels, or readiness.",
            "No raw values, identifiers, TAG paths, timestamps, credentials, or file paths are emitted.",
        ],
    }


def _safe_error_report(reason: str) -> dict[str, Any]:
    report = _base_report()
    if reason in {"input_byte_limit_exceeded", "csv_field_limit_exceeded", "resource_limit_exceeded"}:
        report["structure_status"] = "incomplete"
    elif reason in {
        "input_not_regular_file",
        "input_unavailable",
        "report_path_is_input",
        "report_path_invalid",
        "report_path_inside_git_repository",
        "report_path_exists",
        "report_path_unavailable",
        "report_write_failed",
        "arguments_invalid",
        "internal_error",
    }:
        report["structure_status"] = "not_assessed"
    else:
        report["structure_status"] = "malformed"
    report["content_status"] = "not_assessed"
    report["diagnostics"] = {
        "total": 1,
        "truncated": False,
        "reason_counts": {reason: 1},
        "items": [{"row": 1, "column": "file", "reason": reason}],
    }
    return report


def _utc_ticks(text: str) -> int | None:
    """Parse UTC ISO-8601 without discarding any of the seven .NET tick digits."""
    if not text or len(text) > MAX_DATETIME_CHARS:
        return None
    match = UTC_TIMESTAMP_RE.fullmatch(text)
    if not match:
        return None
    year, month, day, hour, minute, second = map(int, match.groups()[:6])
    fraction, _zone = match.groups()[6:]
    try:
        calendar_day = date(year, month, day)
    except ValueError:
        return None
    if hour > 23 or minute > 59 or second > 59:
        return None
    days = calendar_day.toordinal() - date(1, 1, 1).toordinal()
    fraction_ticks = int((fraction or "").ljust(7, "0"))
    return (((days * 24 + hour) * 60 + minute) * 60 + second) * 10_000_000 + fraction_ticks


def _valid_guid(text: str, *, allow_empty: bool = False) -> bool:
    if not GUID_D_RE.fullmatch(text):
        return False
    try:
        parsed = uuid.UUID(text)
    except ValueError:
        return False
    return allow_empty or parsed.int != 0


def _is_identifier(text: str, maximum: int = MAX_IDENTIFIER_CHARS) -> bool:
    return (
        0 < len(text) <= maximum
        and not text.isspace()
        and text == text.strip()
        and not any(ord(char) < 32 or ord(char) == 127 for char in text)
    )


def _validate_typed_value(kind: str, value: str) -> str | None:
    if kind not in VALUE_KINDS:
        return "value_kind_unknown"
    if len(value) > MAX_FIELD_CHARS:
        return "value_field_too_long"
    if kind == "null":
        return None if value == "" else "null_value_must_be_empty"
    if kind in {"string", "enum"}:
        return None
    if kind == "guid":
        return None if _valid_guid(value, allow_empty=True) else "value_guid_invalid"
    if kind == "boolean":
        return None if value in {"true", "false"} else "value_boolean_invalid"
    if kind == "dateTime":
        return None if _utc_ticks(value) is not None else "value_datetime_invalid"
    if kind in {"int16", "int32", "int64"}:
        if len(value) > MAX_NUMERIC_CHARS or not INTEGER_RE.fullmatch(value):
            return "value_integer_invalid"
        try:
            number = int(value, 10)
        except ValueError:
            return "value_integer_invalid"
        lower, upper = {
            "int16": (-32768, 32767),
            "int32": (-(2**31), 2**31 - 1),
            "int64": (-(2**63), 2**63 - 1),
        }[kind]
        return None if lower <= number <= upper else "value_integer_out_of_range"
    if kind in {"float", "double", "number"}:
        if len(value) > MAX_NUMERIC_CHARS or not NUMBER_RE.fullmatch(value):
            return "value_number_invalid"
        try:
            number = float(value)
        except ValueError:
            return "value_number_invalid"
        if not math.isfinite(number):
            return "value_number_not_finite"
        if kind == "float":
            try:
                if not math.isfinite(struct.unpack("!f", struct.pack("!f", number))[0]):
                    return "value_number_not_finite"
            except OverflowError:
                return "value_number_not_finite"
        return None
    return "value_kind_unknown"


def _validate_timestamp_pair(
    row: dict[str, str],
    timestamp_column: str,
    state_column: str,
    allowed_states: frozenset[str],
    diagnostics: Diagnostics,
    row_number: int,
) -> int | None:
    timestamp = row[timestamp_column]
    state = row[state_column]
    ticks = _utc_ticks(timestamp) if timestamp else None
    if timestamp and ticks is None:
        diagnostics.add(row_number, timestamp_column, "timestamp_utc_invalid")
    if state not in allowed_states:
        reason = (
            "timestamp_status_not_allowed"
            if state in SOURCE_STATES or state in SERVER_STATES
            else "timestamp_status_unknown"
        )
        diagnostics.add(row_number, state_column, reason)
        return ticks
    if state == "NotProvided":
        if timestamp:
            diagnostics.add(row_number, state_column, "timestamp_not_provided_but_present")
        return ticks
    if state != "NotRecorded" and not timestamp:
        diagnostics.add(row_number, timestamp_column, "timestamp_required_by_evidence")
    return ticks


def analyze_bytes(data: bytes) -> tuple[dict[str, Any], int]:
    """Analyze a byte sequence; exposed for synthetic tests and never writes files."""
    report = _base_report()
    if len(data) > MAX_INPUT_BYTES:
        return _safe_error_report("input_byte_limit_exceeded"), 4
    try:
        text = data.decode("utf-8-sig", errors="strict")
    except UnicodeDecodeError:
        return _safe_error_report("utf8_invalid"), 2

    old_limit = csv.field_size_limit()
    csv.field_size_limit(MAX_FIELD_CHARS)
    try:
        reader = csv.reader(io.StringIO(text, newline=""), strict=True)
        try:
            header = next(reader)
        except StopIteration:
            return _safe_error_report("header_missing"), 2
        except csv.Error as error:
            reason = "csv_field_limit_exceeded" if "field larger than field limit" in str(error) else "csv_malformed"
            return _safe_error_report(reason), 4 if reason == "csv_field_limit_exceeded" else 2
        except UnicodeError:
            return _safe_error_report("csv_malformed"), 2

        diagnostics = Diagnostics()
        if not header:
            diagnostics.add(1, "header", "header_missing")
        counts = Counter(report["counts"])
        quality_counts: Counter[str] = Counter()
        kind_counts: Counter[str] = Counter()
        source_counts: Counter[str] = Counter()
        server_counts: Counter[str] = Counter()
        capture_counts: Counter[str] = Counter()
        seen_export: str | None = None
        cohort_ids: set[str] = set()
        tag_ids: set[str] = set()
        id_paths: dict[str, str] = {}
        path_ids: dict[str, str] = {}
        duplicate_keys: dict[tuple[str, str, str, int], int] = defaultdict(int)
        structural_error = False

        if len(header) != len(set(header)):
            diagnostics.add(1, "header", "header_duplicate_column")
        header_set = set(header)
        for column in EXPECTED_COLUMNS:
            if column not in header_set:
                diagnostics.add(1, column, "header_required_column_missing")
        for column in header:
            if column not in EXPECTED_COLUMNS:
                diagnostics.add(1, "header", "header_unknown_column")
        if diagnostics.total:
            report["structure_status"] = "malformed"
            report["content_status"] = "not_assessed"
            report["diagnostics"] = diagnostics.as_dict()
            return report, 2

        column_index = {name: header.index(name) for name in EXPECTED_COLUMNS}
        try:
            for row_number, fields in enumerate(reader, start=2):
                if row_number - 1 > MAX_DATA_ROWS:
                    report["structure_status"] = "incomplete"
                    report["content_status"] = "not_assessed"
                    report["diagnostics"] = {
                        "total": 1,
                        "truncated": False,
                        "reason_counts": {"data_row_limit_exceeded": 1},
                        "items": [{"row": row_number, "column": "file", "reason": "data_row_limit_exceeded"}],
                    }
                    report["counts"]["data_rows"] = MAX_DATA_ROWS
                    return report, 4
                if len(fields) != len(header):
                    diagnostics.add(row_number, "row", "csv_row_width_mismatch")
                    counts["data_rows"] += 1
                    structural_error = True
                    continue
                row = {name: fields[index] for name, index in column_index.items()}
                counts["data_rows"] += 1
                export_id = row["export.id"]
                cohort_id = row["cohort.id"]
                tag_id = row["tag.id"]
                tag_path = row["tag.path"]

                if not _is_identifier(export_id):
                    diagnostics.add(row_number, "export.id", "export_id_invalid")
                elif seen_export is None:
                    seen_export = export_id
                elif export_id != seen_export:
                    diagnostics.add(row_number, "export.id", "export_id_inconsistent")
                if not _is_identifier(cohort_id):
                    diagnostics.add(row_number, "cohort.id", "cohort_id_invalid")
                else:
                    cohort_ids.add(cohort_id)
                if not _valid_guid(tag_id):
                    diagnostics.add(row_number, "tag.id", "tag_id_invalid")
                else:
                    tag_ids.add(tag_id.lower())
                    previous_path = id_paths.setdefault(tag_id.lower(), tag_path)
                    if previous_path != tag_path:
                        diagnostics.add(row_number, "tag.path", "tag_path_inconsistent_for_id")
                    previous_id = path_ids.setdefault(tag_path, tag_id.lower())
                    if previous_id != tag_id.lower():
                        diagnostics.add(row_number, "tag.path", "tag_path_reused_for_different_id")
                if not _is_identifier(tag_path, MAX_TAG_PATH_CHARS):
                    diagnostics.add(row_number, "tag.path", "tag_path_invalid")

                quality = row["quality"]
                if quality not in QUALITIES:
                    diagnostics.add(row_number, "quality", "quality_unknown")
                else:
                    quality_counts[quality] += 1
                    if quality != "Good":
                        counts["non_good_rows"] += 1
                kind = row["value.kind"]
                kind_counts[kind if kind in VALUE_KINDS else "unknown"] += 1
                value_error = _validate_typed_value(kind, row["value"])
                if value_error:
                    diagnostics.add(row_number, "value", value_error)

                if not row["timestamp"]:
                    diagnostics.add(row_number, "timestamp", "observation_timestamp_required")
                elif _utc_ticks(row["timestamp"]) is None:
                    diagnostics.add(row_number, "timestamp", "timestamp_utc_invalid")

                source_status = row["source.timestamp.status"]
                source_counts[source_status if source_status in SOURCE_STATES else "unknown"] += 1
                source_ticks = _validate_timestamp_pair(
                    row,
                    "source.timestamp",
                    "source.timestamp.status",
                    SOURCE_STATES,
                    diagnostics,
                    row_number,
                )
                if not row["source.timestamp"]:
                    counts["missing_source_timestamp_rows"] += 1

                server_status = row["server.timestamp.status"]
                server_counts[server_status if server_status in SERVER_STATES else "unknown"] += 1
                _validate_timestamp_pair(
                    row,
                    "server.timestamp",
                    "server.timestamp.status",
                    SERVER_STATES,
                    diagnostics,
                    row_number,
                )

                capture_status = row["capture.policy.status"]
                capture_counts[capture_status if capture_status in CAPTURE_STATES else "unknown"] += 1
                profile_id = row["capture.policy.profileId"]
                fingerprint = row["capture.policy.fingerprint"]
                if capture_status not in CAPTURE_STATES:
                    diagnostics.add(row_number, "capture.policy.status", "capture_policy_status_unknown")
                elif capture_status in {"NotRecorded", "LegacyDefault"}:
                    if profile_id or fingerprint:
                        diagnostics.add(row_number, "capture.policy.status", "capture_policy_legacy_metadata_present")
                else:
                    if profile_id and not _valid_guid(profile_id):
                        diagnostics.add(row_number, "capture.policy.profileId", "capture_policy_profile_id_invalid")
                    if not fingerprint.startswith(FINGERPRINT_PREFIX) or not FINGERPRINT_RE.fullmatch(
                        fingerprint[len(FINGERPRINT_PREFIX) :]
                    ):
                        diagnostics.add(row_number, "capture.policy.fingerprint", "capture_policy_fingerprint_invalid")

                if (
                    source_ticks is not None
                    and _is_identifier(export_id)
                    and _valid_guid(tag_id)
                    and _is_identifier(cohort_id)
                ):
                    duplicate_keys[(export_id, cohort_id, tag_id.lower(), source_ticks)] += 1
        except csv.Error as error:
            reason = "csv_field_limit_exceeded" if "field larger than field limit" in str(error) else "csv_malformed"
            return _safe_error_report(reason), 4 if reason == "csv_field_limit_exceeded" else 2
        except (OverflowError, MemoryError):
            return _safe_error_report("resource_limit_exceeded"), 4

        duplicate_groups = sum(1 for occurrences in duplicate_keys.values() if occurrences > 1)
        duplicate_rows = sum(occurrences for occurrences in duplicate_keys.values() if occurrences > 1)
        counts["cohorts"] = len(cohort_ids)
        counts["tags"] = len(tag_ids)
        counts["duplicate_source_time_groups"] = duplicate_groups
        counts["duplicate_source_time_rows"] = duplicate_rows
        report["counts"] = dict(counts)
        report["quality_counts"] = dict(sorted(quality_counts.items()))
        report["value_kind_counts"] = dict(sorted(kind_counts.items()))
        report["source_timestamp_status_counts"] = dict(sorted(source_counts.items()))
        report["server_timestamp_status_counts"] = dict(sorted(server_counts.items()))
        report["capture_policy_status_counts"] = dict(sorted(capture_counts.items()))
        report["diagnostics"] = diagnostics.as_dict()
        report["structure_status"] = "malformed" if structural_error else "structurally_valid"
        report["content_status"] = "not_assessed" if structural_error else (
            "no_rows" if counts["data_rows"] == 0 else ("row_errors" if diagnostics.total else "no_row_errors")
        )
        return report, 2 if structural_error else (3 if diagnostics.total else 0)
    finally:
        csv.field_size_limit(old_limit)


def _inside_git_repository(path: Path) -> bool:
    for parent in (path, *path.parents):
        marker = parent / ".git"
        if marker.exists():
            return True
    return False


def _write_report_no_overwrite(path_text: str, input_path: Path, report: dict[str, Any]) -> None:
    target = Path(path_text).expanduser()
    if not target.is_absolute():
        target = Path.cwd() / target
    try:
        parent = target.parent.resolve(strict=True)
        resolved_target = parent / target.name
        if resolved_target.resolve(strict=False) == input_path.resolve(strict=True):
            raise PreflightFailure(5, "report_path_is_input")
        if _inside_git_repository(parent):
            raise PreflightFailure(5, "report_path_inside_git_repository")
    except PreflightFailure:
        raise
    except OSError:
        raise PreflightFailure(5, "report_path_invalid") from None
    try:
        descriptor = os.open(resolved_target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError:
        raise PreflightFailure(5, "report_path_exists") from None
    except OSError:
        raise PreflightFailure(5, "report_path_unavailable") from None
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(report, stream, ensure_ascii=False, indent=2, sort_keys=True)
            stream.write("\n")
    except OSError:
        try:
            os.close(descriptor)
        except OSError:
            pass
        raise PreflightFailure(5, "report_write_failed") from None


def _read_input(path_text: str) -> tuple[Path, bytes]:
    path = Path(path_text).expanduser()
    try:
        resolved = path.resolve(strict=True)
        if not resolved.is_file():
            raise PreflightFailure(5, "input_not_regular_file")
        if resolved.stat().st_size > MAX_INPUT_BYTES:
            raise PreflightFailure(4, "input_byte_limit_exceeded")
        with resolved.open("rb") as stream:
            data = stream.read(MAX_INPUT_BYTES + 1)
    except PreflightFailure:
        raise
    except OSError:
        raise PreflightFailure(5, "input_unavailable") from None
    if len(data) > MAX_INPUT_BYTES:
        raise PreflightFailure(4, "input_byte_limit_exceeded")
    return resolved, data


class _SafeArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:  # pragma: no cover - CLI edge
        del message
        raise PreflightFailure(5, "arguments_invalid")


def _parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    parser = _SafeArgumentParser(
        description="Offline bounded Stage 4F CSV structure preflight; no analytic/readiness verdict."
    )
    parser.add_argument("csv_file", help="Explicit local copy of the prepared Stage 4F CSV")
    parser.add_argument(
        "--report-out",
        help="Optional new report file outside any Git repository (never overwrites)",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    try:
        args = _parse_args(argv)
        input_path, data = _read_input(args.csv_file)
        report, exit_code = analyze_bytes(data)
        if args.report_out:
            _write_report_no_overwrite(args.report_out, input_path, report)
        else:
            json.dump(report, sys.stdout, ensure_ascii=False, indent=2, sort_keys=True)
            sys.stdout.write("\n")
        return exit_code
    except PreflightFailure as failure:
        json.dump(_safe_error_report(failure.reason), sys.stdout, ensure_ascii=False, indent=2, sort_keys=True)
        sys.stdout.write("\n")
        return failure.exit_code
    except Exception:
        # Keep an unexpected local/parser failure useful without surfacing a path,
        # input value, exception message, or traceback to the report channel.
        json.dump(_safe_error_report("internal_error"), sys.stdout, ensure_ascii=False, indent=2, sort_keys=True)
        sys.stdout.write("\n")
        return 5


if __name__ == "__main__":
    raise SystemExit(main())
