"""Fresh, synthetic-only Stage 4F temporal-score research study (#675)."""

from __future__ import annotations

import argparse
import ast
import ctypes
import hashlib
import json
import math
import os
import platform
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence


HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
PROTOCOL_PATH = HERE / "protocol-v1.md"
MANIFEST_PATH = HERE / "experiment-manifest-v1.json"
FIT_WINDOWS_PATH = HERE / "fit-calibration-windows-v1.json"
FIT_LINUX_PATH = HERE / "fit-calibration-linux-v1.json"
FIT_COMPARISON_PATH = HERE / "fit-calibration-comparison-v1.json"
CONFIG_PATH = HERE / "frozen-config-v1.json"
HOLDOUT_AGGREGATE_PATH = HERE / "holdout-aggregate-v1.json"
HOLDOUT_RECEIPT_PATH = HERE / "holdout-receipt-v1.json"
NUMERIC_HELPER_PATH = REPO / "docs/research/intelligence-stage4f/research_harness.py"
EPISODE_HELPER_PATH = REPO / "docs/research/intelligence-stage4f-synthetic-bench/bench.py"
TEST_PATH = HERE / "test_bench.py"

BASE_COMMIT = "ebe9b3abbbaffb8054b79273728f383e3bd17086"
PRODUCT_CHECKPOINT = "793b8303547491e02b98bf73732d3ab5ab76567a"
STUDY_ID = "W15-INTELLIGENCE-4F-TEMPORAL-STUDY-01"
EXPERIMENT_ID = "W15-INTEL-4F-TEMPORAL-2026-10-20-01"
HOLDOUT_ID = "W15-INTEL-4F-TEMPORAL-HOLDOUT-2026-10-20-01"
SEED_NAMESPACE = "ELITESCADA-W15-4F-TEMPORAL-v1"
BOOTSTRAP_NAMESPACE = "ELITESCADA-W15-4F-TEMPORAL-v1|bootstrap-v1"
UNIT_NAMESPACE = "ELITESCADA-W15-4F-TEMPORAL-UNIT-v1"

CADENCE_S = 10
SAMPLES_PER_GROUP = 120
EVENT_START = 12
EVENT_END = SAMPLES_PER_GROUP
STRONG_SHIFT = 3.5
EWMA_ALPHA = 0.20
SHRINKAGE = 0.05
QUANTILES = (0.99, 0.999)
BOOTSTRAP_REPLICATES = 2_000
MAX_CONDITION = 1_000_000.0
MAX_RSS_BYTES = 512 * 1024 * 1024
MAX_WALL_SECONDS = 900.0
MAX_COMMITTED_BYTES = 1024 * 1024
MASK64 = (1 << 64) - 1
TARGET_NAMES = ("motor_current_A", "flow_m3h", "discharge_pressure_bar")
TARGET_SDS = (0.70, 1.00, 0.10)
R0 = ((1.0, 0.65, 0.25), (0.65, 1.0, 0.20), (0.25, 0.20, 1.0))
R1 = ((1.0, -0.65, 0.25), (-0.65, 1.0, 0.20), (0.25, 0.20, 1.0))
AR_PHI = 0.65
SCENARIOS = (
    "nonfault-context", "single-current-strong", "multi-target-strong",
    "relationship-strong", "single-current-1scale", "single-current-2scale",
    "single-current-ramp",
)
STRONG_SCENARIOS = ("single-current-strong", "multi-target-strong", "relationship-strong")
FIT_GROUPS = tuple(range(0, 60))
CAL_GROUPS = tuple(range(60, 120))
HOLDOUT_GROUPS = tuple(range(120, 240))
PARTITIONS = {"fit": FIT_GROUPS, "calibration": CAL_GROUPS, "holdout": HOLDOUT_GROUPS}
STATISTICS = ("joint", "marginal")
FORMS = ("instant", "ewma")

NUMERIC_HELPER_SHA256 = "C216B0DF9D9C79021F7E765FF6C4F5FAACC21CB2BBCC141D24AF12FD34D2F037"
EPISODE_HELPER_SHA256 = "7AAB78231623EDB1B00C9CB83D4B5E11E8A1FC4FB3023D4FA577C578EC41E318"
NUMERIC_HELPER_ALLOWLIST = {
    "ResearchDataError", "MAD_NORMAL_SCALE", "EIGEN_RELATIVE_TOLERANCE", "median",
    "fit_robust_scaling", "transform_rows", "covariance_matrix", "jacobi_eigenvalues",
    "rank_and_condition", "shrink_covariance", "cholesky", "mahalanobis_squared",
    "independent_max_abs", "nearest_rank_quantile",
}
EPISODE_HELPER_ALLOWLIST = {"find_episodes"}


@dataclass(frozen=True)
class Observation:
    group_id: int
    sample_index: int
    source_time_s: int | None
    targets: tuple[float | None, ...]
    running: bool | None
    speed_rpm: float | None
    load_fraction: float | None
    quality: str | None
    source_time_verified: bool
    source_time_synchronized: bool
    context_id: str
    profile_version: str


@dataclass(frozen=True)
class Eligibility:
    eligible: bool
    reason: str | None = None
    break_before: bool = False


@dataclass(frozen=True)
class ScoreDecision:
    sample_index: int
    source_time_s: int | None
    score: float | None
    eligible: bool
    reason: str | None = None
    break_before: bool = False


