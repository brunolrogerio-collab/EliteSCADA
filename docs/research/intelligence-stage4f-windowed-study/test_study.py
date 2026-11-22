from __future__ import annotations

import ast
import math
import tempfile
import unittest
from pathlib import Path

import study


def _reference3() -> study.Reference:
    return study.Reference(
        coefficients=((0.0, 0.0, 0.0),) * 3,
        target_centers=(0.0, 0.0, 0.0),
        target_scales=(study.MAD_NORMAL_SCALE,) * 3,
        reference_correlations=((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)),
        component_centers=(0.0, 0.0, 0.0),
        component_scales=(1.0, 1.0, 1.0),
        design_rank=3,
        design_condition=1.0,
    )


def _fixture_rows(count: int, *, group: int = 2) -> tuple[study.Cohort, ...]:
    rows = []
    for index in range(count):
        targets = (
            float(index % 7 - 3),
            float(index % 11 - 5),
            float(index * 3 % 13 - 6),
        )
        rows.append(study.Cohort(
            group_id=group,
            scenario="nonfault-context",
            index=index,
            source_time_s=index * study.CADENCE_S,
            targets=targets,
            speed_rpm=1450.0,
            load_fraction=0.5,
            source_id=f"UNIT-group-{group}",
        ))
    return tuple(rows)


def _three_column_window() -> tuple[tuple[float, ...], ...]:
    return tuple((float(i - 14), float((i * 7) % 31 - 15), float((i * 11) % 29 - 14)) for i in range(30))


