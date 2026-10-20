from __future__ import annotations

import math
import sys
import tempfile
import unittest
import zipfile
from array import array
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from research_harness import (  # noqa: E402
    ResearchDataError,
    cholesky,
    covariance_matrix,
    fit_robust_scaling,
    independent_max_abs,
    jacobi_eigenvalues,
    mahalanobis_squared,
    median,
    nearest_rank_quantile,
    read_signal_cycles,
    rank_and_condition,
    shrink_covariance,
    validate_partition_mapping,
    verify_configuration_digest,
)


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


if __name__ == "__main__":
    unittest.main()
