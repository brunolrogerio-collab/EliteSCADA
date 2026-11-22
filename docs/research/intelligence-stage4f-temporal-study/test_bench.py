"""Small deterministic UNIT tests; never generate study holdout identities."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from dataclasses import replace
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bench


def unit_rows(group_id: int = 7) -> list[bench.Observation]:
    return bench.generate_run(
        group_id,
        namespace=bench.UNIT_NAMESPACE + "TESTS",
    )


def decisions(values: list[float | None], *, first_break: bool = True) -> list[bench.ScoreDecision]:
    result = []
    for index, value in enumerate(values):
        eligible = value is not None
        result.append(bench.ScoreDecision(
            sample_index=index,
            source_time_s=index * bench.CADENCE_S,
            score=value,
            eligible=eligible,
            reason=None if eligible else "unit-missing",
            break_before=(first_break and index == 0) or not eligible,
        ))
    return result


class ProtocolAndGeneratorTests(unittest.TestCase):
    def test_exact_partitions_seed_identity_and_candidate_grid(self) -> None:
        bench.validate_partitions()
        self.assertEqual(bench.FIT_GROUPS, tuple(range(60)))
        self.assertEqual(bench.CAL_GROUPS, tuple(range(60, 120)))
        self.assertEqual(bench.HOLDOUT_GROUPS, tuple(range(120, 240)))
        self.assertEqual(len({bench.derive_seed(group) for group in range(240)}), 240)
        self.assertEqual(len(bench.candidate_specs()), 8)
        self.assertEqual(len({row["name"] for row in bench.candidate_specs()}), 8)

    def test_seed_and_generator_are_repeatable_and_namespaced(self) -> None:
        first = unit_rows()
        second = unit_rows()
        other_namespace = bench.generate_run(7, namespace=bench.UNIT_NAMESPACE + "OTHER")
        self.assertEqual(first, second)
        self.assertNotEqual(first, other_namespace)
        self.assertEqual(first[0].sample_index, 0)
        self.assertEqual(first[-1].sample_index, bench.SAMPLES_PER_GROUP - 1)
        with self.assertRaises(ValueError):
            bench.generate_run(240)
        with self.assertRaises(ValueError):
            bench.generate_run(0, namespace="unapproved-unit-space")

    def test_scenario_counterpart_shares_noise_and_applies_frozen_delta(self) -> None:
        baseline = unit_rows(8)
        shifted = bench.generate_run(
            8,
            "single-current-strong",
            (2.0, 3.0, 4.0),
            namespace=bench.UNIT_NAMESPACE + "TESTS",
        )
        self.assertEqual(baseline[:bench.EVENT_START], shifted[:bench.EVENT_START])
        for index in range(bench.EVENT_START, bench.SAMPLES_PER_GROUP):
            self.assertAlmostEqual(shifted[index].targets[0] - baseline[index].targets[0], 7.0)
            self.assertAlmostEqual(shifted[index].targets[1], baseline[index].targets[1])
            self.assertAlmostEqual(shifted[index].targets[2], baseline[index].targets[2])

    def test_fixed_context_equations_and_generator_contract(self) -> None:
        row = unit_rows(4)[0]
        self.assertEqual(row.context_id, "pump-profile-v1")
        self.assertEqual(row.profile_version, "model-v1")
        expected = bench._expected_targets(row.speed_rpm, row.load_fraction)
        self.assertEqual(len(expected), 3)
        self.assertEqual(row.source_time_s, 0)
        self.assertEqual(row.quality, "Good")


class FitAndScoringTests(unittest.TestCase):
    def test_linear_solver_matches_hand_solution_and_rejects_singular(self) -> None:
        solution = bench._solve_linear([[3, 2], [2, 3]], [5, 5])
        self.assertAlmostEqual(solution[0], 1.0)
        self.assertAlmostEqual(solution[1], 1.0)
        with self.assertRaises(bench.ResearchDataError):
            bench._solve_linear([[1, 2], [2, 4]], [1, 2])

    def test_fit_reference_is_built_from_explicit_unit_rows_only(self) -> None:
        rows = [row for group in range(9) for row in unit_rows(group)]
        reference = bench.fit_reference_from_rows(rows)
        self.assertEqual(reference["fitVectorCount"], len(rows))
        self.assertEqual(reference["contextDesignRank"], 3)
        self.assertEqual(reference["regularizedRank"], 3)
        self.assertEqual(len(reference["robustScales"]), 3)
        self.assertTrue(all(value > 0 for value in reference["robustScales"]))
        self.assertEqual(len(reference["choleskyLower"]), 3)

    def test_ewma_initializes_and_resets_at_every_boundary(self) -> None:
        gates = [
            bench.Eligibility(True, break_before=True),
            bench.Eligibility(True),
            bench.Eligibility(True, break_before=True),
            bench.Eligibility(False, "bad-quality", True),
            bench.Eligibility(True, break_before=True),
        ]
        values = bench.apply_ewma([10, 10, 10, None, 10], gates, initialization=2.0, alpha=0.2)
        self.assertEqual(values[0], 3.6)
        self.assertAlmostEqual(values[1], 4.88)
        self.assertEqual(values[2], 3.6)
        self.assertIsNone(values[3])
        self.assertEqual(values[4], 3.6)
        with self.assertRaises(ValueError):
            bench.apply_ewma([1], gates[:0], initialization=0)

    def test_eligibility_invalidates_duplicate_and_resets_on_gap_or_context(self) -> None:
        rows = unit_rows(5)
        duplicate = [rows[0], replace(rows[1], source_time_s=rows[0].source_time_s)]
        gates = bench.validate_observations(duplicate)
        self.assertEqual([gate.reason for gate in gates], ["duplicate-source-time"] * 2)

        one_missing = [rows[0], rows[2]]
        gates = bench.validate_observations(one_missing)
        self.assertTrue(gates[0].eligible and gates[1].eligible)
        self.assertTrue(gates[1].break_before)
        self.assertEqual(gates[1].reason, "missing-expected-sample")

        context_change = [rows[0], replace(rows[1], context_id="other-profile")]
        gates = bench.validate_observations(context_change)
        self.assertFalse(gates[1].eligible)
        self.assertEqual(gates[1].reason, "context-mismatch")

        unknown_cadence = bench.validate_observations(rows[:2], cadence_s=None)
        self.assertTrue(all(not gate.eligible and gate.reason == "unknown-cadence" for gate in unknown_cadence))

    def test_threshold_nearest_rank_for_both_predeclared_quantiles(self) -> None:
        baseline = decisions([float(value) for value in range(1, 101)])
        input_rows = {f"{stat}-{form}": [baseline] for stat in bench.STATISTICS for form in bench.FORMS}
        thresholds = bench._candidate_thresholds(input_rows)
        self.assertEqual(thresholds["joint-instant-q099"], 99.0)
        self.assertEqual(thresholds["joint-instant-q0999"], 100.0)
        self.assertEqual(thresholds["marginal-ewma-q099"], 99.0)

    def test_episode_confirmation_termination_rearm_and_latency_oracle(self) -> None:
        # TTTFF confirms at index 4; the two trailing lows count toward the
        # five-low termination rule. Three new highs then rearm at index 10.
        points = decisions([2, 2, 2, 0, 0, 0, 0, 0, 2, 2, 2], first_break=True)
        starts = bench.find_episodes(points, threshold=1)
        self.assertEqual(starts, [4, 10])
        summary = bench.summarize_run(points, threshold=1, onset_index=5)
        self.assertEqual(summary["preOnsetEpisodes"], 1)
        self.assertTrue(summary["detected"])
        self.assertEqual(summary["latencySeconds"], 50)
        self.assertTrue(summary["within600Seconds"])

    def test_fit_only_and_model_validation_fail_closed(self) -> None:
        with self.assertRaises(bench.ResearchDataError):
            bench.fit_reference_from_rows([])
        rows = unit_rows(6)
        invalid = replace(rows[0], targets=(float("nan"), rows[0].targets[1], rows[0].targets[2]))
        with self.assertRaises(bench.ResearchDataError):
            bench.fit_reference_from_rows([invalid, *rows[1:10]])


class MetricsAndFreezeTests(unittest.TestCase):
    @staticmethod
    def _candidate_metrics(*, feasible: bool) -> dict[str, dict[str, object]]:
        result: dict[str, dict[str, object]] = {}
        for spec in bench.candidate_specs():
            nonfault = {
                "pointExceedanceRate": 0.005 if feasible else 0.02,
                "episodesPer24EligibleSimulatedHours": 0.5 if feasible else 2.0,
                "episodeCount": 0 if feasible else 2,
            }
            result[spec["name"]] = {"nonfault-context": nonfault}
            for scenario in bench.STRONG_SCENARIOS:
                result[spec["name"]][scenario] = {
                    "recall": 0.95 if feasible else 0.80,
                    "timelyFractionOfAllGroups": 0.92 if feasible else 0.75,
                }
        return result

    def test_aggregate_uses_group_denominators_and_wilson(self) -> None:
        records = [
            {"eligiblePoints": 10, "ineligiblePoints": 0, "pointExceedances": 1,
             "episodeCount": 0, "eligibleHours": 10 / 360,
             "ineligibleReasons": {}, "preOnsetEpisodes": 0,
             "latencySeconds": None, "detected": False, "within600Seconds": False},
            {"eligiblePoints": 10, "ineligiblePoints": 0, "pointExceedances": 0,
             "episodeCount": 1, "eligibleHours": 10 / 360,
             "ineligibleReasons": {}, "preOnsetEpisodes": 0,
             "latencySeconds": 20, "detected": True, "within600Seconds": True},
        ]
        metrics = bench._aggregate_group_records(records, include_event=True)
        self.assertEqual(metrics["independentSourceGroups"], 2)
        self.assertEqual(metrics["pointExceedanceRate"], 0.05)
        self.assertEqual(metrics["recall"], 0.5)
        self.assertEqual(metrics["timelyFractionOfAllGroups"], 0.5)
        self.assertEqual(metrics["latencySeconds"]["p50"], 20)
        self.assertIsNotNone(metrics["wilsonGroupOccurrence95"])

    def test_bootstrap_is_reproducible_and_cluster_records_are_whole_groups(self) -> None:
        records = []
        for detected, timely in ((False, False), (True, True), (True, False)):
            records.append({
                "eligiblePoints": 120, "pointExceedances": 1,
                "episodeCount": int(detected), "eligibleHours": 1 / 3,
                "detected": detected, "within600Seconds": timely,
                "latencySeconds": 30 if detected else None,
            })
        first = bench._bootstrap_intervals(records, include_event=True, seed=12345)
        second = bench._bootstrap_intervals(records, include_event=True, seed=12345)
        self.assertEqual(first, second)
        self.assertEqual(set(first), {
            "pointExceedanceRate", "episodesPer24EligibleSimulatedHours",
            "recall", "timelyFractionOfAllGroups", "meanLatencyDetectedOnly",
        })

    def test_candidate_selection_uses_frozen_tie_order_and_fallback(self) -> None:
        reference = {"regularizedCondition": 10.0}
        chosen = bench.select_calibration_candidate(self._candidate_metrics(feasible=True), reference)
        self.assertTrue(chosen["calibrationFeasible"])
        self.assertEqual(chosen["selectedCandidate"], "joint-instant-q099")
        fallback = bench.select_calibration_candidate(self._candidate_metrics(feasible=False), reference)
        self.assertFalse(fallback["calibrationFeasible"])
        self.assertEqual(fallback["selectedCandidate"], "joint-instant-q099")
        self.assertFalse(fallback["candidateCanBeCalledWinner"])

    def test_claim_is_atomic_once_and_receipt_is_a_hard_stop(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "identity-claims"
            result = bench._claim_identity_once("UNIT-ONE-USE-01", {"unit": True}, reservation_root=root)
            self.assertTrue(result.exists())
            with self.assertRaises(RuntimeError):
                bench._claim_identity_once("UNIT-ONE-USE-01", {"unit": True}, reservation_root=root)
            receipt = Path(temporary) / "receipt.json"
            receipt.write_text(json.dumps({"holdoutIdentity": "UNIT-ONE-USE-01"}), encoding="utf-8")
            with self.assertRaises(RuntimeError):
                bench._claim_identity_once(
                    "UNIT-ONE-USE-01", {"unit": True}, reservation_root=root,
                    committed_receipt_path=receipt,
                )

    def test_small_json_output_bound_is_enforced_without_overwrite(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "unit.json"
            bench._write_json(path, {"ok": True}, refuse_overwrite=True)
            with self.assertRaises(RuntimeError):
                bench._write_json(path, {"second": True}, refuse_overwrite=True)
            with self.assertRaises(RuntimeError):
                bench._write_json(Path(temporary) / "oversize.json", {"data": "x" * bench.MAX_COMMITTED_BYTES})


if __name__ == "__main__":
    unittest.main()
