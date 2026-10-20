"""Synthetic-only tests for the bounded Stage 4F input preflight."""

from __future__ import annotations

import csv
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import preflight


TAG_A = "11111111-1111-4111-8111-111111111111"
TAG_B = "22222222-2222-4222-8222-222222222222"
EXPORT = "synthetic-export-a"
COHORT = "synthetic-cohort-a"
OBSERVED = "2026-01-02T03:04:05.1234567Z"
SOURCE = "2026-01-02T03:04:05.1234567+00:00"


def record(**updates: str) -> dict[str, str]:
    result = {
        "export.id": EXPORT,
        "cohort.id": COHORT,
        "tag.id": TAG_A,
        "tag.path": "Plant.Test.Value",
        "quality": "Good",
        "value.kind": "int16",
        "value": "0",
        "timestamp": OBSERVED,
        "source.timestamp": SOURCE,
        "source.timestamp.status": "PresentUnverified",
        "server.timestamp": "",
        "server.timestamp.status": "NotProvided",
        "capture.policy.profileId": "",
        "capture.policy.fingerprint": "",
        "capture.policy.status": "LegacyDefault",
    }
    result.update(updates)
    return result


def csv_bytes(records: list[dict[str, str]], *, header: tuple[str, ...] | None = None) -> bytes:
    output = io.StringIO(newline="")
    writer = csv.writer(output, lineterminator="\r\n")
    columns = header or preflight.EXPECTED_COLUMNS
    writer.writerow(columns)
    for item in records:
        writer.writerow([item.get(column, "") for column in columns])
    return output.getvalue().encode("utf-8")