class SplitMix64:
    """Study-local PRNG implementation; no prior generator is imported."""

    def __init__(self, seed: int):
        self.state = seed & MASK64

    def next_u64(self) -> int:
        self.state = (self.state + 0x9E3779B97F4A7C15) & MASK64
        value = self.state
        value = ((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
        value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & MASK64
        return value ^ (value >> 31)

    def uniform_open(self) -> float:
        return ((self.next_u64() >> 11) + 0.5) / (1 << 53)

    def normal_approx(self) -> float:
        return sum(self.uniform_open() for _ in range(12)) - 6.0


def _normalized_text_bytes(raw: bytes) -> bytes:
    return raw.replace(b"\r\n", b"\n").replace(b"\r", b"\n")


def _sha256_bytes(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest().upper()


def _sha256_text(path: Path) -> str:
    return _sha256_bytes(_normalized_text_bytes(path.read_bytes()))


def _canonical_json(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def _sha256_json(value: Any) -> str:
    return _sha256_bytes(_canonical_json(value))


def _write_json(path: Path, value: Any, *, refuse_overwrite: bool = False) -> None:
    if refuse_overwrite and path.exists():
        raise RuntimeError(f"refusing to overwrite existing frozen output: {path.name}")
    data = json.dumps(value, sort_keys=True, indent=2, allow_nan=False).encode("utf-8") + b"\n"
    if len(data) > MAX_COMMITTED_BYTES:
        raise RuntimeError("single committed output exceeds the preregistered 1 MiB bound")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    if temporary.exists():
        raise RuntimeError("stale temporary output exists; refusing unsafe replacement")
    with temporary.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def _source_hashes() -> dict[str, str]:
    return {
        "protocol-v1.md": _sha256_text(PROTOCOL_PATH),
        "experiment-manifest-v1.json": _sha256_text(MANIFEST_PATH),
        "bench.py": _sha256_text(Path(__file__)),
        "test_bench.py": _sha256_text(TEST_PATH),
        "numeric-helper-source": _sha256_text(NUMERIC_HELPER_PATH),
        "episode-helper-source": _sha256_text(EPISODE_HELPER_PATH),
    }


def _extract_helpers(path: Path, expected_digest: str, allowlist: set[str], namespace: dict[str, Any]) -> dict[str, Any]:
    observed_digest = _sha256_text(path)
    if observed_digest != expected_digest:
        raise RuntimeError(f"accepted helper source digest mismatch: {path.relative_to(REPO)} {observed_digest}")
    tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
    selected: list[ast.stmt] = [ast.ImportFrom(module="__future__", names=[ast.alias(name="annotations")], level=0)]
    for node in tree.body:
        if isinstance(node, ast.ClassDef) and node.name in allowlist:
            selected.append(node)
        elif isinstance(node, ast.FunctionDef) and node.name in allowlist:
            selected.append(node)
        elif isinstance(node, ast.Assign) and any(
            isinstance(target, ast.Name) and target.id in allowlist for target in node.targets
        ):
            selected.append(node)
    loaded_names = {
        node.name for node in selected if isinstance(node, (ast.ClassDef, ast.FunctionDef))
    }
    for node in selected:
        if isinstance(node, ast.Assign):
            loaded_names.update(target.id for target in node.targets if isinstance(target, ast.Name))
    required = allowlist.intersection(loaded_names)
    if required != allowlist:
        raise RuntimeError(f"accepted helper allowlist incomplete: {sorted(allowlist - required)}")
    exec(compile(ast.fix_missing_locations(ast.Module(body=selected, type_ignores=[])), str(path), "exec"), namespace)
    return namespace


def _load_helpers() -> dict[str, Any]:
    base_namespace: dict[str, Any] = {"math": math, "Iterable": Iterable, "Sequence": Sequence}
    numerics = _extract_helpers(NUMERIC_HELPER_PATH, NUMERIC_HELPER_SHA256, NUMERIC_HELPER_ALLOWLIST, base_namespace)
    if not isinstance(numerics.get("ResearchDataError"), type):
        raise RuntimeError("accepted numerical error type unavailable")
    numerics["ScoreDecision"] = ScoreDecision
    _extract_helpers(EPISODE_HELPER_PATH, EPISODE_HELPER_SHA256, EPISODE_HELPER_ALLOWLIST, numerics)
    return numerics


_HELPERS = _load_helpers()
ResearchDataError = _HELPERS["ResearchDataError"]
median = _HELPERS["median"]
fit_robust_scaling = _HELPERS["fit_robust_scaling"]
transform_rows = _HELPERS["transform_rows"]
covariance_matrix = _HELPERS["covariance_matrix"]
jacobi_eigenvalues = _HELPERS["jacobi_eigenvalues"]
rank_and_condition = _HELPERS["rank_and_condition"]
shrink_covariance = _HELPERS["shrink_covariance"]
cholesky = _HELPERS["cholesky"]
mahalanobis_squared = _HELPERS["mahalanobis_squared"]
independent_max_abs = _HELPERS["independent_max_abs"]
nearest_rank_quantile = _HELPERS["nearest_rank_quantile"]
find_episodes = _HELPERS["find_episodes"]


def derive_seed(group_id: int, namespace: str = SEED_NAMESPACE) -> int:
    raw = f"{namespace}|group-{group_id:03d}".encode("ascii")
    return int.from_bytes(hashlib.sha256(raw).digest()[:8], "big")


def partition_digest() -> str:
    return _sha256_json({name: list(groups) for name, groups in PARTITIONS.items()})


def validate_partitions(fit: Sequence[int] = FIT_GROUPS, calibration: Sequence[int] = CAL_GROUPS, holdout: Sequence[int] = HOLDOUT_GROUPS) -> None:
    parts = {"fit": tuple(fit), "calibration": tuple(calibration), "holdout": tuple(holdout)}
    if parts != PARTITIONS:
        raise ResearchDataError("fresh source-group assignment differs from the preregistered 60/60/120 split")
    all_groups = [group for ids in parts.values() for group in ids]
    if len(all_groups) != len(set(all_groups)) or len(all_groups) != 240:
        raise ResearchDataError("source groups overlap or the expected 240 independent groups are missing")
    seeds = [derive_seed(group) for group in all_groups]
    if len(seeds) != len(set(seeds)):
        raise ResearchDataError("fresh seed namespace produced a duplicate group seed")


def _context(group_id: int, sample_index: int) -> tuple[bool, float, float]:
    speed = 1430.0 + 5.0 * (group_id % 9)
    load_base = 0.30 + 0.05 * (group_id % 9)
    offsets = (-0.05, 0.05, 0.0, 0.07)
    load = min(0.80, max(0.25, load_base + offsets[sample_index // 30]))
    return True, speed, load


def _expected_targets(speed: float, load: float) -> tuple[float, float, float]:
    return (
        4.0 + 20.0 * load + 0.012 * (speed - 1450.0),
        3.0 + 30.0 * load + 0.020 * (speed - 1450.0),
        1.2 + 3.0 * load - 0.0015 * (speed - 1450.0),
    )


def generate_run(
    group_id: int,
    scenario: str = "nonfault-context",
    fit_scales: Sequence[float] | None = None,
    *,
    namespace: str = SEED_NAMESPACE,
) -> list[Observation]:
    """Generate one new namespaced synthetic run; never calls a prior generator."""
    if namespace == SEED_NAMESPACE and group_id not in range(240):
        raise ValueError("study group id must be in the frozen range 0..239")
    if namespace != SEED_NAMESPACE and not namespace.startswith("ELITESCADA-W15-4F-TEMPORAL-UNIT-"):
        raise ValueError("non-study generation is restricted to the separate UNIT namespace")
    if group_id < 0 or scenario not in SCENARIOS:
        raise ValueError("group id or scenario is outside the declared generator")
    if scenario != "nonfault-context" and (fit_scales is None or len(fit_scales) != 3):
        raise ValueError("injected scenario requires the three frozen fit-only residual scales")
    rng = SplitMix64(derive_seed(group_id, namespace))
    factor0 = cholesky(R0)
    factor1 = cholesky(R1)
    state = [0.0, 0.0, 0.0]
    ar_innovation_scale = math.sqrt(1.0 - AR_PHI * AR_PHI)
    rows: list[Observation] = []
    scales = tuple(float(value) for value in (fit_scales or ()))
    for index in range(SAMPLES_PER_GROUP):
        independent = [rng.normal_approx() for _ in range(3)]
        factor = factor1 if scenario == "relationship-strong" and index >= EVENT_START else factor0
        correlated = [sum(factor[row][column] * independent[column] for column in range(row + 1)) for row in range(3)]
        state = [AR_PHI * state[j] + ar_innovation_scale * correlated[j] for j in range(3)]
        running, speed, load = _context(group_id, index)
        expected = _expected_targets(speed, load)
        targets = [expected[j] + state[j] * TARGET_SDS[j] for j in range(3)]
        if index >= EVENT_START:
            if scenario == "single-current-strong":
                targets[0] += STRONG_SHIFT * scales[0]
            elif scenario == "multi-target-strong":
                for target in range(3):
                    targets[target] += STRONG_SHIFT * scales[target]
            elif scenario == "single-current-1scale":
                targets[0] += scales[0]
            elif scenario == "single-current-2scale":
                targets[0] += 2.0 * scales[0]
            elif scenario == "single-current-ramp":
                targets[0] += STRONG_SHIFT * min(1.0, (index - EVENT_START) / 60.0) * scales[0]
        rows.append(Observation(
            group_id, index, index * CADENCE_S, tuple(targets), running, speed, load,
            "Good", True, True, "pump-profile-v1", "model-v1",
        ))
    return rows


def _solve_linear(matrix: Sequence[Sequence[float]], vector: Sequence[float]) -> list[float]:
    size = len(vector)
    if size == 0 or len(matrix) != size or any(len(row) != size for row in matrix):
        raise ResearchDataError("normal-equation system shape mismatch")
    augmented = [[float(matrix[i][j]) for j in range(size)] + [float(vector[i])] for i in range(size)]
    scale = max(1.0, max(abs(value) for row in matrix for value in row))
    for column in range(size):
        pivot = max(range(column, size), key=lambda row: abs(augmented[row][column]))
        if not math.isfinite(augmented[pivot][column]) or abs(augmented[pivot][column]) <= 1e-12 * scale:
            raise ResearchDataError("singular/ill-conditioned contextual design")
        augmented[column], augmented[pivot] = augmented[pivot], augmented[column]
        divisor = augmented[column][column]
        augmented[column] = [value / divisor for value in augmented[column]]
        for row in range(size):
            if row != column:
                multiplier = augmented[row][column]
                augmented[row] = [augmented[row][i] - multiplier * augmented[column][i] for i in range(size + 1)]
    result = [augmented[i][-1] for i in range(size)]
    if any(not math.isfinite(value) for value in result):
        raise ResearchDataError("context coefficients are nonfinite")
    return result


def _features(row: Observation) -> tuple[float, float, float]:
    if row.speed_rpm is None or row.load_fraction is None:
        raise ResearchDataError("missing fit context")
    return (1.0, row.load_fraction, (row.speed_rpm - 1450.0) / 20.0)


def fit_reference_from_rows(rows: Sequence[Observation]) -> dict[str, Any]:
    """Fit only the supplied rows; production caller passes fit nonfault rows."""
    if len(rows) < 4 or any(not validate_observations([row])[0].eligible for row in rows):
        raise ResearchDataError("fit requires at least four eligible complete observations")
    xtx = [[0.0] * 3 for _ in range(3)]
    xty = [[0.0] * 3 for _ in range(3)]
    for row in rows:
        x = _features(row)
        for i in range(3):
            for j in range(3):
                xtx[i][j] += x[i] * x[j]
            for target in range(3):
                value = row.targets[target]
                if value is None:
                    raise ResearchDataError("fit target is missing")
                xty[target][i] += x[i] * float(value)
    design_eigenvalues = jacobi_eigenvalues(xtx)
    design_rank, design_condition = rank_and_condition(design_eigenvalues)
    if design_rank != 3 or design_condition is None or design_condition > MAX_CONDITION:
        raise ResearchDataError("context design is singular or ill-conditioned")
    coefficients = [_solve_linear(xtx, xty[target]) for target in range(3)]
    residual_rows: list[tuple[float, float, float]] = []
    for row in rows:
        x = _features(row)
        residual_rows.append(tuple(
            float(row.targets[target]) - sum(coefficients[target][i] * x[i] for i in range(3))
            for target in range(3)
        ))
    centers, scales = fit_robust_scaling(residual_rows)
    transformed = transform_rows(residual_rows, centers, scales)
    covariance, covariance_mean = covariance_matrix(transformed)
    values = jacobi_eigenvalues(covariance)
    rank, condition = rank_and_condition(values)
    if rank != 3 or condition is None or not math.isfinite(condition):
        raise ResearchDataError("fit covariance is singular or ill-conditioned")
    regularized = shrink_covariance(covariance, SHRINKAGE)
    regularized_values = jacobi_eigenvalues(regularized)
    regularized_rank, regularized_condition = rank_and_condition(regularized_values)
    if regularized_rank != 3 or regularized_condition is None or regularized_condition > MAX_CONDITION:
        raise ResearchDataError("fixed-shrinkage fit covariance is ill-conditioned")
    factor = cholesky(regularized)
    centered = [[row[j] - covariance_mean[j] for j in range(3)] for row in transformed]
    joint_values = [mahalanobis_squared(row, factor) for row in centered]
    marginal_values = [independent_max_abs(row) for row in centered]
    return {
        "contextCoefficients": coefficients,
        "robustCenters": centers,
        "robustScales": scales,
        "covariance": covariance,
        "covarianceMean": covariance_mean,
        "regularizedCovariance": regularized,
        "choleskyLower": factor,
        "contextDesignRank": design_rank,
        "contextDesignCondition": design_condition,
        "unregularizedRank": rank,
        "unregularizedCondition": condition,
        "regularizedRank": regularized_rank,
        "regularizedCondition": regularized_condition,
        "fitStatisticMeans": {
            "joint": sum(joint_values) / len(joint_values),
            "marginal": sum(marginal_values) / len(marginal_values),
        },
        "fitVectorCount": len(rows),
    }


def fit_reference() -> dict[str, Any]:
    """Fit reference using only fresh IDs 0–59, baseline scenario."""
    validate_partitions()
    rows: list[Observation] = []
    for group_id in FIT_GROUPS:
        rows.extend(generate_run(group_id, "nonfault-context"))
    reference = fit_reference_from_rows(rows)
    reference["fitGroupCount"] = len(FIT_GROUPS)
    reference["fitUniqueScalarTargetValues"] = len(rows) * 3
    reference["fitSourceVectorPasses"] = 2
    reference["fitSourceVectorVisits"] = len(rows) * 2
    return reference


def validate_observations(
    rows: Sequence[Observation],
    cadence_s: int | None = CADENCE_S,
    expected_context: str = "pump-profile-v1",
    expected_version: str = "model-v1",
) -> list[Eligibility]:
    """Fail closed; gaps break state, and every duplicate-time row is invalid."""
    if cadence_s is None or cadence_s <= 0:
        return [Eligibility(False, "unknown-cadence", True) for _ in rows]
    counts: dict[int, int] = {}
    for row in rows:
        if row.source_time_s is not None:
            counts[row.source_time_s] = counts.get(row.source_time_s, 0) + 1
    results: list[Eligibility] = []
    previous_time: int | None = None
    previous_eligible = False
    for row in rows:
        reason: str | None = None
        break_before = not previous_eligible
        if row.source_time_s is not None and counts.get(row.source_time_s, 0) > 1:
            reason = "duplicate-source-time"
        elif len(row.targets) != 3 or any(value is None for value in row.targets):
            reason = "missing-required-target"
        elif any(not math.isfinite(float(value)) for value in row.targets if value is not None):
            reason = "nonfinite-target"
        elif row.running is not True:
            reason = "context-not-running"
        elif row.speed_rpm is None or not math.isfinite(row.speed_rpm):
            reason = "missing-or-nonfinite-speed"
        elif row.load_fraction is None or not math.isfinite(row.load_fraction):
            reason = "missing-or-nonfinite-load"
        elif row.quality != "Good":
            reason = "quality-not-good"
        elif row.source_time_s is None:
            reason = "missing-source-time"
        elif not row.source_time_verified:
            reason = "source-time-unverified"
        elif not row.source_time_synchronized:
            reason = "source-time-unsynchronized"
        elif row.source_time_s % cadence_s != 0:
            reason = "misaligned-source-time"
        elif row.context_id != expected_context:
            reason = "context-mismatch"
        elif row.profile_version != expected_version:
            reason = "profile-version-mismatch"
        elif not 0.25 <= float(row.load_fraction) <= 0.80:
            reason = "context-outside-profile"
        elif previous_time is not None and previous_eligible and row.source_time_s <= previous_time:
            reason = "nonincreasing-source-time"

        if reason is not None:
            results.append(Eligibility(False, reason, True))
            previous_time = None
            previous_eligible = False
            continue

        if previous_time is not None and previous_eligible:
            delta = row.source_time_s - previous_time
            if delta != cadence_s:
                break_before = True
                reason = "gap-over-limit" if delta > 2 * cadence_s else "missing-expected-sample"
        results.append(Eligibility(True, reason, break_before))
        previous_time = row.source_time_s
        previous_eligible = True
    return results


def _residual(row: Observation, reference: dict[str, Any]) -> list[float]:
    features = _features(row)
    residual: list[float] = []
    for target in range(3):
        value = row.targets[target]
        if value is None:
            raise ResearchDataError("missing target reached scorer")
        predicted = sum(reference["contextCoefficients"][target][i] * features[i] for i in range(3))
        standardized = (float(value) - predicted - reference["robustCenters"][target]) / reference["robustScales"][target]
        centered = standardized - reference["covarianceMean"][target]
        if not math.isfinite(centered):
            raise ResearchDataError("nonfinite robust standardized residual")
        residual.append(centered)
    return residual


def _instantaneous_statistics(row: Observation, reference: dict[str, Any]) -> dict[str, float]:
    vector = _residual(row, reference)
    return {
        "joint": mahalanobis_squared(vector, reference["choleskyLower"]),
        "marginal": independent_max_abs(vector),
    }


def apply_ewma(
    instantaneous: Sequence[float | None],
    eligibility: Sequence[Eligibility],
    initialization: float,
    alpha: float = EWMA_ALPHA,
) -> list[float | None]:
    if len(instantaneous) != len(eligibility) or not 0.0 < alpha <= 1.0:
        raise ValueError("EWMA inputs or alpha are invalid")
    state = float(initialization)
    result: list[float | None] = []
    for value, gate in zip(instantaneous, eligibility):
        if gate.break_before or not gate.eligible:
            state = float(initialization)
        if not gate.eligible or value is None:
            result.append(None)
            state = float(initialization)
            continue
        if not math.isfinite(float(value)):
            result.append(None)
            state = float(initialization)
            continue
        state = alpha * float(value) + (1.0 - alpha) * state
        result.append(state)
    return result


def score_run(rows: Sequence[Observation], reference: dict[str, Any]) -> dict[str, list[ScoreDecision]]:
    eligibility = validate_observations(rows)
    instant_values: dict[str, list[float | None]] = {name: [] for name in STATISTICS}
    for row, gate in zip(rows, eligibility):
        if gate.eligible:
            scores = _instantaneous_statistics(row, reference)
            for name in STATISTICS:
                instant_values[name].append(scores[name])
        else:
            for name in STATISTICS:
                instant_values[name].append(None)

    outputs: dict[str, list[ScoreDecision]] = {}
    for statistic in STATISTICS:
        ewma_values = apply_ewma(instant_values[statistic], eligibility, reference["fitStatisticMeans"][statistic])
        outputs[f"{statistic}-instant"] = [
            ScoreDecision(row.sample_index, row.source_time_s, instant_values[statistic][index], gate.eligible, gate.reason, gate.break_before)
            for index, (row, gate) in enumerate(zip(rows, eligibility))
        ]
        outputs[f"{statistic}-ewma"] = [
            ScoreDecision(row.sample_index, row.source_time_s, ewma_values[index], gate.eligible, gate.reason, gate.break_before)
            for index, (row, gate) in enumerate(zip(rows, eligibility))
        ]
    return outputs


def candidate_name(statistic: str, form: str, quantile: float) -> str:
    if statistic not in STATISTICS or form not in FORMS or quantile not in QUANTILES:
        raise ValueError("candidate is outside the finite preregistered grid")
    qlabel = "q099" if quantile == 0.99 else "q0999"
    return f"{statistic}-{form}-{qlabel}"


def candidate_specs() -> list[dict[str, Any]]:
    return [
        {"name": candidate_name(statistic, form, quantile), "statistic": statistic, "form": form, "quantile": quantile}
        for statistic in STATISTICS for form in FORMS for quantile in QUANTILES
    ]


def summarize_run(decisions: Sequence[ScoreDecision], threshold: float, onset_index: int | None) -> dict[str, Any]:
    eligible = [item for item in decisions if item.eligible and item.score is not None]
    reasons: dict[str, int] = {}
    for item in decisions:
        if not item.eligible:
            reason = item.reason or "unspecified-ineligible"
            reasons[reason] = reasons.get(reason, 0) + 1
    starts = find_episodes(decisions, threshold)
    exceedances = sum(float(item.score) > threshold for item in eligible)
    summary: dict[str, Any] = {
        "eligiblePoints": len(eligible),
        "ineligiblePoints": len(decisions) - len(eligible),
        "ineligibleReasons": reasons,
        "pointExceedances": exceedances,
        "episodeCount": len(starts),
        "preOnsetEpisodes": 0,
        "eligibleHours": len(eligible) * CADENCE_S / 3600.0,
        "detected": None,
        "latencySeconds": None,
        "within600Seconds": None,
    }
    if onset_index is not None:
        summary["preOnsetEpisodes"] = sum(index < onset_index for index in starts)
        event_starts = [index for index in starts if index >= onset_index]
        if event_starts:
            latency = (event_starts[0] - onset_index) * CADENCE_S
            summary.update({"detected": True, "latencySeconds": latency, "within600Seconds": latency <= 600})
        else:
            summary.update({"detected": False, "within600Seconds": False})
    return summary


def _candidate_thresholds(normal_decisions: dict[str, list[list[ScoreDecision]]]) -> dict[str, float]:
    thresholds: dict[str, float] = {}
    for spec in candidate_specs():
        method = f"{spec['statistic']}-{spec['form']}"
        values = [
            float(item.score)
            for group in normal_decisions[method]
            for item in group
            if item.eligible and item.score is not None
        ]
        if not values:
            raise ResearchDataError(f"no eligible calibration reference scores for {spec['name']}")
        thresholds[spec["name"]] = nearest_rank_quantile(values, float(spec["quantile"]))
    return thresholds


def _aggregate_group_records(records: Sequence[dict[str, Any]], *, include_event: bool) -> dict[str, Any]:
    group_count = len(records)
    eligible_points = sum(int(row["eligiblePoints"]) for row in records)
    ineligible_points = sum(int(row["ineligiblePoints"]) for row in records)
    exceedances = sum(int(row["pointExceedances"]) for row in records)
    episodes = sum(int(row["episodeCount"]) for row in records)
    eligible_hours = sum(float(row["eligibleHours"]) for row in records)
    reason_counts: dict[str, int] = {}
    for row in records:
        for reason, count in row["ineligibleReasons"].items():
            reason_counts[reason] = reason_counts.get(reason, 0) + int(count)
    latency_values = [float(row["latencySeconds"]) for row in records if row["latencySeconds"] is not None]
    result: dict[str, Any] = {
        "independentSourceGroups": group_count,
        "eligibleSourceGroups": sum(int(row["eligiblePoints"] > 0) for row in records),
        "eligiblePoints": eligible_points,
        "ineligiblePoints": ineligible_points,
        "ineligibleReasons": reason_counts,
        "pointExceedances": exceedances,
        "pointExceedanceRate": exceedances / eligible_points if eligible_points else None,
        "episodeCount": episodes,
        "eligibleHours": eligible_hours,
        "episodesPer24EligibleSimulatedHours": episodes / eligible_hours * 24.0 if eligible_hours else None,
        "groupsWithOneOrMoreEpisodes": sum(int(row["episodeCount"] > 0) for row in records),
        "wilsonGroupOccurrence95": _wilson_interval(
            sum(int(row["episodeCount"] > 0) for row in records), group_count
        ),
        "preOnsetEpisodes": sum(int(row["preOnsetEpisodes"]) for row in records),
        "latencySeconds": {
            "detectedGroups": len(latency_values),
            "p50": nearest_rank_quantile(latency_values, 0.50) if latency_values else None,
            "p95": nearest_rank_quantile(latency_values, 0.95) if latency_values else None,
            "maximum": max(latency_values) if latency_values else None,
            "meanDetectedOnly": sum(latency_values) / len(latency_values) if latency_values else None,
        },
    }
    if include_event:
        detected = sum(int(row["detected"] is True) for row in records)
        timely = sum(int(row["within600Seconds"] is True) for row in records)
        late = sum(int(row["detected"] is True and row["within600Seconds"] is False) for row in records)
        result.update({
            "detectedGroups": detected,
            "missedGroups": group_count - detected,
            "lateGroups": late,
            "recall": detected / group_count if group_count else None,
            "timelyWithin600Seconds": timely,
            "timelyFractionOfAllGroups": timely / group_count if group_count else None,
        })
    return result


def _wilson_interval(successes: int, trials: int, z: float = 1.959963984540054) -> list[float] | None:
    if trials <= 0 or not 0 <= successes <= trials:
        return None
    p = successes / trials
    denominator = 1.0 + z * z / trials
    center = (p + z * z / (2.0 * trials)) / denominator
    margin = z * math.sqrt((p * (1.0 - p) + z * z / (4.0 * trials)) / trials) / denominator
    return [max(0.0, center - margin), min(1.0, center + margin)]


def _bootstrap_seed(candidate: str, scenario: str) -> int:
    raw = f"{BOOTSTRAP_NAMESPACE}|{candidate}|{scenario}".encode("ascii")
    return int.from_bytes(hashlib.sha256(raw).digest()[:8], "big")


def _bootstrap_intervals(records: Sequence[dict[str, Any]], *, include_event: bool, seed: int) -> dict[str, list[float] | None]:
    if not records:
        return {}
    rng = SplitMix64(seed)
    n = len(records)
    samples: dict[str, list[float]] = {"pointExceedanceRate": [], "episodesPer24EligibleSimulatedHours": []}
    if include_event:
        samples.update({"recall": [], "timelyFractionOfAllGroups": [], "meanLatencyDetectedOnly": []})
    for _ in range(BOOTSTRAP_REPLICATES):
        eligible_points = exceedances = episodes = detected = timely = 0
        eligible_hours = latency_sum = 0.0
        for _ in range(n):
            row = records[rng.next_u64() % n]
            eligible_points += int(row["eligiblePoints"])
            exceedances += int(row["pointExceedances"])
            episodes += int(row["episodeCount"])
            eligible_hours += float(row["eligibleHours"])
            if include_event:
                detected += int(row["detected"] is True)
                timely += int(row["within600Seconds"] is True)
                if row["latencySeconds"] is not None:
                    latency_sum += float(row["latencySeconds"])
        if eligible_points:
            samples["pointExceedanceRate"].append(exceedances / eligible_points)
        if eligible_hours:
            samples["episodesPer24EligibleSimulatedHours"].append(episodes / eligible_hours * 24.0)
        if include_event:
            samples["recall"].append(detected / n)
            samples["timelyFractionOfAllGroups"].append(timely / n)
            if detected:
                samples["meanLatencyDetectedOnly"].append(latency_sum / detected)
    return {
        name: [nearest_rank_quantile(values, 0.025), nearest_rank_quantile(values, 0.975)] if values else None
        for name, values in samples.items()
    }


def _calibration_feasible(metrics: dict[str, Any], reference: dict[str, Any]) -> bool:
    nonfault = metrics["nonfault-context"]
    if reference["regularizedCondition"] > MAX_CONDITION:
        return False
    if nonfault["pointExceedanceRate"] is None or nonfault["pointExceedanceRate"] > 0.01:
        return False
    if nonfault["episodesPer24EligibleSimulatedHours"] is None or nonfault["episodesPer24EligibleSimulatedHours"] > 1.0:
        return False
    for scenario in STRONG_SCENARIOS:
        if metrics[scenario].get("recall") is None or metrics[scenario]["recall"] < 0.90:
            return False
        if metrics[scenario].get("timelyFractionOfAllGroups") is None or metrics[scenario]["timelyFractionOfAllGroups"] < 0.90:
            return False
    return True


def select_calibration_candidate(calibration_metrics: dict[str, Any], reference: dict[str, Any]) -> dict[str, Any]:
    joint_specs = [spec for spec in candidate_specs() if spec["statistic"] == "joint"]
    feasible: list[tuple[dict[str, Any], dict[str, Any]]] = []
    for spec in joint_specs:
        metrics = calibration_metrics[spec["name"]]
        if _calibration_feasible(metrics, reference):
            minimum_timely = min(metrics[scenario]["timelyFractionOfAllGroups"] for scenario in STRONG_SCENARIOS)
            minimum_recall = min(metrics[scenario]["recall"] for scenario in STRONG_SCENARIOS)
            false_episodes = metrics["nonfault-context"]["episodeCount"]
            method_order = 0 if spec["form"] == "instant" else 1
            quantile_order = 0 if spec["quantile"] == 0.99 else 1
            feasible.append((spec, {
                "minimumTimely": minimum_timely,
                "minimumRecall": minimum_recall,
                "falseEpisodes": false_episodes,
                "methodOrder": method_order,
                "quantileOrder": quantile_order,
            }))
    if not feasible:
        fallback = candidate_name("joint", "instant", 0.99)
        return {
            "selectedCandidate": fallback,
            "calibrationFeasible": False,
            "reason": "no joint candidate met every frozen calibration point-estimate target; freeze instantaneous joint q0.99 as an infeasible reference",
            "candidateCanBeCalledWinner": False,
            "eligibleJointCandidates": [spec["name"] for spec in joint_specs],
        }
    feasible.sort(key=lambda item: (
        -item[1]["minimumTimely"], -item[1]["minimumRecall"], item[1]["falseEpisodes"],
        item[1]["methodOrder"], item[1]["quantileOrder"],
    ))
    winner = feasible[0][0]["name"]
    return {
        "selectedCandidate": winner,
        "calibrationFeasible": True,
        "reason": "selected by the frozen calibration-only lexicographic order",
        "candidateCanBeCalledWinner": True,
        "eligibleJointCandidates": [spec["name"] for spec in joint_specs],
    }


def _peak_rss_bytes() -> int:
    if os.name == "nt":
        from ctypes import wintypes

        class ProcessMemoryCounters(ctypes.Structure):
            _fields_ = [
                ("cb", wintypes.DWORD),
                ("PageFaultCount", wintypes.DWORD),
                ("PeakWorkingSetSize", ctypes.c_size_t),
                ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t),
                ("PeakPagefileUsage", ctypes.c_size_t),
            ]

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        psapi = ctypes.WinDLL("psapi", use_last_error=True)
        get_current_process = kernel32.GetCurrentProcess
        get_current_process.argtypes = []
        get_current_process.restype = wintypes.HANDLE
        get_process_memory_info = psapi.GetProcessMemoryInfo
        get_process_memory_info.argtypes = [wintypes.HANDLE, ctypes.POINTER(ProcessMemoryCounters), wintypes.DWORD]
        get_process_memory_info.restype = wintypes.BOOL
        counters = ProcessMemoryCounters()
        counters.cb = ctypes.sizeof(counters)
        if not get_process_memory_info(get_current_process(), ctypes.byref(counters), counters.cb):
            raise ctypes.WinError(ctypes.get_last_error())
        return int(counters.PeakWorkingSetSize)
    import resource

    peak = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    return int(peak if sys.platform == "darwin" else peak * 1024)


def _git_head() -> str:
    result = subprocess.run(["git", "rev-parse", "HEAD"], cwd=REPO, capture_output=True, text=True, check=True)
    return result.stdout.strip()


def _git_clean() -> bool:
    result = subprocess.run(["git", "status", "--porcelain=v1", "--untracked-files=all"], cwd=REPO, capture_output=True, text=True, check=True)
    return not result.stdout.strip()


def _platform_record(environment: str, image_digest: str | None, source_commit: str) -> dict[str, Any]:
    return {
        "environment": environment,
        "os": platform.platform(),
        "pythonVersion": platform.python_version(),
        "implementation": platform.python_implementation(),
        "imageDigest": image_digest,
        "sourceCommit": source_commit,
    }


def run_fit_calibration(
    output_path: Path,
    *,
    environment: str,
    image_digest: str | None = None,
    source_commit: str | None = None,
) -> dict[str, Any]:
    """Fit fresh IDs 0–59, calibrate only on IDs 60–119; never generates holdout IDs."""
    validate_partitions()
    started_wall = time.perf_counter()
    started_cpu = time.process_time()
    commit = source_commit or _git_head()
    model = fit_reference()
    candidate_rows = candidate_specs()
    normal_decisions: dict[str, list[list[ScoreDecision]]] = {
        f"{statistic}-{form}": [] for statistic in STATISTICS for form in FORMS
    }
    generated_calibration_vectors = 0
    for group_id in CAL_GROUPS:
        rows = generate_run(group_id, "nonfault-context")
        generated_calibration_vectors += len(rows)
        scores = score_run(rows, model)
        for method, decisions in scores.items():
            normal_decisions[method].append(decisions)

    thresholds = _candidate_thresholds(normal_decisions)
    grouped: dict[str, dict[str, list[dict[str, Any]]]] = {
        spec["name"]: {scenario: [] for scenario in SCENARIOS} for spec in candidate_rows
    }
    for spec in candidate_rows:
        method = f"{spec['statistic']}-{spec['form']}"
        for group_index, decisions in enumerate(normal_decisions[method]):
            grouped[spec["name"]]["nonfault-context"].append(
                summarize_run(decisions, thresholds[spec["name"]], None)
            )

    generated_event_vectors = 0
    for scenario in SCENARIOS[1:]:
        for group_id in CAL_GROUPS:
            rows = generate_run(group_id, scenario, model["robustScales"])
            generated_event_vectors += len(rows)
            scores = score_run(rows, model)
            for spec in candidate_rows:
                method = f"{spec['statistic']}-{spec['form']}"
                grouped[spec["name"]][scenario].append(
                    summarize_run(scores[method], thresholds[spec["name"]], EVENT_START)
                )

    calibration_metrics = {
        name: {
            scenario: _aggregate_group_records(records, include_event=scenario != "nonfault-context")
            for scenario, records in scenarios.items()
        }
        for name, scenarios in grouped.items()
    }
    selection = select_calibration_candidate(calibration_metrics, model)
    unique_fit_vectors = len(FIT_GROUPS) * SAMPLES_PER_GROUP
    unique_calibration_vectors = len(CAL_GROUPS) * len(SCENARIOS) * SAMPLES_PER_GROUP
    summary = {
        "schemaVersion": 1,
        "stage": "fit-calibration",
        "studyId": STUDY_ID,
        "experimentId": EXPERIMENT_ID,
        "freshHoldoutIdentityNotClaimed": True,
        "partitionDigest": partition_digest(),
        "groupCounts": {"fit": len(FIT_GROUPS), "calibration": len(CAL_GROUPS), "holdoutReservedForWindowsOnly": len(HOLDOUT_GROUPS)},
        "platform": _platform_record(environment, image_digest, commit),
        "sourceHashes": _source_hashes(),
        "helperAllowlist": {
            "numeric": sorted(NUMERIC_HELPER_ALLOWLIST),
            "episode": sorted(EPISODE_HELPER_ALLOWLIST),
        },
        "reference": model,
        "thresholds": thresholds,
        "calibrationMetrics": calibration_metrics,
        "selection": selection,
        "processing": {
            "uniqueGeneratedVectors": {"fit": unique_fit_vectors, "calibration": unique_calibration_vectors, "total": unique_fit_vectors + unique_calibration_vectors},
            "uniqueGeneratedScalarTargetValues": {"fit": unique_fit_vectors * 3, "calibration": unique_calibration_vectors * 3, "total": (unique_fit_vectors + unique_calibration_vectors) * 3},
            "sourceVectorPasses": {"fit": 2, "calibration": 1},
            "sourceVectorVisits": {"fit": unique_fit_vectors * 2, "calibration": generated_calibration_vectors + generated_event_vectors},
            "sourceScalarTargetVisits": {"fit": unique_fit_vectors * 2 * 3, "calibration": (generated_calibration_vectors + generated_event_vectors) * 3},
            "statisticScoreEvaluations": (generated_calibration_vectors + generated_event_vectors) * len(STATISTICS) * len(FORMS),
            "candidateThresholdDecisionEvaluations": (generated_calibration_vectors + generated_event_vectors) * len(candidate_rows),
            "experimentGroupIDsGenerated": list(FIT_GROUPS + CAL_GROUPS),
            "holdoutGroupIDsGenerated": [],
        },
        "resources": {
            "cpuSeconds": time.process_time() - started_cpu,
            "wallSeconds": time.perf_counter() - started_wall,
            "peakRssBytes": _peak_rss_bytes(),
            "workerCount": 1,
        },
    }
    if summary["resources"]["wallSeconds"] > MAX_WALL_SECONDS or summary["resources"]["peakRssBytes"] > MAX_RSS_BYTES:
        summary["resourceDisposition"] = "FAIL_RESOURCE_LIMIT"
    else:
        summary["resourceDisposition"] = "WITHIN_PREREGISTERED_LIMITS"
    _write_json(output_path, summary, refuse_overwrite=True)
    return summary


def _compare_tree(left: Any, right: Any, path: str, differences: list[dict[str, Any]], maximums: dict[str, float]) -> None:
    if isinstance(left, dict) and isinstance(right, dict):
        ignored = {"platform", "pythonVersion", "implementation", "imageDigest", "environment", "resources"}
        keys = (set(left) | set(right)) - ignored
        for key in sorted(keys):
            if key not in left or key not in right:
                differences.append({"path": f"{path}.{key}", "kind": "missing-key"})
            else:
                _compare_tree(left[key], right[key], f"{path}.{key}" if path else key, differences, maximums)
        return
    if isinstance(left, list) and isinstance(right, list):
        if len(left) != len(right):
            differences.append({"path": path, "kind": "length", "left": len(left), "right": len(right)})
            return
        for index, (a, b) in enumerate(zip(left, right)):
            _compare_tree(a, b, f"{path}[{index}]", differences, maximums)
        return
    if isinstance(left, bool) or isinstance(right, bool):
        if left is not right:
            differences.append({"path": path, "kind": "exact", "left": left, "right": right})
        return
    if isinstance(left, (int, float)) and isinstance(right, (int, float)):
        a, b = float(left), float(right)
        absolute = abs(a - b)
        relative = absolute / max(abs(a), abs(b), 1e-300)
        maximums["absolute"] = max(maximums.get("absolute", 0.0), absolute)
        maximums["relative"] = max(maximums.get("relative", 0.0), relative)
        tolerance = 1e-10 if path.startswith("reference.") else 1e-8 * max(abs(a), abs(b), 1e-12)
        if absolute > tolerance:
            differences.append({"path": path, "kind": "numeric-tolerance", "left": left, "right": right, "absoluteDifference": absolute, "allowedAbsoluteDifference": tolerance})
        return
    if left != right:
        differences.append({"path": path, "kind": "exact", "left": left, "right": right})


def compare_fit_calibration(windows_path: Path, linux_path: Path, output_path: Path) -> dict[str, Any]:
    windows = json.loads(windows_path.read_text(encoding="utf-8"))
    linux = json.loads(linux_path.read_text(encoding="utf-8"))
    differences: list[dict[str, Any]] = []
    maximums: dict[str, float] = {}
    _compare_tree(windows, linux, "", differences, maximums)
    report = {
        "schemaVersion": 1,
        "stage": "cross-platform-fit-calibration-comparison",
        "status": "PASS" if not differences else "CROSS_PLATFORM_DIVERGENCE",
        "exactIdentityCountsLabelsAndCandidateChoice": not differences,
        "tolerances": {"fitReferenceAbsolute": 1e-10, "thresholdsAndCalibrationMetricsRelative": 1e-8},
        "maximumObservedDifferences": maximums,
        "differenceCount": len(differences),
        "differences": differences[:50],
        "windowsArtifactSha256": _sha256_text(windows_path),
        "linuxArtifactSha256": _sha256_text(linux_path),
        "windowsSourceCommit": windows["platform"]["sourceCommit"],
        "linuxSourceCommit": linux["platform"]["sourceCommit"],
        "selectedCandidate": windows["selection"]["selectedCandidate"],
        "windowsSelection": windows["selection"],
        "linuxSelection": linux["selection"],
        "fitParameterTolerancePassed": not any(item["kind"] == "numeric-tolerance" and item["path"].startswith("reference.") for item in differences),
        "thresholdMetricTolerancePassed": not any(item["kind"] == "numeric-tolerance" and not item["path"].startswith("reference.") for item in differences),
    }
    _write_json(output_path, report, refuse_overwrite=True)
    return report


def freeze_configuration(
    windows_path: Path = FIT_WINDOWS_PATH,
    linux_path: Path = FIT_LINUX_PATH,
    comparison_path: Path = FIT_COMPARISON_PATH,
    output_path: Path = CONFIG_PATH,
) -> dict[str, Any]:
    comparison = json.loads(comparison_path.read_text(encoding="utf-8"))
    windows = json.loads(windows_path.read_text(encoding="utf-8"))
    if comparison.get("status") != "PASS":
        raise RuntimeError("configuration cannot be frozen after cross-platform divergence")
    if windows["platform"]["sourceCommit"] != comparison.get("linuxSourceCommit"):
        raise RuntimeError("Windows/Linux fit-calibration source commits differ")
    configuration = {
        "schemaVersion": 1,
        "studyId": STUDY_ID,
        "experimentId": EXPERIMENT_ID,
        "holdoutIdentity": HOLDOUT_ID,
        "seedNamespace": SEED_NAMESPACE,
        "baseCommit": BASE_COMMIT,
        "productCheckpoint": PRODUCT_CHECKPOINT,
        "partitionDigest": partition_digest(),
        "groupPartitions": {name: [ids[0], ids[-1]] for name, ids in PARTITIONS.items()},
        "protocolDigest": _sha256_text(PROTOCOL_PATH),
        "manifestDigest": _sha256_text(MANIFEST_PATH),
        "sourceHashes": windows["sourceHashes"],
        "fitCalibrationSourceCommit": windows["platform"]["sourceCommit"],
        "helperAllowlist": windows["helperAllowlist"],
        "reference": windows["reference"],
        "thresholds": windows["thresholds"],
        "calibrationMetrics": windows["calibrationMetrics"],
        "selection": windows["selection"],
        "crossPlatformComparison": {
            "status": comparison["status"],
            "artifactSha256": _sha256_text(comparison_path),
            "windowsFitCalibrationSha256": _sha256_text(windows_path),
            "linuxFitCalibrationSha256": _sha256_text(linux_path),
            "maximumObservedDifferences": comparison["maximumObservedDifferences"],
        },
        "frozenStudyContract": {
            "statistics": ["joint squared Mahalanobis distance", "marginal max(abs(z)) control"],
            "forms": ["instantaneous", "EWMA alpha 0.20"],
            "shrinkage": 0.05,
            "quantiles": list(QUANTILES),
            "holdoutPlatform": "Windows CPython 3.14.6 only",
            "holdoutReuseAllowed": False,
        },
    }
    _write_json(output_path, configuration, refuse_overwrite=True)
    return {"config": configuration, "normalizedTextSha256": _sha256_text(output_path), "canonicalJsonSha256": _sha256_json(configuration)}


def _bootstrap_mean_interval(values: Sequence[float], seed: int) -> list[float] | None:
    if not values:
        return None
    rng = SplitMix64(seed)
    count = len(values)
    means: list[float] = []
    for _ in range(BOOTSTRAP_REPLICATES):
        total = 0.0
        for _ in range(count):
            total += values[rng.next_u64() % count]
        means.append(total / count)
    return [nearest_rank_quantile(means, 0.025), nearest_rank_quantile(means, 0.975)]


def _paired_delta(left: Sequence[dict[str, Any]], right: Sequence[dict[str, Any]], scenario: str, metric: str, pair_name: str) -> dict[str, Any]:
    def group_value(record: dict[str, Any]) -> float | None:
        if metric == "pointExceedanceRate":
            return record["pointExceedances"] / record["eligiblePoints"] if record["eligiblePoints"] else None
        if metric == "episodesPer24EligibleSimulatedHours":
            return record["episodeCount"] / record["eligibleHours"] * 24.0 if record["eligibleHours"] else None
        if metric == "recall":
            return float(record["detected"]) if record["detected"] is not None else None
        if metric == "timelyFractionOfAllGroups":
            return float(record["within600Seconds"]) if record["within600Seconds"] is not None else None
        if metric == "latencySecondsAmongMatchedDetections":
            return float(record["latencySeconds"]) if record["latencySeconds"] is not None else None
        raise ValueError(metric)

    deltas: list[float] = []
    for left_row, right_row in zip(left, right):
        a, b = group_value(left_row), group_value(right_row)
        if a is not None and b is not None:
            deltas.append(a - b)
    if not deltas:
        return {"matchedGroups": 0, "leftMinusRightMean": None, "bootstrap95": None}
    seed_data = f"{BOOTSTRAP_NAMESPACE}|paired|{pair_name}|{scenario}|{metric}".encode("ascii")
    seed = int.from_bytes(hashlib.sha256(seed_data).digest()[:8], "big")
    return {
        "matchedGroups": len(deltas),
        "leftMinusRightMean": sum(deltas) / len(deltas),
        "bootstrap95": _bootstrap_mean_interval(deltas, seed),
    }


def _paired_comparisons(
    grouped: dict[str, dict[str, list[dict[str, Any]]]],
) -> dict[str, Any]:
    output: dict[str, Any] = {}
    for quantile in QUANTILES:
        qlabel = "q099" if quantile == 0.99 else "q0999"
        pairs = (
            (f"joint-ewma-{qlabel}", f"joint-instant-{qlabel}"),
            (f"joint-instant-{qlabel}", f"marginal-instant-{qlabel}"),
            (f"joint-ewma-{qlabel}", f"marginal-ewma-{qlabel}"),
        )
        for left_name, right_name in pairs:
            pair_key = f"{left_name}-minus-{right_name}"
            output[pair_key] = {}
            for scenario in SCENARIOS:
                left_rows = grouped[left_name][scenario]
                right_rows = grouped[right_name][scenario]
                metrics = ["pointExceedanceRate", "episodesPer24EligibleSimulatedHours"]
                if scenario != "nonfault-context":
                    metrics.extend(["recall", "timelyFractionOfAllGroups", "latencySecondsAmongMatchedDetections"])
                output[pair_key][scenario] = {
                    metric: _paired_delta(left_rows, right_rows, scenario, metric, pair_key)
                    for metric in metrics
                }
    return output


def _target_disposition(candidate_metrics: dict[str, Any]) -> dict[str, Any]:
    def disposition(value: float | None, threshold: float, direction: str, groups: int) -> dict[str, Any]:
        if groups < 100 or value is None:
            return {"status": "INSUFFICIENT", "estimate": value, "target": threshold, "direction": direction}
        passed = value <= threshold if direction == "max" else value >= threshold
        return {
            "status": "PASS_POINT_ESTIMATE" if passed else "FAIL_POINT_ESTIMATE",
            "estimate": value,
            "target": threshold,
            "direction": direction,
            "uncertaintyReportedSeparately": True,
        }

    normal = candidate_metrics["nonfault-context"]
    groups = int(normal["independentSourceGroups"])
    result: dict[str, Any] = {
        "pointFalsePositiveRate": disposition(normal["pointExceedanceRate"], 0.01, "max", groups),
        "falseEpisodesPer24EligibleSimulatedHours": disposition(normal["episodesPer24EligibleSimulatedHours"], 1.0, "max", groups),
        "strongClasses": {},
    }
    for scenario in STRONG_SCENARIOS:
        metrics = candidate_metrics[scenario]
        class_groups = int(metrics["independentSourceGroups"])
        result["strongClasses"][scenario] = {
            "recall": disposition(metrics.get("recall"), 0.90, "min", class_groups),
            "within600SecondsOfAllGroups": disposition(metrics.get("timelyFractionOfAllGroups"), 0.90, "min", class_groups),
        }
    return result


def _canonical_reservation_root() -> Path:
    if os.name == "nt":
        local_app_data = Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData" / "Local"))
        return local_app_data / "EliteSCADA" / "research-claims" / "w15-intelligence-stage4f-temporal-study"
    return Path.home() / ".local" / "share" / "EliteSCADA" / "research-claims" / "w15-intelligence-stage4f-temporal-study"


def _claim_path(identity: str, reservation_root: Path) -> Path:
    key = hashlib.sha256(identity.encode("utf-8")).hexdigest()
    return reservation_root / f"{key}.json"


def _claim_identity_once(
    identity: str,
    payload: dict[str, Any],
    *,
    reservation_root: Path | None = None,
    committed_receipt_path: Path | None = None,
    caller_scratch: Path | None = None,
) -> Path:
    del caller_scratch  # Caller scratch is deliberately irrelevant to identity ownership.
    if committed_receipt_path is not None and committed_receipt_path.exists():
        try:
            committed = json.loads(committed_receipt_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise RuntimeError("committed one-use receipt is unreadable; hard stop") from exc
        if committed.get("holdoutIdentity") != identity:
            raise RuntimeError("committed receipt path belongs to a different identity; hard stop")
        raise RuntimeError("holdout identity is already committed; hard stop")
    root = reservation_root or _canonical_reservation_root()
    root.mkdir(parents=True, exist_ok=True)
    claim_path = _claim_path(identity, root)
    record = {**payload, "holdoutIdentity": identity, "state": "reserved", "reservedAtUtc": datetime.now(timezone.utc).isoformat()}
    encoded = (json.dumps(record, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")
    flags = os.O_CREAT | os.O_EXCL | os.O_WRONLY
    if hasattr(os, "O_BINARY"):
        flags |= os.O_BINARY
    try:
        descriptor = os.open(claim_path, flags, 0o600)
    except FileExistsError as exc:
        raise RuntimeError("holdout identity is already reserved or consumed; hard stop") from exc
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(encoded)
        stream.flush()
        os.fsync(stream.fileno())
    return claim_path


def _update_reservation(claim_path: Path, **changes: Any) -> None:
    record = json.loads(claim_path.read_text(encoding="utf-8"))
    record.update(changes)
    temporary = claim_path.with_name(claim_path.name + ".state.tmp")
    if temporary.exists():
        raise RuntimeError("stale reservation-state temporary file exists; hard stop")
    data = (json.dumps(record, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")
    with temporary.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, claim_path)


def _claim_then_execute(
    identity: str,
    payload: dict[str, Any],
    action: Any,
    *,
    reservation_root: Path | None = None,
    committed_receipt_path: Path | None = None,
    caller_scratch: Path | None = None,
) -> Any:
    claim_path = _claim_identity_once(
        identity, payload, reservation_root=reservation_root,
        committed_receipt_path=committed_receipt_path, caller_scratch=caller_scratch,
    )
    try:
        result = action()
    except Exception as exc:
        _update_reservation(claim_path, state="failed", failureType=type(exc).__name__, failedAtUtc=datetime.now(timezone.utc).isoformat())
        raise
    _update_reservation(claim_path, state="completed", completedAtUtc=datetime.now(timezone.utc).isoformat())
    return result


def _verify_frozen_config(config_path: Path) -> tuple[dict[str, Any], dict[str, str]]:
    if HOLDOUT_AGGREGATE_PATH.exists() or HOLDOUT_RECEIPT_PATH.exists():
        raise RuntimeError("holdout aggregate or committed receipt already exists; one-use hard stop")
    config = json.loads(config_path.read_text(encoding="utf-8"))
    if config.get("experimentId") != EXPERIMENT_ID or config.get("holdoutIdentity") != HOLDOUT_ID:
        raise RuntimeError("frozen configuration does not bind the fresh preregistered identity")
    if config.get("partitionDigest") != partition_digest() or config.get("seedNamespace") != SEED_NAMESPACE:
        raise RuntimeError("frozen configuration partition or seed namespace differs")
    observed = _source_hashes()
    if config.get("sourceHashes") != observed:
        raise RuntimeError("current protocol/manifest/source/test/helper digest differs from frozen configuration")
    if config.get("protocolDigest") != observed["protocol-v1.md"] or config.get("manifestDigest") != observed["experiment-manifest-v1.json"]:
        raise RuntimeError("protocol or manifest digest differs from frozen configuration")
    if not _git_clean():
        raise RuntimeError("holdout requires a clean checkout at the committed frozen-configuration head")
    return config, observed


def _run_holdout_analysis(config: dict[str, Any]) -> dict[str, Any]:
    specs = candidate_specs()
    grouped: dict[str, dict[str, list[dict[str, Any]]]] = {
        spec["name"]: {scenario: [] for scenario in SCENARIOS} for spec in specs
    }
    generated_vectors = 0
    statistic_score_evaluations = 0
    candidate_decision_evaluations = 0
    model = config["reference"]
    for group_id in HOLDOUT_GROUPS:
        for scenario in SCENARIOS:
            rows = generate_run(group_id, scenario, model["robustScales"])
            generated_vectors += len(rows)
            scores = score_run(rows, model)
            statistic_score_evaluations += len(rows) * len(STATISTICS) * len(FORMS)
            candidate_decision_evaluations += len(rows) * len(specs)
            onset = None if scenario == "nonfault-context" else EVENT_START
            for spec in specs:
                method = f"{spec['statistic']}-{spec['form']}"
                grouped[spec["name"]][scenario].append(
                    summarize_run(scores[method], config["thresholds"][spec["name"]], onset)
                )

    candidate_metrics: dict[str, Any] = {}
    for spec in specs:
        name = spec["name"]
        candidate_metrics[name] = {}
        for scenario, records in grouped[name].items():
            metrics = _aggregate_group_records(records, include_event=scenario != "nonfault-context")
            metrics["bootstrap95"] = _bootstrap_intervals(
                records, include_event=scenario != "nonfault-context", seed=_bootstrap_seed(name, scenario)
            )
            if scenario == "nonfault-context" and metrics["episodeCount"] == 0:
                metrics["zeroEpisodeWilsonGroupOccurrence95"] = metrics["wilsonGroupOccurrence95"]
            candidate_metrics[name][scenario] = metrics
        if spec["statistic"] == "joint":
            candidate_metrics[name]["targetDisposition"] = _target_disposition(candidate_metrics[name])
        else:
            candidate_metrics[name]["targetDisposition"] = "CONTROL_ONLY_NOT_ELIGIBLE_FOR_SELECTION"

    paired = _paired_comparisons(grouped)
    unique_vectors = len(HOLDOUT_GROUPS) * len(SCENARIOS) * SAMPLES_PER_GROUP
    return {
        "candidateMetrics": candidate_metrics,
        "pairedComparisons": paired,
        "processing": {
            "uniqueGeneratedVectors": unique_vectors,
            "uniqueGeneratedScalarTargetValues": unique_vectors * 3,
            "sourceVectorPasses": 1,
            "sourceVectorVisits": generated_vectors,
            "sourceScalarTargetVisits": generated_vectors * 3,
            "statisticScoreEvaluations": statistic_score_evaluations,
            "candidateThresholdDecisionEvaluations": candidate_decision_evaluations,
            "experimentGroupIDsGenerated": list(HOLDOUT_GROUPS),
            "holdoutGroupIDsGenerated": list(HOLDOUT_GROUPS),
            "scenarioRuns": len(HOLDOUT_GROUPS) * len(SCENARIOS),
            "rowLevelFilesWritten": False,
        },
    }


def run_holdout(config_path: Path = CONFIG_PATH) -> dict[str, Any]:
    """Claim the one new Windows identity before the first holdout generator call."""
    config, source_hashes = _verify_frozen_config(config_path)
    if os.name != "nt" or platform.python_version() != "3.14.6":
        raise RuntimeError("final holdout is predeclared for Windows CPython 3.14.6 only")
    source_commit = _git_head()
    claim_payload = {
        "studyId": STUDY_ID,
        "experimentId": EXPERIMENT_ID,
        "seedNamespace": SEED_NAMESPACE,
        "configCanonicalSha256": _sha256_json(config),
        "configFileSha256": _sha256_text(config_path),
        "protocolDigest": source_hashes["protocol-v1.md"],
        "manifestDigest": source_hashes["experiment-manifest-v1.json"],
        "sourceHashes": source_hashes,
        "partitionDigest": partition_digest(),
        "preHoldoutCommit": source_commit,
        "designatedPlatform": "Windows CPython 3.14.6",
        "previousHoldoutIdentityReadOrReused": False,
    }
    started_wall = time.perf_counter()
    started_cpu = time.process_time()

    def perform() -> dict[str, Any]:
        analysis = _run_holdout_analysis(config)
        resources = {
            "cpuSeconds": time.process_time() - started_cpu,
            "wallSeconds": time.perf_counter() - started_wall,
            "peakRssBytes": _peak_rss_bytes(),
            "workerCount": 1,
            "pythonVersion": platform.python_version(),
            "os": platform.platform(),
        }
        resource_disposition = "WITHIN_PREREGISTERED_LIMITS"
        if resources["wallSeconds"] > MAX_WALL_SECONDS or resources["peakRssBytes"] > MAX_RSS_BYTES:
            resource_disposition = "FAIL_RESOURCE_LIMIT"
        aggregate = {
            "schemaVersion": 1,
            "studyId": STUDY_ID,
            "experimentId": EXPERIMENT_ID,
            "holdoutIdentity": HOLDOUT_ID,
            "configurationCanonicalSha256": claim_payload["configCanonicalSha256"],
            "configurationFileSha256": claim_payload["configFileSha256"],
            "preHoldoutCommit": source_commit,
            "partitionDigest": partition_digest(),
            "candidateThresholds": config["thresholds"],
            "calibrationSelection": config["selection"],
            "holdoutAnalysis": analysis,
            "resources": resources,
            "resourceDisposition": resource_disposition,
            "syntheticEvidenceOnly": True,
            "realFieldExposureStatus": "UNMEASURED",
            "productMethodApproval": False,
        }
        _write_json(HOLDOUT_AGGREGATE_PATH, aggregate, refuse_overwrite=True)
        receipt = {
            "schemaVersion": 1,
            "studyId": STUDY_ID,
            "experimentId": EXPERIMENT_ID,
            "holdoutIdentity": HOLDOUT_ID,
            "state": "consumed-completed",
            "designatedPlatform": "Windows CPython 3.14.6",
            "preHoldoutCommit": source_commit,
            "configurationCanonicalSha256": claim_payload["configCanonicalSha256"],
            "configurationFileSha256": claim_payload["configFileSha256"],
            "protocolDigest": claim_payload["protocolDigest"],
            "manifestDigest": claim_payload["manifestDigest"],
            "sourceHashes": source_hashes,
            "partitionDigest": partition_digest(),
            "aggregateFile": HOLDOUT_AGGREGATE_PATH.name,
            "aggregateNormalizedTextSha256": _sha256_text(HOLDOUT_AGGREGATE_PATH),
            "uniqueGeneratedVectors": analysis["processing"]["uniqueGeneratedVectors"],
            "uniqueGeneratedScalarTargetValues": analysis["processing"]["uniqueGeneratedScalarTargetValues"],
            "completedAtUtc": datetime.now(timezone.utc).isoformat(),
            "retryAllowed": False,
        }
        _write_json(HOLDOUT_RECEIPT_PATH, receipt, refuse_overwrite=True)
        return {"aggregate": aggregate, "receipt": receipt}

    return _claim_then_execute(
        HOLDOUT_ID, claim_payload, perform,
        committed_receipt_path=HOLDOUT_RECEIPT_PATH,
    )


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Fresh synthetic-only Stage 4F temporal research study")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("test", help="run only separate UNIT fixtures")

    fit_parser = commands.add_parser("fit-calibration", help="fit IDs 0-59 and calibrate IDs 60-119; never generate holdout")
    fit_parser.add_argument("--environment", choices=("windows", "linux"), required=True)
    fit_parser.add_argument("--image-digest", default=None)
    fit_parser.add_argument("--source-commit", default=None)
    fit_parser.add_argument("--output", type=Path, default=FIT_WINDOWS_PATH)

    compare_parser = commands.add_parser("compare", help="compare frozen Windows/Linux fit-calibration outputs")
    compare_parser.add_argument("--windows", type=Path, default=FIT_WINDOWS_PATH)
    compare_parser.add_argument("--linux", type=Path, default=FIT_LINUX_PATH)
    compare_parser.add_argument("--output", type=Path, default=FIT_COMPARISON_PATH)

    freeze_parser = commands.add_parser("freeze", help="freeze the agreed configuration before holdout")
    freeze_parser.add_argument("--windows", type=Path, default=FIT_WINDOWS_PATH)
    freeze_parser.add_argument("--linux", type=Path, default=FIT_LINUX_PATH)
    freeze_parser.add_argument("--comparison", type=Path, default=FIT_COMPARISON_PATH)
    freeze_parser.add_argument("--output", type=Path, default=CONFIG_PATH)

    holdout_parser = commands.add_parser("holdout", help="consume the one Windows-only final holdout")
    holdout_parser.add_argument("--config", type=Path, default=CONFIG_PATH)
    args = parser.parse_args(argv)

    if args.command == "test":
        import unittest

        suite = unittest.defaultTestLoader.discover(str(HERE), pattern="test_bench.py")
        result = unittest.TextTestRunner(verbosity=2).run(suite)
        return 0 if result.wasSuccessful() else 1
    if args.command == "fit-calibration":
        run_fit_calibration(
            args.output,
            environment=args.environment,
            image_digest=args.image_digest,
            source_commit=args.source_commit,
        )
        return 0
    if args.command == "compare":
        compare_fit_calibration(args.windows, args.linux, args.output)
        return 0
    if args.command == "freeze":
        freeze_configuration(args.windows, args.linux, args.comparison, args.output)
        return 0
    if args.command == "holdout":
        run_holdout(args.config)
        return 0
    parser.error("unsupported command")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