class StudyMechanicsTests(unittest.TestCase):
    def test_partition_and_unit_seed_determinism(self) -> None:
        study.audit_group_partition()
        self.assertEqual(study.derive_seed(4, study.UNIT_NAMESPACE), study.derive_seed(4, study.UNIT_NAMESPACE))
        self.assertNotEqual(study.derive_seed(4, study.UNIT_NAMESPACE), study.derive_seed(5, study.UNIT_NAMESPACE))
        with self.assertRaises(study.StudyError):
            study.derive_seed(240)

    def test_generator_is_repeatable_and_scenarios_share_pre_onset_noise(self) -> None:
        stream = study.independent_stream(7, study.UNIT_NAMESPACE)
        again = study.independent_stream(7, study.UNIT_NAMESPACE)
        self.assertEqual(stream, again)
        baseline = study.generate_run(7, "nonfault-context", independent=stream, namespace=study.UNIT_NAMESPACE)
        shifted = study.generate_run(7, "current-plus-3-fit-scales", (2.0, 3.0, 0.5), independent=stream, namespace=study.UNIT_NAMESPACE)
        self.assertEqual(len(baseline), 120)
        for index in range(study.EVENT_START):
            self.assertEqual(baseline[index].targets, shifted[index].targets)
        for index in range(study.EVENT_START, study.SAMPLES):
            self.assertAlmostEqual(shifted[index].targets[0] - baseline[index].targets[0], 6.0)
            self.assertAlmostEqual(shifted[index].targets[1], baseline[index].targets[1])

    def test_context_and_target_expectations_are_fixed(self) -> None:
        speed, load = study.context_for(0, 0)
        self.assertEqual((speed, load), (1430.0, 0.25))
        self.assertEqual(study.context_for(0, 30)[1], 0.35)
        self.assertEqual(study.expected_targets(1450.0, 0.5), (14.0, 18.0, 2.7))

    def test_formula_hand_check_for_p2(self) -> None:
        first = [-1.0] * 15 + [1.0] * 15
        second = [-2.0] * 15 + [2.0] * 15
        rows = tuple((first[i], second[i]) for i in range(30))
        ref = study.Reference(
            coefficients=((0.0,), (0.0,)),
            target_centers=(0.5 * study.MAD_NORMAL_SCALE, 0.0),
            target_scales=(study.MAD_NORMAL_SCALE, 2.0 * study.MAD_NORMAL_SCALE),
            reference_correlations=((1.0, -1.0), (-1.0, 1.0)),
        )
        level, dispersion, relationship = study.window_components(rows, ref)
        self.assertAlmostEqual(level, 0.5)
        self.assertAlmostEqual(dispersion, 0.0)
        self.assertAlmostEqual(relationship, 8.0)

    def test_nearest_rank_rule_and_threshold_comparison_are_strict(self) -> None:
        self.assertEqual(study.nearest_rank((1.0, 2.0, 3.0, 4.0), 0.75), 3.0)
        rows = _fixture_rows(30)
        reference = _reference3()
        probe = study.score_run(rows, reference)
        self.assertEqual(len(probe.scores), 1)
        equality = study.score_run(rows, reference, threshold=probe.scores[0])
        self.assertEqual(equality.exceedances, 0)
        self.assertEqual(equality.episode_confirmations, ())

    def test_p2_and_p8_finite_mechanics_and_degenerate_fail_closed(self) -> None:
        for width in (2, 8):
            rows = tuple(tuple(float(((i * (j + 3)) % (17 + j)) - (8 + j // 2)) for j in range(width)) for i in range(30))
            centers = tuple(study.median([row[j] for row in rows]) for j in range(width))
            scales = tuple(study.MAD_NORMAL_SCALE * study.mad([row[j] for row in rows])[1] for j in range(width))
            corr = study._pearson_columns(rows)
            ref = study.Reference((), centers, scales, corr)
            components = study.window_components(rows, ref)
            self.assertIsNotNone(components)
            self.assertTrue(all(math.isfinite(value) for value in components))
        flat = tuple((1.0, 1.0) for _ in range(30))
        ref = study.Reference((), (0.0, 0.0), (1.0, 1.0), ((1.0, 0.0), (0.0, 1.0)))
        self.assertIsNone(study.window_components(flat, ref))
        self.assertIsNone(study.window_components(flat[:-1], ref))

    def test_relationship_flip_is_not_marginal_dispersion_change(self) -> None:
        values = [-1.0] * 15 + [1.0] * 15
        positive = tuple((x, x) for x in values)
        flipped = tuple((x, -x) for x in values)
        doubled = tuple((2.0 * x, 2.0 * y) for x, y in positive)
        ref = study.Reference(
            (), (0.0, 0.0), (study.MAD_NORMAL_SCALE, study.MAD_NORMAL_SCALE),
            ((1.0, 1.0), (1.0, 1.0)),
        )
        baseline = study.window_components(positive, ref)
        relation = study.window_components(flipped, ref)
        dispersion = study.window_components(doubled, ref)
        self.assertAlmostEqual(baseline[2], 0.0)
        self.assertAlmostEqual(relation[0], baseline[0])
        self.assertAlmostEqual(relation[1], baseline[1])
        self.assertAlmostEqual(relation[2], 8.0)
        self.assertAlmostEqual(dispersion[0], baseline[0])
        self.assertAlmostEqual(dispersion[2], baseline[2])
        self.assertAlmostEqual(dispersion[1], math.log(2.0))

    def test_positive_affine_units_and_signal_order_invariance(self) -> None:
        rows = _three_column_window()
        columns = [[row[j] for row in rows] for j in range(3)]
        centers = tuple(study.median(column) for column in columns)
        scales = tuple(study.MAD_NORMAL_SCALE * study.mad(column)[1] for column in columns)
        correlation = study._pearson_columns(rows)
        ref = study.Reference((), centers, scales, correlation)
        baseline = study.window_components(rows, ref)
        multipliers = (0.1, 1000.0, 2.5)
        offsets = (14.0, -80.0, 0.75)
        transformed = tuple(tuple(multipliers[j] * row[j] + offsets[j] for j in range(3)) for row in rows)
        transformed_ref = study.Reference(
            (), tuple(multipliers[j] * centers[j] + offsets[j] for j in range(3)),
            tuple(multipliers[j] * scales[j] for j in range(3)), correlation,
        )
        changed_units = study.window_components(transformed, transformed_ref)
        self.assertEqual(len(baseline), 3)
        for left, right in zip(baseline, changed_units, strict=True):
            self.assertAlmostEqual(left, right, places=10)
        order = (2, 0, 1)
        permuted_rows = tuple(tuple(row[j] for j in order) for row in rows)
        permuted_ref = study.Reference(
            (), tuple(centers[j] for j in order), tuple(scales[j] for j in order),
            tuple(tuple(correlation[i][j] for j in order) for i in order),
        )
        permuted = study.window_components(permuted_rows, permuted_ref)
        for left, right in zip(baseline, permuted, strict=True):
            self.assertAlmostEqual(left, right, places=10)

    def test_outlier_and_nonlinear_relationship_limitations_are_visible(self) -> None:
        values = [-1.0] * 15 + [1.0] * 15
        baseline = tuple((x, x) for x in values)
        contaminated = list(baseline)
        contaminated[-1] = (1.0, 1_000_000.0)
        self.assertEqual(study.mad([row[1] for row in baseline]), study.mad([row[1] for row in contaminated]))
        base_corr = study._pearson_columns(baseline)[0][1]
        outlier_corr = study._pearson_columns(contaminated)[0][1]
        self.assertGreater(abs(base_corr - outlier_corr), 0.5)
        symmetric = [-5.0, 5.0, 0.0] + [value for value in range(-4, 5) for _ in range(3)]
        self.assertEqual(len(symmetric), 30)
        nonlinear = tuple((float(x), float(x * x)) for x in symmetric)
        self.assertLess(abs(study._pearson_columns(nonlinear)[0][1]), 0.05)

    def test_ols_solver_and_fit_partition_guard(self) -> None:
        solution = study.solve_linear(((2.0, 1.0), (1.0, 2.0)), (5.0, 4.0))
        self.assertAlmostEqual(solution[0], 2.0)
        self.assertAlmostEqual(solution[1], 1.0)
        eval_row = study.Cohort(60, "nonfault-context", 0, 0, (1.0, 2.0, 3.0), 1450.0, 0.5, source_id="UNIT")
        with self.assertRaises(study.StudyError):
            study.fit_contextual_reference((eval_row,))
        injected_fit_row = study.Cohort(0, "current-plus-3-fit-scales", 0, 0, (1.0, 2.0, 3.0), 1450.0, 0.5, source_id="UNIT")
        with self.assertRaises(study.StudyError):
            study.fit_contextual_reference((injected_fit_row,))

    def test_window_warmup_latency_and_strict_episode_boundary(self) -> None:
        summary = study.score_run(_fixture_rows(45), _reference3(), threshold=-1e9)
        self.assertEqual(summary.eligible_cohorts, 45)
        self.assertEqual(summary.warmup_cohorts, 29)
        self.assertEqual(summary.scored_decisions, 16)
        self.assertEqual(summary.episode_confirmations, (33,))
        self.assertEqual(study._latency(summary), 210)
        self.assertEqual(summary.scored_decisions * study.CADENCE_S, 160)
        self.assertEqual(summary.eligible_cohorts * study.CADENCE_S, 450)

    def test_episode_entry_termination_retains_history_and_rearms(self) -> None:
        tracker = study.EpisodeTracker()
        sequence = (True, False, True, False, True)
        confirmations = []
        for index, above in enumerate(sequence):
            confirmed, _ = tracker.step(above, index)
            if confirmed:
                confirmations.append(index)
        self.assertEqual(confirmations, [4])
        self.assertTrue(tracker.active)
        terminated = False
        for index in range(5, 10):
            _confirmed, ended = tracker.step(False, index)
            terminated = terminated or ended
        self.assertTrue(terminated)
        self.assertFalse(tracker.active)
        self.assertEqual(tuple(tracker.decisions), (False, False, False, False, False))
        confirmed_indices = []
        for index in range(10, 13):
            confirmed, _ = tracker.step(True, index)
            if confirmed:
                confirmed_indices.append(index)
        self.assertEqual(confirmed_indices, [12])

    def test_gaps_ineligible_rows_and_duplicates_reset_the_window(self) -> None:
        rows = list(_fixture_rows(71))
        gap_rows = rows[:40] + rows[41:]
        gap = study.score_run(gap_rows, _reference3(), threshold=-1e9)
        self.assertGreater(gap.reset_count, 0)
        self.assertEqual(gap.scored_decisions, 12)

        bad = list(_fixture_rows(71))
        bad[40] = study.replace(bad[40], quality="Bad")
        ineligible = study.score_run(bad, _reference3(), threshold=-1e9)
        self.assertEqual(ineligible.ineligible_reasons["quality-not-good"], 1)
        self.assertEqual(ineligible.scored_decisions, 12)

        duplicate = list(_fixture_rows(35))
        duplicate.insert(6, duplicate[5])
        duplicated = study.score_run(duplicate, _reference3(), threshold=-1e9)
        self.assertEqual(duplicated.ineligible_reasons["duplicate-source-key"], 2)
        self.assertEqual(duplicated.scored_decisions, 0)

    def test_numerical_degeneracy_fails_closed(self) -> None:
        flat = tuple(study.Cohort(
            group_id=3,
            scenario="nonfault-context",
            index=index,
            source_time_s=index * study.CADENCE_S,
            targets=(1.0, 1.0, 1.0),
            speed_rpm=1450.0,
            load_fraction=0.5,
            source_id="UNIT-flat",
        ) for index in range(30))
        result = study.score_run(flat, _reference3(), threshold=0.0)
        self.assertEqual(result.scored_decisions, 0)
        self.assertEqual(result.numerical_no_score, 1)

    def test_raw_query_budget_counts_before_filtering(self) -> None:
        self.assertEqual(study.validate_query_budget(7200, 120, 3, 3), 43920)
        with self.assertRaises(study.StudyError):
            study.validate_query_budget(7200, 120, 8, 3)
        with self.assertRaises(study.StudyError):
            study.validate_query_budget(7200, 120, 3, 3, extra_raw_rows=6081)
        with self.assertRaises(study.StudyError):
            study.validate_query_budget(7200, 120, 3, 3, window_days=32)
        with self.assertRaises(study.StudyError):
            study.validate_query_budget(7200, 120, 3, 3, outputs=501)

    def test_bootstrap_indices_are_reproducible_and_group_sized(self) -> None:
        first = study._bootstrap_resamples(5)
        second = study._bootstrap_resamples(5)
        self.assertEqual(len(first), study.BOOTSTRAP_REPLICATES)
        self.assertEqual(first, second)
        self.assertTrue(all(len(rep) == 5 and all(0 <= item < 5 for item in rep) for rep in first))

    def test_claim_is_atomic_interrupt_persistent_and_receipt_refuses_replay(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            state = root / "claims"
            receipt = root / "receipt.json"
            path = study.claim_final_holdout_once(state, "UNIT-disposable-claim", {"code": "unit"}, receipt)
            self.assertTrue(path.exists())
            with self.assertRaises(study.StudyError):
                study.claim_final_holdout_once(state, "UNIT-disposable-claim", {"code": "unit"}, receipt)
            self.assertTrue(path.exists(), "interrupted claim must remain as a hard stop")

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            receipt = root / "already-committed.json"
            receipt.write_text("{}", encoding="utf-8")
            with self.assertRaises(study.StudyError):
                study.claim_final_holdout_once(root / "claims", "UNIT-receipt-replay", {"unit": True}, receipt)
            self.assertFalse((root / "claims" / f"{study._identity_hash('UNIT-receipt-replay')}.json").exists())

    def test_atomic_artifact_create_refuses_overwrite(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "unit-artifact.json"
            study._atomic_new_bytes(path, b"first")
            with self.assertRaises(study.StudyError):
                study._atomic_new_bytes(path, b"second")
            self.assertEqual(path.read_bytes(), b"first")

    def test_holdout_claim_precedes_any_study_generation(self) -> None:
        tree = ast.parse((Path(study.__file__).parent / "study.py").read_text(encoding="utf-8"))
        function = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == "run_final_holdout")
        claim_call = next(node for node in ast.walk(function) if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "claim_final_holdout_once")
        generate_call = next(node for node in ast.walk(function) if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "generate_run")
        self.assertLess(claim_call.lineno, generate_call.lineno)


if __name__ == "__main__":
    unittest.main()