class PreflightContractTests(unittest.TestCase):
    def analyze(self, records: list[dict[str, str]], **kwargs):
        return preflight.analyze_bytes(csv_bytes(records, **kwargs))

    def reasons(self, report: dict) -> set[str]:
        return set(report["diagnostics"]["reason_counts"])

    def test_header_only_template_is_structural_not_readiness(self):
        report, code = preflight.analyze_bytes(csv_bytes([]))
        self.assertEqual(code, 0)
        self.assertEqual(report["content_status"], "no_rows")
        self.assertEqual(report["analytical_eligibility"], "NOT_ASSESSED")
        self.assertEqual(report["product_readiness"], "NOT_ASSESSED")
        self.assertTrue(all(value == "NOT_ASSESSED" for value in report["assessments"].values()))

    def test_bom_quoted_fields_and_empty_string_are_supported(self):
        item = record(**{"value.kind": "string", "value": "comma, quote \" and newline\nkept"})
        report, code = preflight.analyze_bytes(b"\xef\xbb\xbf" + csv_bytes([item]))
        self.assertEqual(code, 0)
        self.assertEqual(report["counts"]["data_rows"], 1)
        self.assertEqual(report["content_status"], "no_row_errors")
        self.assertNotIn(item["value"], json.dumps(report))

    def test_header_missing_unknown_duplicate_and_reordered(self):
        missing = tuple(column for column in preflight.EXPECTED_COLUMNS if column != "quality")
        report, code = preflight.analyze_bytes(csv_bytes([], header=missing))
        self.assertEqual(code, 2)
        self.assertIn("header_required_column_missing", self.reasons(report))

        unknown = tuple("secret-column" if column == "quality" else column for column in preflight.EXPECTED_COLUMNS)
        report, code = preflight.analyze_bytes(csv_bytes([], header=unknown))
        self.assertEqual(code, 2)
        self.assertIn("header_unknown_column", self.reasons(report))
        self.assertNotIn("secret-column", json.dumps(report))

        duplicate = list(preflight.EXPECTED_COLUMNS)
        duplicate[-1] = duplicate[-2]
        report, code = preflight.analyze_bytes(csv_bytes([], header=tuple(duplicate)))
        self.assertEqual(code, 2)
        self.assertIn("header_duplicate_column", self.reasons(report))

        reversed_header = tuple(reversed(preflight.EXPECTED_COLUMNS))
        report, code = preflight.analyze_bytes(csv_bytes([record()], header=reversed_header))
        self.assertEqual(code, 0)

    def test_malformed_quotes_and_row_width_are_structural_errors(self):
        report, code = preflight.analyze_bytes((",".join(preflight.EXPECTED_COLUMNS) + '\r\n"unterminated').encode())
        self.assertEqual(code, 2)
        self.assertIn("csv_malformed", self.reasons(report))

        short = csv_bytes([]) + b"synthetic,too,few\r\n"
        report, code = preflight.analyze_bytes(short)
        self.assertEqual(code, 2)
        self.assertEqual(report["structure_status"], "malformed")
        self.assertEqual(report["content_status"], "not_assessed")
        self.assertIn("csv_row_width_mismatch", self.reasons(report))

    def test_value_null_empty_zero_and_ranges(self):
        good_records = [
            record(**{"value.kind": "null", "value": ""}),
            record(**{"value.kind": "string", "value": ""}),
            record(**{"value.kind": "int16", "value": "0", "source.timestamp": "", "source.timestamp.status": "NotProvided"}),
        ]
        report, code = self.analyze(good_records)
        self.assertEqual(code, 0)
        self.assertEqual(report["counts"]["missing_source_timestamp_rows"], 1)
        self.assertEqual(report["value_kind_counts"], {"int16": 1, "null": 1, "string": 1})

        invalid = [
            record(**{"value.kind": "null", "value": "0"}),
            record(**{"value.kind": "int16", "value": "32768"}),
            record(**{"value.kind": "int32", "value": "1_000"}),
            record(**{"value.kind": "int64", "value": str(2**63)}),
            record(**{"value.kind": "float", "value": "1e100"}),
            record(**{"value.kind": "double", "value": "NaN"}),
            record(**{"value.kind": "number", "value": "Infinity"}),
            record(**{"value.kind": "boolean", "value": "True"}),
            record(**{"value.kind": "guid", "value": "not-a-guid"}),
            record(**{"value.kind": "dateTime", "value": "2026-01-02T03:04:05.12345678Z"}),
            record(**{"value.kind": "unknown", "value": "private-value"}),
        ]
        report, code = self.analyze(invalid)
        self.assertEqual(code, 3)
        for expected in (
            "null_value_must_be_empty",
            "value_integer_out_of_range",
            "value_integer_invalid",
            "value_number_not_finite",
            "value_boolean_invalid",
            "value_guid_invalid",
            "value_datetime_invalid",
            "value_kind_unknown",
        ):
            self.assertIn(expected, self.reasons(report))
        self.assertNotIn("private-value", json.dumps(report))

    def test_source_and_server_evidence_match_tag_value_contract(self):
        accepted = [
            record(**{"source.timestamp": "", "source.timestamp.status": "NotProvided"}),
            record(**{"source.timestamp": SOURCE, "source.timestamp.status": "NotRecorded"}),
            record(**{"server.timestamp": OBSERVED, "server.timestamp.status": "NotRecorded"}),
        ]
        report, code = self.analyze(accepted)
        self.assertEqual(code, 0)

        invalid = [
            record(**{"source.timestamp": SOURCE, "source.timestamp.status": "NotProvided"}),
            record(**{"source.timestamp": "", "source.timestamp.status": "Synchronized"}),
            record(**{"server.timestamp": OBSERVED, "server.timestamp.status": "NotProvided"}),
            record(**{"server.timestamp": OBSERVED, "server.timestamp.status": "Synchronized"}),
        ]
        report, code = self.analyze(invalid)
        self.assertEqual(code, 3)
        self.assertIn("timestamp_not_provided_but_present", self.reasons(report))
        self.assertIn("timestamp_required_by_evidence", self.reasons(report))
        self.assertIn("timestamp_status_not_allowed", self.reasons(report))

    def test_timestamps_preserve_seventh_fractional_digit_for_keys(self):
        base = "2026-01-02T03:04:05.123456"
        rows = [
            record(**{"source.timestamp": base + "1Z"}),
            record(**{"source.timestamp": base + "2Z"}),
            record(**{"source.timestamp": base + "2Z"}),
        ]
        report, code = self.analyze(rows)
        self.assertEqual(code, 0)
        self.assertEqual(report["counts"]["duplicate_source_time_groups"], 1)
        self.assertEqual(report["counts"]["duplicate_source_time_rows"], 2)
        self.assertNotIn(rows[0]["source.timestamp"], json.dumps(report))

    def test_present_source_times_still_participate_when_evidence_is_invalid(self):
        instant = "2026-01-02T03:04:05Z"
        rows = [
            record(**{"source.timestamp": instant, "source.timestamp.status": "NotProvided"}),
            record(**{"source.timestamp": instant, "source.timestamp.status": "PresentUnverified"}),
        ]
        report, code = self.analyze(rows)
        self.assertEqual(code, 3)
        self.assertEqual(report["counts"]["duplicate_source_time_groups"], 1)
        self.assertIn("timestamp_not_provided_but_present", self.reasons(report))

    def test_duplicate_scope_includes_export_cohort_and_tag(self):
        instant = "2026-01-02T03:04:05Z"
        rows = [
            record(**{"source.timestamp": instant}),
            record(**{"source.timestamp": instant}),
            record(**{"export.id": "synthetic-export-b", "source.timestamp": instant}),
            record(**{"cohort.id": "synthetic-cohort-b", "source.timestamp": instant}),
            record(**{"tag.id": TAG_B, "tag.path": "Plant.Test.Other", "source.timestamp": instant}),
        ]
        report, code = self.analyze(rows)
        self.assertEqual(code, 3)  # inconsistent export annotation remains visible as an error
        self.assertEqual(report["counts"]["duplicate_source_time_groups"], 1)
        self.assertEqual(report["counts"]["duplicate_source_time_rows"], 2)
        self.assertIn("export_id_inconsistent", self.reasons(report))

    def test_capture_policy_metadata_is_contract_consistent(self):
        good_fingerprint = preflight.FINGERPRINT_PREFIX + "a" * 32
        rows = [
            record(**{"capture.policy.status": "NotRecorded"}),
            record(**{"capture.policy.status": "LegacyDefault"}),
            record(
                **{
                    "capture.policy.status": "Explicit",
                    "capture.policy.profileId": "33333333-3333-4333-8333-333333333333",
                    "capture.policy.fingerprint": good_fingerprint,
                }
            ),
        ]
        report, code = self.analyze(rows)
        self.assertEqual(code, 0)

        invalid = [
            record(**{"capture.policy.status": "LegacyDefault", "capture.policy.fingerprint": good_fingerprint}),
            record(**{"capture.policy.status": "Explicit", "capture.policy.profileId": "not-guid", "capture.policy.fingerprint": "bad"}),
            record(**{"capture.policy.status": "unknown"}),
        ]
        report, code = self.analyze(invalid)
        self.assertEqual(code, 3)
        self.assertIn("capture_policy_legacy_metadata_present", self.reasons(report))
        self.assertIn("capture_policy_profile_id_invalid", self.reasons(report))
        self.assertIn("capture_policy_fingerprint_invalid", self.reasons(report))
        self.assertIn("capture_policy_status_unknown", self.reasons(report))

    def test_identifiers_and_tag_path_consistency_are_aggregate_only(self):
        secret = "confidential-Line-9"
        rows = [
            record(**{"tag.path": secret}),
            record(**{"tag.path": "other-path"}),
            record(**{"tag.id": TAG_B, "tag.path": secret}),
        ]
        report, code = self.analyze(rows)
        self.assertEqual(code, 3)
        self.assertIn("tag_path_inconsistent_for_id", self.reasons(report))
        self.assertIn("tag_path_reused_for_different_id", self.reasons(report))
        serialized = json.dumps(report)
        for private_text in (secret, TAG_A, EXPORT, COHORT, OBSERVED, SOURCE):
            self.assertNotIn(private_text, serialized)

    def test_invalid_declared_ids_paths_and_quality_are_diagnosed(self):
        report, code = self.analyze(
            [
                record(
                    **{
                        "export.id": " ",
                        "cohort.id": "\t",
                        "tag.id": "00000000-0000-0000-0000-000000000000",
                        "tag.path": "",
                        "quality": "GOOD",
                    }
                )
            ]
        )
        self.assertEqual(code, 3)
        self.assertTrue(
            {
                "export_id_invalid",
                "cohort_id_invalid",
                "tag_id_invalid",
                "tag_path_invalid",
                "quality_unknown",
            }.issubset(self.reasons(report))
        )

    def test_non_good_and_missing_provenance_rows_are_counted_not_dropped(self):
        rows = [
            record(**{"quality": "BadCommunication", "source.timestamp": "", "source.timestamp.status": "NotProvided"}),
            record(**{"quality": "Uncertain", "source.timestamp": "", "source.timestamp.status": "NotProvided"}),
        ]
        report, code = self.analyze(rows)
        self.assertEqual(code, 0)
        self.assertEqual(report["counts"]["data_rows"], 2)
        self.assertEqual(report["counts"]["non_good_rows"], 2)
        self.assertEqual(report["counts"]["missing_source_timestamp_rows"], 2)
        self.assertEqual(report["quality_counts"], {"BadCommunication": 1, "Uncertain": 1})

    def test_observation_and_source_times_must_be_explicit_utc(self):
        rows = [
            record(**{"timestamp": "", "source.timestamp": "2026-01-02T03:04:05+02:00"}),
            record(**{"timestamp": "2026-02-30T03:04:05Z", "source.timestamp": "2026-01-02T03:04:60Z"}),
        ]
        report, code = self.analyze(rows)
        self.assertEqual(code, 3)
        self.assertIn("observation_timestamp_required", self.reasons(report))
        self.assertIn("timestamp_utc_invalid", self.reasons(report))

    def test_diagnostic_cap_counts_all_reasons_without_values(self):
        marker = "raw-private-value-do-not-report"
        rows = [record(**{"value.kind": "unknown", "value": marker}) for _ in range(preflight.MAX_DIAGNOSTICS + 5)]
        report, code = self.analyze(rows)
        self.assertEqual(code, 3)
        self.assertEqual(report["diagnostics"]["total"], preflight.MAX_DIAGNOSTICS + 5)
        self.assertTrue(report["diagnostics"]["truncated"])
        self.assertEqual(len(report["diagnostics"]["items"]), preflight.MAX_DIAGNOSTICS)
        self.assertNotIn(marker, json.dumps(report))

    def test_utf8_resource_and_field_bounds(self):
        report, code = preflight.analyze_bytes(b"\xff\xfeinvalid")
        self.assertEqual(code, 2)
        self.assertIn("utf8_invalid", self.reasons(report))

        original = preflight.MAX_INPUT_BYTES
        try:
            preflight.MAX_INPUT_BYTES = 10
            report, code = preflight.analyze_bytes(b"x" * 11)
            self.assertEqual(code, 4)
            self.assertIn("input_byte_limit_exceeded", self.reasons(report))
        finally:
            preflight.MAX_INPUT_BYTES = original

        original_rows = preflight.MAX_DATA_ROWS
        try:
            preflight.MAX_DATA_ROWS = 1
            report, code = preflight.analyze_bytes(csv_bytes([record(), record(**{"tag.id": TAG_B})]))
            self.assertEqual(code, 4)
            self.assertEqual(report["structure_status"], "incomplete")
            self.assertEqual(report["counts"]["data_rows"], 1)
            self.assertIn("data_row_limit_exceeded", self.reasons(report))
        finally:
            preflight.MAX_DATA_ROWS = original_rows

        original_field = preflight.MAX_FIELD_CHARS
        try:
            preflight.MAX_FIELD_CHARS = 200
            report, code = preflight.analyze_bytes(
                csv_bytes([record(**{"value.kind": "string", "value": "x" * 201})])
            )
            self.assertEqual(code, 4)
            self.assertIn("csv_field_limit_exceeded", self.reasons(report))
        finally:
            preflight.MAX_FIELD_CHARS = original_field

    def test_report_file_is_explicit_new_and_never_in_repository(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            input_path = root / "input.csv"
            input_path.write_bytes(csv_bytes([record()]))
            output_path = root / "report.json"
            report, code = preflight.analyze_bytes(input_path.read_bytes())
            preflight._write_report_no_overwrite(str(output_path), input_path, report)
            self.assertEqual(json.loads(output_path.read_text())["content_status"], "no_row_errors")
            with self.assertRaises(preflight.PreflightFailure) as exists:
                preflight._write_report_no_overwrite(str(output_path), input_path, report)
            self.assertEqual(exists.exception.reason, "report_path_exists")
            with self.assertRaises(preflight.PreflightFailure) as collision:
                preflight._write_report_no_overwrite(str(input_path), input_path, report)
            self.assertEqual(collision.exception.reason, "report_path_is_input")

            repo = root / "fake-repo"
            repo.mkdir()
            (repo / ".git").mkdir()
            with self.assertRaises(preflight.PreflightFailure) as inside:
                preflight._write_report_no_overwrite(str(repo / "report.json"), input_path, report)
            self.assertEqual(inside.exception.reason, "report_path_inside_git_repository")
            self.assertFalse((repo / "report.json").exists())

    def test_cli_exit_codes_and_stdout_are_stable_and_redacted(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "synthetic.csv"
            source.write_bytes(
                csv_bytes([record(**{"value.kind": "string", "value": "do-not-print-this"})])
            )
            process = subprocess.run(
                [sys.executable, str(Path(preflight.__file__).resolve()), str(source)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(process.returncode, 0)
            output = json.loads(process.stdout)
            self.assertEqual(output["product_readiness"], "NOT_ASSESSED")
            self.assertNotIn("do-not-print-this", process.stdout)
            self.assertNotIn(str(source), process.stdout)

            explicit_report = Path(temp) / "new-report.json"
            process = subprocess.run(
                [
                    sys.executable,
                    str(Path(preflight.__file__).resolve()),
                    str(source),
                    "--report-out",
                    str(explicit_report),
                ],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(process.returncode, 0)
            self.assertEqual(process.stdout, "")
            saved_report = explicit_report.read_text(encoding="utf-8")
            self.assertEqual(json.loads(saved_report)["content_status"], "no_row_errors")
            process = subprocess.run(
                [
                    sys.executable,
                    str(Path(preflight.__file__).resolve()),
                    str(source),
                    "--report-out",
                    str(explicit_report),
                ],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(process.returncode, 5)
            self.assertEqual(json.loads(process.stdout)["diagnostics"]["reason_counts"], {"report_path_exists": 1})
            self.assertEqual(explicit_report.read_text(encoding="utf-8"), saved_report)

            missing = Path(temp) / "unavailable-sensitive-path.csv"
            process = subprocess.run(
                [sys.executable, str(Path(preflight.__file__).resolve()), str(missing)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(process.returncode, 5)
            self.assertNotIn(str(missing), process.stdout)
            self.assertEqual(json.loads(process.stdout)["structure_status"], "not_assessed")
            process = subprocess.run(
                [
                    sys.executable,
                    str(Path(preflight.__file__).resolve()),
                    str(missing),
                    "--report-out",
                    str(missing),
                ],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(process.returncode, 5)
            self.assertFalse(missing.exists())
            self.assertNotIn(str(missing), process.stdout)

            fake_repo = Path(temp) / "repo"
            fake_repo.mkdir()
            (fake_repo / ".git").mkdir()
            repo_report = fake_repo / "report.json"
            process = subprocess.run(
                [
                    sys.executable,
                    str(Path(preflight.__file__).resolve()),
                    str(source),
                    "--report-out",
                    str(repo_report),
                ],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(process.returncode, 5)
            self.assertFalse(repo_report.exists())
            self.assertEqual(
                json.loads(process.stdout)["diagnostics"]["reason_counts"],
                {"report_path_inside_git_repository": 1},
            )

            bad = Path(temp) / "bad.csv"
            bad.write_bytes(csv_bytes([record(**{"value.kind": "null", "value": "0"})]))
            process = subprocess.run(
                [sys.executable, str(Path(preflight.__file__).resolve()), str(bad)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(process.returncode, 3)


if __name__ == "__main__":
    unittest.main()
