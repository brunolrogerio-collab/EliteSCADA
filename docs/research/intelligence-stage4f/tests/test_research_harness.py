from __future__ import annotations

import json
import math
import multiprocessing
import sys
import tempfile
import unittest
import zipfile
from array import array
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from research_harness import (  # noqa: E402
    ResearchDataError,
    claim_final_holdout_once,
    cholesky,
    covariance_matrix,
    default_holdout_guard_directory,
    fit_robust_scaling,
    independent_max_abs,
    jacobi_eigenvalues,
    mahalanobis_squared,
    median,
    nearest_rank_quantile,
    peak_rss_bytes,
    read_signal_cycles,
    rank_and_condition,
    run_holdout,
    sha256_file,
    shrink_covariance,
    validate_partition_mapping,
    verify_configuration_digest,
)


def _claim_in_independent_process(directory: str, barrier, result_queue) -> None:
    try:
        barrier.wait(timeout=15)
        claim_final_holdout_once("A" * 64, "B" * 40, "C" * 40, Path(directory))
        result_queue.put("claimed")
    except ResearchDataError:
        result_queue.put("rejected")
    except BaseException as exc:  # pragma: no cover - relayed to parent assertion
        result_queue.put(f"error: {type(exc).__name__}: {exc}")


class ResearchHarnessTests(unittest.TestCase):
    def test_median_even_and_odd(self) -> None:
        self.assertEqual(median([4.0, 1.0, 3.0]), 3.0)
        self.assertEqual(median([4.0, 1.0, 3.0, 2.0]), 2.5)

    def test_zero_robust_scale_is_degenerate(self) -> None:
        with self.assertRaisesRegex(ResearchDataError, "zero or invalid robust scale"):
            fit_robust_scaling([(2.0, 1.0), (2.0, 3.0), (2.0, 4.0)])

    def test_covariance_and_jacobi_eigenvalues(self) -> None:
        covariance, _ = covariance_matrix([(0.0, 0.0), (1.0, 1.0), (2.0, 2.0)])
        eigenvalues = jacobi_eigenvalues(covariance)
        self.assertAlmostEqual(eigenvalues[0], 0.0, delta=1e-12)
        self.assertAlmostEqual(eigenvalues[1], 2.0, delta=1e-12)
        rank, condition = rank_and_condition(eigenvalues)
        self.assertEqual(rank, 1)
        self.assertIsNone(condition)

    def test_regularization_repairs_singular_covariance(self) -> None:
        regularized = shrink_covariance([[1.0, 1.0], [1.0, 1.0]], 0.1)
        eigenvalues = jacobi_eigenvalues(regularized)
        rank, condition = rank_and_condition(eigenvalues)
        self.assertEqual(rank, 2)
        self.assertIsNotNone(condition)
        self.assertGreater(eigenvalues[0], 0.0)
        cholesky(regularized)

    def test_cholesky_joint_score_and_independent_control(self) -> None:
        identity = [[1.0, 0.0], [0.0, 1.0]]
        factor = cholesky(identity)
        self.assertAlmostEqual(mahalanobis_squared((3.0, 4.0), factor), 25.0)
        self.assertEqual(independent_max_abs((3.0, -4.0)), 4.0)

    def test_relationship_change_has_joint_only_evidence_in_fixed_synthetic_case(self) -> None:
        # Deterministic highly correlated training vectors, then a fixed vector
        # with small marginals but a contradictory relationship. This is a unit
        # test of score mechanics, not hydraulic-source sensitivity evidence.
        training = [(float(index), float(index) + (0.01 if index % 2 else -0.01)) for index in range(-500, 500)]
        centers, scales = fit_robust_scaling(training)
        standardized = [[(row[i] - centers[i]) / scales[i] for i in range(2)] for row in training]
        covariance, _ = covariance_matrix(standardized)
        factor = cholesky(shrink_covariance(covariance, 0.05))
        contradictory = (0.5, -0.5)
        joint = mahalanobis_squared(contradictory, factor)
        independent = independent_max_abs(contradictory)
        self.assertGreater(joint, independent)

    def test_nearest_rank_quantile(self) -> None:
        self.assertEqual(nearest_rank_quantile([5.0, 1.0, 4.0, 2.0, 3.0], 0.8), 4.0)
        with self.assertRaises(ResearchDataError):
            nearest_rank_quantile([1.0, math.nan], 0.99)

    def test_cycle_partitions_reject_overlap_and_wrong_split(self) -> None:
        valid = {"fit": tuple(range(1788, 1794)), "calibration": (1794, 1795), "holdout": (1796, 1797)}
        validate_partition_mapping(valid)
        with self.assertRaisesRegex(ResearchDataError, "more than one partition"):
            validate_partition_mapping({"fit": (1788, 1789, 1790, 1791, 1792, 1793), "calibration": (1793, 1795), "holdout": (1796, 1797)})

    def test_fit_calibration_reader_stops_before_holdout_rows(self) -> None:
        payload = (b"1\t2\n" * 1787) + ((b"1\t2\n") * 8) + (b"not-a-number\n" * 2)
        with tempfile.TemporaryDirectory() as directory:
            archive_path = Path(directory) / "source.zip"
            with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
                archive.writestr("EPS1.txt", payload)
            result = read_signal_cycles(archive_path, "EPS1", tuple(range(1788, 1796)), expected_samples=2)
        self.assertEqual(set(result), set(range(1788, 1796)))
        self.assertTrue(all(values == array("d", [1.0, 2.0]) for values in result.values()))

    def test_selected_nonfinite_row_fails_closed(self) -> None:
        payload = (b"1\t2\n" * 1787) + ((b"1\tNaN\n"))
        with tempfile.TemporaryDirectory() as directory:
            archive_path = Path(directory) / "source.zip"
            with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
                archive.writestr("EPS1.txt", payload)
            with self.assertRaisesRegex(ResearchDataError, "not finite"):
                read_signal_cycles(archive_path, "EPS1", (1788,), expected_samples=2)

    def test_configuration_digest_detects_mutation(self) -> None:
        config = {"configurationSha256": "", "value": 1}
        from research_harness import _configuration_digest
        config["configurationSha256"] = _configuration_digest(config)
        verify_configuration_digest(config)
        config["value"] = 2
        with self.assertRaisesRegex(ResearchDataError, "digest mismatch"):
            verify_configuration_digest(config)

    def test_holdout_claim_is_single_use_and_repeat_stops_before_archive_read(self) -> None:
        research_dir = Path(__file__).resolve().parents[1]
        protocol_path = research_dir / "protocol-v1.md"
        split_path = research_dir / "cycle-split-manifest.csv"
        guard_path = "D" * 40
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            guard_dir = root / "persistent-guard"
            config = {
                "configurationSha256": "",
                "holdoutOpened": False,
                "holdoutCycles": [1796, 1797],
                "protocolSha256": sha256_file(protocol_path),
                "splitManifestSha256": sha256_file(split_path),
                "studyCommit": "E" * 40,
            }
            from research_harness import _configuration_digest

            config["configurationSha256"] = _configuration_digest(config)
            config_path = root / "frozen-config.json"
            config_path.write_text(json.dumps(config), encoding="utf-8")

            marker = claim_final_holdout_once(config["configurationSha256"], guard_path, config["studyCommit"], guard_dir)
            self.assertTrue(marker.is_file())
            with mock.patch("research_harness.verify_archive", side_effect=AssertionError("archive read")) as verify_archive, \
                 mock.patch("research_harness.read_profile_cycles", side_effect=AssertionError("profile rows read")) as read_profile, \
                 mock.patch("research_harness.load_channels", side_effect=AssertionError("matrix rows read")) as load_channels:
                with self.assertRaisesRegex(ResearchDataError, "already claimed/consumed"):
                    run_holdout(
                        root / "unused-source.zip",
                        root / "unused-source-manifest.json",
                        split_path,
                        protocol_path,
                        config_path,
                        guard_path,
                        guard_dir,
                    )
            verify_archive.assert_not_called()
            read_profile.assert_not_called()
            load_channels.assert_not_called()

    def test_independent_processes_cannot_claim_same_digest(self) -> None:
        context = multiprocessing.get_context("spawn")
        with tempfile.TemporaryDirectory() as directory:
            guard_dir = Path(directory) / "claims"
            barrier = context.Barrier(2)
            results = context.Queue()
            processes = [
                context.Process(target=_claim_in_independent_process, args=(str(guard_dir), barrier, results))
                for _ in range(2)
            ]
            try:
                for process in processes:
                    process.start()
                outcomes = [results.get(timeout=30) for _ in processes]
                for process in processes:
                    process.join(timeout=30)
                self.assertTrue(all(not process.is_alive() for process in processes))
                self.assertEqual(sorted(outcomes), ["claimed", "rejected"])
                self.assertTrue(all(process.exitcode == 0 for process in processes))
                self.assertEqual(len(list(guard_dir.glob("*.claim.json"))), 1)
            finally:
                for process in processes:
                    if process.is_alive():
                        process.terminate()
                        process.join(timeout=5)
                results.close()

    def test_default_holdout_guard_is_persistent_user_state_outside_checkout(self) -> None:
        guard_directory = default_holdout_guard_directory().resolve()
        checkout_root = Path(__file__).resolve().parents[4]
        self.assertTrue(guard_directory.is_absolute())
        self.assertNotIn(checkout_root.resolve(), guard_directory.parents)

    def test_peak_rss_is_reported_on_supported_runtimes(self) -> None:
        self.assertGreater(peak_rss_bytes(), 0)


if __name__ == "__main__":
    unittest.main()
