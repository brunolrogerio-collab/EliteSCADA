from __future__ import annotations

import dataclasses
import hashlib
import itertools
import json
import math
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from threading import Barrier

import bench as b


def row(**changes):
    base = b.generate_trace(7)[0]
    return dataclasses.replace(base, **changes)


def decision(index: int, above: bool, *, break_before: bool = False, eligible: bool = True) -> b.ScoreDecision:
    return b.ScoreDecision(index, index * b.CADENCE_S, 2.0 if above else 0.0, eligible, None, break_before)


def reference_episode_starts(values: tuple[bool, ...]) -> list[int]:
    """Small independent specification using direct five-value slices."""
    starts: list[int] = []
    active = False
    below_run = 0
    for index, above in enumerate(values):
        if active:
            if above:
                below_run = 0
            else:
                below_run += 1
                if below_run == 5:
                    active = False
                    below_run = 0
            continue
        window = values[max(0, index - 4):index + 1]
        if len(window) == 5 and sum(window) >= 3:
            starts.append(index)
            active = True
            below_run = 0
            for item in reversed(window):
                if item:
                    break
                below_run += 1
    return starts


class SyntheticBenchMechanismTests(unittest.TestCase):
    def test_only_named_pure_numerics_are_reused(self):
        expected = {
            "median", "fit_robust_scaling", "transform_rows", "covariance_matrix",
            "jacobi_eigenvalues", "rank_and_condition", "shrink_covariance", "cholesky",
            "mahalanobis_squared", "independent_max_abs", "nearest_rank_quantile",
        }
        self.assertEqual(set(b.REUSED_NUMERICAL_SYMBOLS), expected)
        self.assertNotIn("claim_final_holdout_once", b._NUM)
        self.assertNotIn("read_signal_cycles", b._NUM)

    def test_frozen_partition_is_contiguous_whole_group_and_seeds_are_unique(self):
        self.assertEqual((len(b.FIT_GROUPS), len(b.CAL_GROUPS), len(b.HOLDOUT_GROUPS)), (360, 120, 120))
        self.assertEqual([b.partition_for_group(i) for i in (0, 359, 360, 479, 480, 599)],
                         ["fit", "fit", "calibration", "calibration", "holdout", "holdout"])
        seeds = [b.derive_seed(i) for i in range(600)]
        self.assertEqual(len(seeds), len(set(seeds)))
        self.assertEqual(b.partition_digest(), b.partition_digest())
        b.validate_independent_groups(b.FIT_GROUPS, b.CAL_GROUPS, b.HOLDOUT_GROUPS)
        with self.assertRaises(ValueError):
            b.partition_for_group(600)
        with self.assertRaises(b.ResearchDataError):
            b.validate_independent_groups(b.FIT_GROUPS, b.CAL_GROUPS, b.HOLDOUT_GROUPS[:-21])

    def test_deterministic_generator_and_grouped_counterpart_labels(self):
        first = b.generate_trace(11)
        second = b.generate_trace(11)
        injected = b.generate_trace(11, "single-current-strong", (0.7, 1.0, 0.1))
        self.assertEqual(first, second)
        self.assertEqual(len(first), 120)
        self.assertEqual([x.source_time_s for x in first], list(range(0, 1200, 10)))
        self.assertEqual([x.group_id for x in first + injected], [11] * 240)
        for index in range(b.EVENT_START):
            self.assertEqual(first[index], injected[index])
        self.assertAlmostEqual(injected[12].targets[0] - first[12].targets[0], 3.5 * 0.7, places=12)
        self.assertEqual(b.partition_for_group(11), b.partition_for_group(injected[12].group_id))
        self.assertNotEqual(b.derive_seed(11), b.derive_seed(12))

    def test_context_changes_are_declared_and_expected_signal_is_finite(self):
        trace = b.generate_trace(19)
        loads = {item.load_fraction for item in trace}
        self.assertGreater(len(loads), 1)
        for item in trace:
            self.assertTrue(item.running)
            self.assertTrue(1430 <= item.speed_rpm <= 1470)
            self.assertTrue(0.25 <= item.load_fraction <= 0.80)
            self.assertTrue(all(math.isfinite(x) for x in item.targets))
            self.assertTrue(all(math.isfinite(x) for x in b._expected_targets(item.speed_rpm, item.load_fraction)))

    def test_predeclared_relationship_stimulus_preserves_preevent_pairing(self):
        base = b.generate_trace(3)
        changed = b.generate_trace(3, "relationship-strong", (0.7, 1.0, 0.1))
        self.assertEqual(base[:b.EVENT_START], changed[:b.EVENT_START])
        self.assertNotEqual(base[b.EVENT_START:], changed[b.EVENT_START:])
        self.assertEqual(b.R0[0][0], b.R1[0][0])
        self.assertEqual(b.R0[1][1], b.R1[1][1])
        self.assertLess(b.jacobi_eigenvalues(b.R1)[0], b.jacobi_eigenvalues(b.R0)[0])
        self.assertGreater(b.jacobi_eigenvalues(b.R1)[0], 0)

    def test_fit_helpers_reject_constant_scale_and_singular_covariance(self):
        with self.assertRaises(b.ResearchDataError):
            b.fit_robust_scaling([(1.0, 2.0), (1.0, 3.0), (1.0, 4.0)])
        singular = [[1.0, 1.0], [1.0, 1.0]]
        rank, condition = b.rank_and_condition(b.jacobi_eigenvalues(singular))
        self.assertEqual(rank, 1)
        self.assertIsNone(condition)
        with self.assertRaises(b.ResearchDataError):
            b.cholesky(singular)
        self.assertEqual(b._solve_linear([[2.0, 1.0], [1.0, 3.0]], [5.0, 6.0]), [1.8, 1.4])
        with self.assertRaises(b.ResearchDataError):
            b._solve_linear([[1.0, 1.0], [1.0, 1.0]], [2.0, 2.0])

    def test_invalid_and_insufficient_inputs_fail_closed(self):
        valid = b.generate_trace(5)
        variants = [
            (dataclasses.replace(valid[0], targets=(None, 1.0, 2.0)), "missing-required-target"),
            (dataclasses.replace(valid[0], running=None), "missing-required-context"),
            (dataclasses.replace(valid[0], quality="Unknown"), "quality-not-good"),
            (dataclasses.replace(valid[0], source_time_s=None), "missing-source-time"),
            (dataclasses.replace(valid[0], source_time_verified=False), "source-time-unverified"),
            (dataclasses.replace(valid[0], source_time_synchronized=False), "source-time-unsynchronized"),
            (dataclasses.replace(valid[0], source_time_s=1), "misaligned-source-time"),
            (dataclasses.replace(valid[0], context_id="other"), "context-mismatch"),
            (dataclasses.replace(valid[0], profile_version="stale"), "stale-profile-version"),
            (dataclasses.replace(valid[0], targets=(math.inf, 1.0, 2.0)), "nonfinite-target"),
            (dataclasses.replace(valid[0], load_fraction=0.9), "context-outside-profile"),
            (dataclasses.replace(valid[0], running=False), "context-not-running"),
        ]
        for invalid, expected_reason in variants:
            with self.subTest(reason=expected_reason):
                self.assertEqual(b.validate_trace([invalid])[0].reason, expected_reason)
                self.assertFalse(b.validate_trace([invalid])[0].eligible)
        self.assertEqual(b.validate_trace(valid, cadence_s=None)[0].reason, "unknown-cadence")
        duplicate_orders = [
            [valid[0], dataclasses.replace(valid[1], source_time_s=valid[0].source_time_s), valid[2]],
            [dataclasses.replace(valid[1], source_time_s=valid[0].source_time_s), valid[0], valid[2]],
        ]
        model = {
            "contextCoefficients": [[0.0, 0.0, 0.0] for _ in range(3)],
            "robustCenters": [0.0, 0.0, 0.0],
            "robustScales": [1.0, 1.0, 1.0],
            "covarianceMean": [0.0, 0.0, 0.0],
            "candidates": {"unit": {"status": "eligible", "kind": "independent"}},
        }
        for ordered_rows in duplicate_orders:
            with self.subTest(duplicate_order=[row.sample_index for row in ordered_rows]):
                eligibility = b.validate_trace(ordered_rows)
                self.assertEqual([item.reason for item in eligibility[:2]], ["duplicate-source-time"] * 2)
                self.assertFalse(any(item.eligible for item in eligibility[:2]))
                scored = b.score_trace(ordered_rows, model)["unit"]
                self.assertTrue(all(not item.eligible and item.score is None for item in scored[:2]))
        gap = b.validate_trace([valid[0], valid[2]])
        self.assertTrue(gap[1].eligible)
        self.assertTrue(gap[1].break_before)
        self.assertEqual(gap[1].reason, "missing-expected-sample")
        large_gap = [valid[0], dataclasses.replace(valid[1], sample_index=3, source_time_s=30)]
        self.assertEqual(b.validate_trace(large_gap)[1].reason, "gap-over-limit")

    def test_episode_confirmation_termination_and_interruptions(self):
        threshold = 1.0
        self.assertEqual(b.find_episodes([decision(i, True) for i in range(3)], threshold), [])
        self.assertEqual(b.find_episodes([decision(0, True), decision(1, True), decision(2, True), decision(3, False)], threshold), [])
        self.assertEqual(b.find_episodes([decision(0, True), decision(1, True), decision(2, False), decision(3, True), decision(4, False)], threshold), [4])

        after_gap = [decision(i, True) for i in range(4)] + [decision(4, True, break_before=True)] + [decision(i, True) for i in range(5, 9)]
        self.assertEqual(b.find_episodes(after_gap, threshold), [8])
        after_context_change = [decision(i, True) for i in range(4)] + [decision(4, True, break_before=True)] + [decision(i, True) for i in range(5, 10)]
        self.assertEqual(b.find_episodes(after_context_change, threshold), [8])
        after_ineligible = [decision(i, True) for i in range(4)] + [decision(4, True, eligible=False)] + [decision(i, True) for i in range(5, 10)]
        self.assertEqual(b.find_episodes(after_ineligible, threshold), [9])

        pattern = [decision(i, True) for i in range(5)] + [decision(i, False) for i in range(5, 10)] + [decision(i, True) for i in range(10, 15)]
        self.assertEqual(b.find_episodes(pattern, threshold), [4, 12])
        self.assertEqual(b._trace_summary([decision(i, True) for i in range(12, 17)], 1.0, 12)["latencySeconds"], 40)

    def test_episode_termination_preserves_window_for_rearm(self):
        threshold = 1.0
        ttttt_fffff_ttt = [True] * 5 + [False] * 5 + [True] * 3
        ttt_fffff_ttt = [True] * 3 + [False] * 5 + [True] * 3
        self.assertEqual(b.find_episodes([decision(i, value) for i, value in enumerate(ttttt_fffff_ttt)], threshold), [4, 12])
        self.assertEqual(b.find_episodes([decision(i, value) for i, value in enumerate(ttt_fffff_ttt)], threshold), [4, 10])

    def test_episode_matches_independent_oracle_for_short_boolean_sequences(self):
        threshold = 1.0
        checked = 0
        for length in range(14):
            for values in itertools.product((False, True), repeat=length):
                with self.subTest(values=values):
                    actual = b.find_episodes([decision(i, value) for i, value in enumerate(values)], threshold)
                    self.assertEqual(actual, reference_episode_starts(values))
                checked += 1
        self.assertEqual(checked, 16_383)

    def test_quantile_and_calibration_tie_break_are_fixed(self):
        self.assertEqual(b.nearest_rank_quantile([4.0, 1.0, 3.0, 2.0], 0.75), 3.0)
        candidates = {f"joint-{value:.2f}": {"status": "eligible", "shrinkage": value} for value in b.LAMBDA_GRID}
        model = {"candidates": candidates}
        metrics = {}
        for key in candidates:
            metrics[key] = {
                "pointFalsePositiveRate": 0.005, "falseEpisodesPer24h": 0.5,
                "classes": {name: {"recall": 0.95, "within10MinuteFraction": 0.95} for name in b.STRONG_CLASSES},
            }
        selected, feasible, _ = b._candidate_selection(metrics, model)
        self.assertTrue(feasible)
        self.assertEqual(selected, "joint-0.05")
        metrics["joint-0.05"]["classes"]["relationship-strong"]["recall"] = 0.50
        selected, feasible, _ = b._candidate_selection(metrics, model)
        self.assertTrue(feasible)
        self.assertEqual(selected, "joint-0.10")
        for key in candidates:
            metrics[key]["classes"]["relationship-strong"]["recall"] = 0.50
        selected, feasible, _ = b._candidate_selection(metrics, model)
        self.assertFalse(feasible)
        self.assertEqual(selected, "joint-0.05")

    def test_hash_bound_config_detects_mutation_and_platform_tolerance_is_explicit(self):
        manifest = b._manifest()
        payload = {
            "protocolSha256": manifest["protocolSha256"],
            "manifestCanonicalSha256": b._sha256_json(manifest),
            "benchSourceSha256": b._sha256_text(Path(b.__file__)),
            "testSourceSha256": b._sha256_text(b.TEST_SOURCE),
            "numericSourceSha256": b._sha256_text(b.OLD_NUMERIC_SOURCE),
            "partitionSha256": b.partition_digest(),
            "selectedJointCandidate": "joint-0.05",
            "fitParameter": 1.0,
            "thresholdMetric": 10.0,
        }
        payload["configurationSha256"] = b._sha256_json(payload)
        b._verify_configuration(payload)
        with tempfile.TemporaryDirectory() as directory:
            left = dict(payload)
            right = dict(payload)
            right["fitParameter"] += 5e-11
            right.pop("configurationSha256")
            right["configurationSha256"] = b._sha256_json(right)
            lp, rp = Path(directory) / "left.json", Path(directory) / "right.json"
            lp.write_bytes(b._canonical_json(left))
            rp.write_bytes(b._canonical_json(right))
            comparison = b.compare_fit_calibration(lp, rp)
            self.assertIn("WITHIN_PREDECLARED_TOLERANCES", comparison["status"])
            right["fitParameter"] += 5e-9
            right.pop("configurationSha256")
            right["configurationSha256"] = b._sha256_json(right)
            rp.write_bytes(b._canonical_json(right))
            with self.assertRaisesRegex(RuntimeError, "tolerance exceeded"):
                b.compare_fit_calibration(lp, rp)

    def test_identity_claim_rejects_second_scratch_before_generator(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "canonical-reservations"
            identity = "UNIT-SECOND-SCRATCH-NOT-FINAL-HOLDOUT"
            receipt = {"identity": identity, "state": "reserved"}
            generator_calls = []

            def attempt(scratch_name):
                scratch = Path(directory) / scratch_name
                return b._claim_then_generate(
                    scratch / "receipt.json", receipt,
                    lambda: generator_calls.append(scratch_name) or "generated",
                    reservation_root=root,
                )

            self.assertEqual(attempt("scratch-a"), "generated")
            with self.assertRaisesRegex(RuntimeError, "already consumed or reserved"):
                attempt("scratch-b")
            self.assertEqual(generator_calls, ["scratch-a"])
            self.assertTrue((root / f"{hashlib.sha256(identity.encode()).hexdigest()}.json").exists())
            self.assertTrue((Path(directory) / "scratch-a" / "receipt.json").exists())
            self.assertFalse((Path(directory) / "scratch-b" / "receipt.json").exists())

    def test_identity_claim_allows_only_one_concurrent_scratch_contender(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "canonical-reservations"
            identity = "UNIT-CONCURRENT-CONTENDER-NOT-FINAL-HOLDOUT"
            receipt = {"identity": identity, "state": "reserved"}
            generator_calls = []
            start_together = Barrier(2)

            def attempt(index):
                scratch = Path(directory) / f"scratch-{index}"
                start_together.wait(timeout=5)
                try:
                    return b._claim_then_generate(
                        scratch / "receipt.json", receipt,
                        lambda: generator_calls.append(index) or "generated",
                        reservation_root=root,
                    )
                except RuntimeError:
                    return "rejected"

            with ThreadPoolExecutor(max_workers=2) as pool:
                results = list(pool.map(attempt, (1, 2)))
            self.assertCountEqual(results, ["generated", "rejected"])
            self.assertEqual(len(generator_calls), 1)

    def test_committed_unit_receipt_rejects_before_generator_or_reservation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "canonical-reservations"
            scratch = Path(directory) / "scratch"
            committed = Path(directory) / "committed-unit-receipt.json"
            identity = "UNIT-COMMITTED-NOT-FINAL-HOLDOUT"
            committed.write_bytes(b._canonical_json({"identity": identity, "state": "completed"}) + b"\n")
            generator_calls = []
            with self.assertRaisesRegex(RuntimeError, "already committed"):
                b._claim_then_generate(
                    scratch / "receipt.json", {"identity": identity, "state": "reserved"},
                    lambda: generator_calls.append("generated"),
                    reservation_root=root,
                    committed_receipt_path=committed,
                )
            self.assertEqual(generator_calls, [])
            self.assertFalse(root.exists())

    def test_historical_receipt_verifies_at_frozen_commit_and_blocks_current_hash_mismatch(self):
        with tempfile.TemporaryDirectory() as directory:
            frozen_head = "a" * 40
            identity = "UNIT-HISTORICAL-NOT-FINAL-HOLDOUT"
            file_bytes = {
                "docs/research/intelligence-stage4f-synthetic-bench/bench.py": b"historical bench source\n",
                "docs/research/intelligence-stage4f-synthetic-bench/test_bench.py": b"historical tests\n",
                "docs/research/intelligence-stage4f/research_harness.py": b"historical numerical source\n",
                "docs/research/intelligence-stage4f-synthetic-bench/protocol-v3.md": b"historical protocol\n",
                "docs/research/intelligence-stage4f-synthetic-bench/experiment-manifest-v1.json": b._canonical_json({"fixture": "manifest"}) + b"\n",
            }
            config = {"fixture": "config"}
            config["configurationSha256"] = b._sha256_json(config)
            file_bytes["docs/research/intelligence-stage4f-synthetic-bench/frozen-config-v1.json"] = b._canonical_json(config) + b"\n"
            protocol_sha = b._sha256_text_bytes(file_bytes["docs/research/intelligence-stage4f-synthetic-bench/protocol-v3.md"])
            manifest_sha = b._sha256_json(json.loads(file_bytes["docs/research/intelligence-stage4f-synthetic-bench/experiment-manifest-v1.json"]))
            partition_sha = "partition-fixture-sha256"
            report = {
                "holdoutIdentity": identity,
                "experimentIdentity": "UNIT-EXPERIMENT",
                "frozenHead": frozen_head,
                "configurationSha256": config["configurationSha256"],
                "protocolSha256": protocol_sha,
                "partitionSha256": partition_sha,
                "fixture": "aggregate",
            }
            report["reportSha256"] = b._sha256_json(report)
            receipt = {
                "holdoutIdentity": identity,
                "experimentIdentity": report["experimentIdentity"],
                "frozenHead": frozen_head,
                "configurationSha256": config["configurationSha256"],
                "protocolSha256": protocol_sha,
                "partitionSha256": partition_sha,
                "manifestCanonicalSha256": manifest_sha,
                "benchSourceSha256": b._sha256_text_bytes(file_bytes["docs/research/intelligence-stage4f-synthetic-bench/bench.py"]),
                "testSourceSha256": b._sha256_text_bytes(file_bytes["docs/research/intelligence-stage4f-synthetic-bench/test_bench.py"]),
                "numericSourceSha256": b._sha256_text_bytes(file_bytes["docs/research/intelligence-stage4f/research_harness.py"]),
                "state": "completed",
                "aggregateReportSha256": report["reportSha256"],
            }
            with self.subTest("recorded-source-proof"):
                proof = b._verify_historical_holdout_record(
                    receipt, report,
                    read_committed_file=lambda revision, path: file_bytes[path],
                    current_bench_source=b"hardened bench source\n",
                    current_test_source=b"hardened test source\n",
                )
                self.assertTrue(proof["historicalEvidenceVerified"])
                self.assertFalse(proof["currentSourcesMatchHistorical"])

            report_path = Path(directory) / "aggregate.json"
            receipt_path = Path(directory) / "receipt.json"
            report_path.write_bytes(b._canonical_json(report) + b"\n")
            receipt_path.write_bytes(b._canonical_json(receipt) + b"\n")
            generator_calls = []
            with self.assertRaisesRegex(RuntimeError, "current bench/test source hashes differ"):
                b._reject_published_holdout(
                    report_path, receipt_path,
                    read_committed_file=lambda revision, path: file_bytes[path],
                    current_bench_source=b"hardened bench source\n",
                    current_test_source=b"hardened test source\n",
                )
                generator_calls.append("generated")
            self.assertEqual(generator_calls, [])

    def test_bootstrap_and_zero_episode_wilson_bound_are_deterministic(self):
        blocks = [{"fp": 1.0, "n": 100.0}, {"fp": 0.0, "n": 100.0}, {"fp": 2.0, "n": 100.0}]
        first = b._bootstrap_ci(blocks, "fp", "n", seed=123)
        second = b._bootstrap_ci(blocks, "fp", "n", seed=123)
        self.assertEqual(first, second)
        zero_bound = b.wilson_interval(0, 120)
        self.assertIsNotNone(zero_bound)
        self.assertGreater(zero_bound[1], 0.0)

    def test_peak_rss_measurement_is_available_on_current_platform(self):
        self.assertGreater(b._peak_rss_bytes(), 0)


if __name__ == "__main__":
    unittest.main()
