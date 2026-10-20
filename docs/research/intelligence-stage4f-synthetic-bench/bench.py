"""Offline, synthetic-only Stage 4F motor/pump research bench.

This module is deliberately disconnected from EliteSCADA product code and data.
It streams deterministic synthetic source groups and persists only aggregates.
"""

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
import tempfile
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence


HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
PROTOCOL_PATH = HERE / "protocol-v3.md"
MANIFEST_PATH = HERE / "experiment-manifest-v1.json"
OLD_NUMERIC_SOURCE = REPO / "docs/research/intelligence-stage4f/research_harness.py"
TEST_SOURCE = HERE / "test_bench.py"
EXPERIMENT_ID = "W15-INTEL-4F-SYNTH-BENCH-2026-10-20-01"
HOLDOUT_ID = "W15-INTEL-4F-SYNTH-BENCH-HOLDOUT-2026-10-20-01"
SEED_NAMESPACE = "ELITESCADA-W15-4F-SYNTHETIC-BENCH-v1"
CADENCE_S = 10
SAMPLES_PER_GROUP = 120
EVENT_START = 12
EVENT_END = SAMPLES_PER_GROUP
STRONG_SHIFT = 3.5
LAMBDA_GRID = (0.05, 0.10, 0.25, 0.50)
STRONG_CLASSES = ("single-current-strong", "multi-target-strong", "relationship-strong")
EXPLORATORY_CLASSES = ("single-current-1scale", "single-current-2scale", "single-current-ramp")
SCENARIOS = ("nonfault-context",) + STRONG_CLASSES + EXPLORATORY_CLASSES
FIT_GROUPS = tuple(range(0, 360))
CAL_GROUPS = tuple(range(360, 480))
HOLDOUT_GROUPS = tuple(range(480, 600))
PARTITIONS = {"fit": FIT_GROUPS, "calibration": CAL_GROUPS, "holdout": HOLDOUT_GROUPS}
TARGET_NAMES = ("motor_current", "flow", "discharge_pressure")
TARGET_SDS = (0.70, 1.00, 0.10)
R0 = ((1.0, 0.65, 0.25), (0.65, 1.0, 0.20), (0.25, 0.20, 1.0))
R1 = ((1.0, -0.65, 0.25), (-0.65, 1.0, 0.20), (0.25, 0.20, 1.0))
AR_PHI = 0.65
MAX_CONDITION = 1_000_000.0
BOOTSTRAP_REPLICATES = 2_000
TARGETS = (0.01, 1.0, 0.90, 0.90)  # point FP, episodes/24 h, recall, within-10-minute fraction
MASK64 = (1 << 64) - 1


def _canonical_text_bytes(raw: bytes) -> bytes:
    return raw.replace(b"\r\n", b"\n").replace(b"\r", b"\n")


def _sha256_bytes(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest().upper()


def _sha256_text(path: Path) -> str:
    return _sha256_bytes(_canonical_text_bytes(path.read_bytes()))


def _canonical_json(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def _sha256_json(value: Any) -> str:
    return _sha256_bytes(_canonical_json(value))


def _load_safe_numerics() -> dict[str, Any]:
    """Compile only reviewed pure helpers, not the prior harness or its guards."""
    tree = ast.parse(OLD_NUMERIC_SOURCE.read_text(encoding="utf-8"), filename=str(OLD_NUMERIC_SOURCE))
    wanted_functions = {
        "median", "fit_robust_scaling", "transform_rows", "covariance_matrix",
        "jacobi_eigenvalues", "rank_and_condition", "shrink_covariance", "cholesky",
        "mahalanobis_squared", "independent_max_abs", "nearest_rank_quantile",
    }
    nodes: list[ast.stmt] = [ast.ImportFrom(module="__future__", names=[ast.alias(name="annotations")], level=0)]
    for node in tree.body:
        if isinstance(node, ast.ClassDef) and node.name == "ResearchDataError":
            nodes.append(node)
        elif isinstance(node, ast.FunctionDef) and node.name in wanted_functions:
            nodes.append(node)
        elif isinstance(node, ast.Assign) and any(
            isinstance(target, ast.Name) and target.id in {"MAD_NORMAL_SCALE", "EIGEN_RELATIVE_TOLERANCE"}
            for target in node.targets
        ):
            nodes.append(node)
    namespace: dict[str, Any] = {"math": math, "Iterable": Iterable, "Sequence": Sequence}
    exec(compile(ast.fix_missing_locations(ast.Module(body=nodes, type_ignores=[])), str(OLD_NUMERIC_SOURCE), "exec"), namespace)
    required = wanted_functions | {"ResearchDataError", "MAD_NORMAL_SCALE", "EIGEN_RELATIVE_TOLERANCE"}
    if not required.issubset(namespace):
        raise RuntimeError(f"safe numerical subset incomplete: {sorted(required - namespace.keys())}")
    return namespace


_NUM = _load_safe_numerics()
ResearchDataError = _NUM["ResearchDataError"]
median = _NUM["median"]
fit_robust_scaling = _NUM["fit_robust_scaling"]
transform_rows = _NUM["transform_rows"]
covariance_matrix = _NUM["covariance_matrix"]
jacobi_eigenvalues = _NUM["jacobi_eigenvalues"]
rank_and_condition = _NUM["rank_and_condition"]
shrink_covariance = _NUM["shrink_covariance"]
cholesky = _NUM["cholesky"]
mahalanobis_squared = _NUM["mahalanobis_squared"]
independent_max_abs = _NUM["independent_max_abs"]
nearest_rank_quantile = _NUM["nearest_rank_quantile"]
REUSED_NUMERICAL_SYMBOLS = tuple(sorted(name for name in _NUM if name in {
    "median", "fit_robust_scaling", "transform_rows", "covariance_matrix",
    "jacobi_eigenvalues", "rank_and_condition", "shrink_covariance", "cholesky",
    "mahalanobis_squared", "independent_max_abs", "nearest_rank_quantile",
}))


@dataclass(frozen=True)
class Observation:
    group_id: int
    sample_index: int
    source_time_s: int | None
    targets: tuple[float | None, float | None, float | None]
    running: bool | None
    speed_rpm: float | None
    load_fraction: float | None
    quality: str
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


def derive_seed(group_id: int) -> int:
    raw = f"{SEED_NAMESPACE}|group-{group_id:03d}".encode("ascii")
    return int.from_bytes(hashlib.sha256(raw).digest()[:8], "big")


def partition_for_group(group_id: int) -> str:
    for name, group_ids in PARTITIONS.items():
        if group_id in group_ids:
            return name
    raise ValueError(f"group id outside frozen partition: {group_id}")


def partition_digest() -> str:
    return _sha256_json({name: list(ids) for name, ids in PARTITIONS.items()})


def validate_independent_groups(fit_groups: Sequence[int], calibration_groups: Sequence[int], holdout_groups: Sequence[int]) -> None:
    parts = {"fit": tuple(fit_groups), "calibration": tuple(calibration_groups), "holdout": tuple(holdout_groups)}
    if parts != PARTITIONS:
        raise ResearchDataError("group assignment differs from the frozen 360/120/120 whole-run split")
    flattened = [group_id for ids in parts.values() for group_id in ids]
    if len(flattened) != len(set(flattened)):
        raise ResearchDataError("independent source group appears in more than one partition")
    if len(parts["holdout"]) < 100:
        raise ResearchDataError("fewer than 100 independent final holdout source groups")


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


def generate_trace(group_id: int, scenario: str = "nonfault-context", fit_scales: Sequence[float] | None = None) -> list[Observation]:
    if group_id not in range(600):
        raise ValueError("group id must be in the frozen range 0..599")
    if scenario not in SCENARIOS:
        raise ValueError(f"unknown scenario: {scenario}")
    if scenario != "nonfault-context" and (fit_scales is None or len(fit_scales) != 3):
        raise ValueError("injected scenarios require three fit-only residual scales")
    seed = derive_seed(group_id)
    rng = SplitMix64(seed)
    factor0 = cholesky(R0)
    factor1 = cholesky(R1)
    state = [0.0, 0.0, 0.0]
    ar_innovation_scale = math.sqrt(1.0 - AR_PHI * AR_PHI)
    traces: list[Observation] = []
    for index in range(SAMPLES_PER_GROUP):
        independent = [rng.normal_approx() for _ in range(3)]
        factor = factor1 if scenario == "relationship-strong" and index >= EVENT_START else factor0
        correlated = [sum(factor[row][column] * independent[column] for column in range(row + 1)) for row in range(3)]
        state = [AR_PHI * state[j] + ar_innovation_scale * correlated[j] for j in range(3)]
        running, speed, load = _context(group_id, index)
        expected = _expected_targets(speed, load)
        targets = [expected[j] + state[j] * TARGET_SDS[j] for j in range(3)]
        if scenario != "nonfault-context" and index >= EVENT_START:
            scales = tuple(float(value) for value in fit_scales or ())
            if scenario == "single-current-strong":
                targets[0] += STRONG_SHIFT * scales[0]
            elif scenario == "multi-target-strong":
                for j in range(3):
                    targets[j] += STRONG_SHIFT * scales[j]
            elif scenario == "single-current-1scale":
                targets[0] += scales[0]
            elif scenario == "single-current-2scale":
                targets[0] += 2.0 * scales[0]
            elif scenario == "single-current-ramp":
                ramp_fraction = min(1.0, (index - EVENT_START) / 60.0)
                targets[0] += STRONG_SHIFT * ramp_fraction * scales[0]
        traces.append(Observation(
            group_id=group_id, sample_index=index, source_time_s=index * CADENCE_S,
            targets=tuple(targets), running=running, speed_rpm=speed, load_fraction=load,
            quality="Good", source_time_verified=True, source_time_synchronized=True,
            context_id="pump-profile-v1", profile_version="model-v1",
        ))
    return traces


def validate_trace(
    rows: Sequence[Observation], cadence_s: int | None = CADENCE_S,
    expected_context_id: str = "pump-profile-v1", expected_version: str = "model-v1",
) -> list[Eligibility]:
    if cadence_s is None or cadence_s <= 0:
        return [Eligibility(False, "unknown-cadence", True) for _ in rows]
    results: list[Eligibility] = []
    seen: set[int] = set()
    previous_time: int | None = None
    previous_eligible = True
    for row in rows:
        reason: str | None = None
        break_before = not previous_eligible
        if len(row.targets) != 3 or any(value is None for value in row.targets):
            reason = "missing-required-target"
        elif any(not math.isfinite(float(value)) for value in row.targets if value is not None):
            reason = "nonfinite-target"
        elif row.running is None or row.speed_rpm is None or row.load_fraction is None:
            reason = "missing-required-context"
        elif row.running is not True:
            reason = "context-not-running"
        elif not math.isfinite(float(row.speed_rpm)) or not math.isfinite(float(row.load_fraction)):
            reason = "nonfinite-context"
        elif not 1430.0 <= float(row.speed_rpm) <= 1470.0 or not 0.25 <= float(row.load_fraction) <= 0.80:
            reason = "context-outside-profile"
        elif row.quality != "Good":
            reason = "quality-not-good"
        elif row.source_time_s is None:
            reason = "missing-source-time"
        elif not row.source_time_verified:
            reason = "source-time-unverified"
        elif not row.source_time_synchronized:
            reason = "source-time-unsynchronized"
        elif row.source_time_s in seen:
            reason = "duplicate-source-time"
        elif row.source_time_s % cadence_s != 0 or row.source_time_s != row.sample_index * cadence_s:
            reason = "misaligned-source-time"
        elif row.context_id != expected_context_id:
            reason = "context-mismatch"
        elif row.profile_version != expected_version:
            reason = "stale-profile-version"
        if reason is not None:
            results.append(Eligibility(False, reason, True))
            previous_eligible = False
            if row.source_time_s is not None:
                seen.add(row.source_time_s)
            continue
        if row.source_time_s is not None:
            seen.add(row.source_time_s)
        if previous_time is not None and row.source_time_s is not None:
            delta = row.source_time_s - previous_time
            if delta <= 0:
                results.append(Eligibility(False, "source-time-not-increasing", True))
                previous_eligible = False
                continue
            if delta != cadence_s:
                break_before = True
                gap_reason = "gap-over-limit" if delta > 2 * cadence_s else "missing-expected-sample"
                results.append(Eligibility(True, gap_reason, True))
                previous_time = row.source_time_s
                previous_eligible = True
                continue
        results.append(Eligibility(True, None, break_before))
        previous_time = row.source_time_s
        previous_eligible = True
    return results


def context_features(row: Observation) -> tuple[float, float, float]:
    assert row.speed_rpm is not None and row.load_fraction is not None
    return (1.0, row.load_fraction, (row.speed_rpm - 1450.0) / 20.0)


def _solve_linear(matrix: Sequence[Sequence[float]], vector: Sequence[float]) -> list[float]:
    size = len(vector)
    if size == 0 or len(matrix) != size or any(len(row) != size for row in matrix):
        raise ResearchDataError("linear system shape mismatch")
    augmented = [[float(matrix[i][j]) for j in range(size)] + [float(vector[i])] for i in range(size)]
    scale = max(1.0, max(abs(value) for row in matrix for value in row))
    for column in range(size):
        pivot = max(range(column, size), key=lambda row: abs(augmented[row][column]))
        if not math.isfinite(augmented[pivot][column]) or abs(augmented[pivot][column]) <= 1e-12 * scale:
            raise ResearchDataError("singular/ill-conditioned context design")
        augmented[column], augmented[pivot] = augmented[pivot], augmented[column]
        divisor = augmented[column][column]
        augmented[column] = [value / divisor for value in augmented[column]]
        for row in range(size):
            if row == column:
                continue
            multiplier = augmented[row][column]
            augmented[row] = [augmented[row][i] - multiplier * augmented[column][i] for i in range(size + 1)]
    result = [augmented[i][-1] for i in range(size)]
    if any(not math.isfinite(value) for value in result):
        raise ResearchDataError("context coefficients are nonfinite")
    return result


def fit_reference_model() -> dict[str, Any]:
    validate_independent_groups(FIT_GROUPS, CAL_GROUPS, HOLDOUT_GROUPS)
    xtx = [[0.0] * 3 for _ in range(3)]
    xty = [[0.0] * 3 for _ in range(3)]
    for group_id in FIT_GROUPS:
        for row in generate_trace(group_id):
            x = context_features(row)
            for i in range(3):
                for j in range(3):
                    xtx[i][j] += x[i] * x[j]
                for target in range(3):
                    xty[target][i] += x[i] * float(row.targets[target])
    design_eigenvalues = jacobi_eigenvalues(xtx)
    design_rank, design_condition = rank_and_condition(design_eigenvalues)
    if design_rank != 3 or design_condition is None or design_condition > MAX_CONDITION:
        raise ResearchDataError("context design is singular or ill-conditioned")
    coefficients = [_solve_linear(xtx, xty[target]) for target in range(3)]
    residual_rows: list[tuple[float, float, float]] = []
    for group_id in FIT_GROUPS:
        for row in generate_trace(group_id):
            x = context_features(row)
            residual_rows.append(tuple(
                float(row.targets[target]) - sum(coefficients[target][i] * x[i] for i in range(3))
                for target in range(3)
            ))
    centers, scales = fit_robust_scaling(residual_rows)
    transformed = transform_rows(residual_rows, centers, scales)
    covariance, covariance_mean = covariance_matrix(transformed)
    eigenvalues = jacobi_eigenvalues(covariance)
    rank, condition = rank_and_condition(eigenvalues)
    if rank != 3 or condition is None or not math.isfinite(condition):
        raise ResearchDataError("fit covariance is singular or ill-conditioned")
    candidates: dict[str, dict[str, Any]] = {}
    for shrinkage in LAMBDA_GRID:
        key = f"joint-{shrinkage:.2f}"
        regularized = shrink_covariance(covariance, shrinkage)
        values = jacobi_eigenvalues(regularized)
        candidate_rank, candidate_condition = rank_and_condition(values)
        status = "eligible"
        factor: list[list[float]] | None = None
        if candidate_rank != 3 or candidate_condition is None or candidate_condition > MAX_CONDITION:
            status = "ill-conditioned"
        else:
            try:
                factor = cholesky(regularized)
            except ResearchDataError:
                status = "non-positive-definite"
        candidates[key] = {
            "kind": "joint", "shrinkage": shrinkage, "condition": candidate_condition,
            "rank": candidate_rank, "factor": factor, "status": status,
        }
    candidates["independent-control"] = {
        "kind": "independent", "shrinkage": None, "condition": None,
        "rank": 3, "factor": None, "status": "eligible",
    }
    return {
        "contextCoefficients": coefficients,
        "robustCenters": centers,
        "robustScales": scales,
        "covariance": covariance,
        "covarianceMean": covariance_mean,
        "contextDesignRank": design_rank,
        "contextDesignCondition": design_condition,
        "unregularizedRank": rank,
        "unregularizedCondition": condition,
        "candidates": candidates,
    }


def _score_one(row: Observation, model: dict[str, Any], candidate: dict[str, Any]) -> float:
    x = context_features(row)
    residual = [
        (float(row.targets[j]) - sum(model["contextCoefficients"][j][i] * x[i] for i in range(3)) - model["robustCenters"][j]) / model["robustScales"][j]
        - model["covarianceMean"][j]
        for j in range(3)
    ]
    if any(not math.isfinite(value) for value in residual):
        raise ResearchDataError("nonfinite standardized residual")
    if candidate["kind"] == "joint":
        factor = candidate.get("factor")
        if factor is None:
            raise ResearchDataError("candidate factor unavailable")
        return mahalanobis_squared(residual, factor)
    return independent_max_abs(residual)


def score_trace(rows: Sequence[Observation], model: dict[str, Any]) -> dict[str, list[ScoreDecision]]:
    eligibility = validate_trace(rows)
    output = {key: [] for key, candidate in model["candidates"].items() if candidate["status"] == "eligible"}
    for row, gate in zip(rows, eligibility):
        for key in output:
            candidate = model["candidates"][key]
            score = _score_one(row, model, candidate) if gate.eligible else None
            output[key].append(ScoreDecision(row.sample_index, row.source_time_s, score, gate.eligible, gate.reason, gate.break_before))
    return output


def find_episodes(decisions: Sequence[ScoreDecision], threshold: float) -> list[int]:
    starts: list[int] = []
    history: list[bool] = []
    active = False
    below_run = 0
    for decision in decisions:
        if not decision.eligible or decision.break_before:
            history.clear()
            active = False
            below_run = 0
            if not decision.eligible:
                continue
        above = decision.score is not None and decision.score > threshold
        if above:
            below_run = 0
            history.append(True)
            history = history[-5:]
            if not active and sum(history) >= 3:
                active = True
                starts.append(decision.sample_index)
        else:
            history.append(False)
            history = history[-5:]
            if active:
                below_run += 1
                if below_run >= 5:
                    active = False
                    below_run = 0
                    history.clear()
            else:
                below_run = 0
    return starts


def _trace_summary(decisions: Sequence[ScoreDecision], threshold: float, onset_index: int | None = None) -> dict[str, Any]:
    eligible = [d for d in decisions if d.eligible and d.score is not None]
    point_exceedances = sum(d.score > threshold for d in eligible)
    starts = find_episodes(decisions, threshold)
    result: dict[str, Any] = {
        "eligiblePoints": len(eligible),
        "pointExceedances": point_exceedances,
        "episodeStarts": starts,
        "episodeCount": len(starts),
        "preOnsetEpisodes": 0,
        "detected": None,
        "latencySeconds": None,
        "within10Minutes": None,
    }
    if onset_index is not None:
        result["preOnsetEpisodes"] = sum(start < onset_index for start in starts)
        event_starts = [start for start in starts if start >= onset_index]
        if event_starts:
            latency = (event_starts[0] - onset_index) * CADENCE_S
            result["detected"] = True
            result["latencySeconds"] = latency
            result["within10Minutes"] = latency <= 600
        else:
            result["detected"] = False
            result["within10Minutes"] = False
    return result


def _percentile(values: Sequence[float], q: float) -> float | None:
    if not values:
        return None
    ordered = sorted(float(value) for value in values)
    index = min(len(ordered) - 1, max(0, math.ceil(q * len(ordered)) - 1))
    return ordered[index]


def _bootstrap_ci(blocks: Sequence[dict[str, float]], metric: str, denominator: str | None = None, seed: int = 0x6714F) -> list[float] | None:
    if not blocks:
        return None
    rng = SplitMix64(seed)
    values: list[float] = []
    n = len(blocks)
    for _ in range(BOOTSTRAP_REPLICATES):
        selected = [blocks[rng.next_u64() % n] for _ in range(n)]
        numerator = sum(block.get(metric, 0.0) for block in selected)
        divisor = sum(block.get(denominator, 0.0) for block in selected) if denominator else float(n)
        if divisor > 0:
            values.append(numerator / divisor)
    lo, hi = _percentile(values, 0.025), _percentile(values, 0.975)
    return [lo, hi] if lo is not None and hi is not None else None


def wilson_interval(successes: int, trials: int, z: float = 1.959963984540054) -> list[float] | None:
    if trials <= 0 or not 0 <= successes <= trials:
        return None
    p = successes / trials
    denominator = 1.0 + z * z / trials
    center = (p + z * z / (2.0 * trials)) / denominator
    margin = z * math.sqrt((p * (1.0 - p) + z * z / (4.0 * trials)) / trials) / denominator
    return [max(0.0, center - margin), min(1.0, center + margin)]


def _eligible_hours(sample_count: int) -> float:
    return sample_count * CADENCE_S / 3600.0


def _candidate_selection(metrics: dict[str, dict[str, Any]], model: dict[str, Any]) -> tuple[str, bool, str]:
    joint_keys = [f"joint-{value:.2f}" for value in LAMBDA_GRID if model["candidates"][f"joint-{value:.2f}"]["status"] == "eligible"]
    if not joint_keys:
        raise ResearchDataError("no numerically eligible joint candidate")

    def components(key: str) -> tuple[float, float, float, bool]:
        item = metrics[key]
        recalls = [item["classes"][name]["recall"] for name in STRONG_CLASSES]
        min_recall = min(recalls)
        feasible = (
            item["pointFalsePositiveRate"] <= TARGETS[0]
            and item["falseEpisodesPer24h"] <= TARGETS[1]
            and all(item["classes"][name]["recall"] >= TARGETS[2] for name in STRONG_CLASSES)
            and all(item["classes"][name]["within10MinuteFraction"] >= TARGETS[3] for name in STRONG_CLASSES)
        )
        shrinkage = float(model["candidates"][key]["shrinkage"])
        return min_recall, item["falseEpisodesPer24h"], shrinkage, feasible

    feasible_keys = [key for key in joint_keys if components(key)[3]]
    if feasible_keys:
        key = sorted(feasible_keys, key=lambda k: (-components(k)[0], components(k)[1], components(k)[2]))[0]
        return key, True, "feasible calibration targets; highest minimum strong-class recall, then fewer episodes, then lower shrinkage"
    key = sorted(joint_keys, key=lambda k: (-components(k)[0], components(k)[1], components(k)[2]))[0]
    return key, False, "no candidate met every calibration target; highest minimum strong-class recall, then fewer episodes, then lower shrinkage"


def _summarize_calibration(model: dict[str, Any]) -> dict[str, Any]:
    threshold_scores: dict[str, list[float]] = {key: [] for key, candidate in model["candidates"].items() if candidate["status"] == "eligible"}
    for group_id in CAL_GROUPS:
        traces = score_trace(generate_trace(group_id), model)
        for key, decisions in traces.items():
            threshold_scores[key].extend(float(item.score) for item in decisions if item.eligible and item.score is not None)
    thresholds = {key: nearest_rank_quantile(scores, 0.99) for key, scores in threshold_scores.items()}
    accum: dict[str, dict[str, Any]] = {
        key: {"baselineBlocks": [], "classes": {name: [] for name in STRONG_CLASSES + EXPLORATORY_CLASSES}}
        for key in thresholds
    }
    for group_id in CAL_GROUPS:
        baseline_scores = score_trace(generate_trace(group_id), model)
        for key, decisions in baseline_scores.items():
            trace = _trace_summary(decisions, thresholds[key])
            accum[key]["baselineBlocks"].append({
                "pointExceedances": float(trace["pointExceedances"]),
                "eligiblePoints": float(trace["eligiblePoints"]),
                "episodes": float(trace["episodeCount"]),
                "hours": _eligible_hours(trace["eligiblePoints"]),
            })
        for scenario in STRONG_CLASSES + EXPLORATORY_CLASSES:
            injected = generate_trace(group_id, scenario, model["robustScales"])
            scores = score_trace(injected, model)
            for key, decisions in scores.items():
                trace = _trace_summary(decisions, thresholds[key], EVENT_START)
                accum[key]["classes"][scenario].append({
                    "detected": float(bool(trace["detected"])),
                    "within10": float(bool(trace["within10Minutes"])),
                    "latencySeconds": float(trace["latencySeconds"]) if trace["latencySeconds"] is not None else 0.0,
                    "latencyObserved": float(trace["latencySeconds"] is not None),
                })
    metrics: dict[str, dict[str, Any]] = {}
    for key, data in accum.items():
        base = data["baselineBlocks"]
        point = sum(row["pointExceedances"] for row in base)
        eligible = sum(row["eligiblePoints"] for row in base)
        episodes = sum(row["episodes"] for row in base)
        hours = sum(row["hours"] for row in base)
        classes = {}
        for scenario, outcomes in data["classes"].items():
            classes[scenario] = {
                "sourceBlocks": len(outcomes),
                "recall": sum(row["detected"] for row in outcomes) / len(outcomes),
                "within10MinuteFraction": sum(row["within10"] for row in outcomes) / len(outcomes),
                "meanDetectedLatencySeconds": (
                    sum(row["latencySeconds"] for row in outcomes) / sum(row["latencyObserved"] for row in outcomes)
                    if sum(row["latencyObserved"] for row in outcomes) else None
                ),
            }
        metrics[key] = {
            "threshold": thresholds[key],
            "pointFalsePositiveNumerator": int(point),
            "pointFalsePositiveDenominator": int(eligible),
            "pointFalsePositiveRate": point / eligible if eligible else 0.0,
            "falseEpisodes": int(episodes),
            "eligibleSimulatedHours": hours,
            "falseEpisodesPer24h": episodes / hours * 24.0 if hours else math.inf,
            "classes": classes,
        }
    selected, selected_feasible, rationale = _candidate_selection(metrics, model)
    return {
        "thresholds": thresholds,
        "candidateMetrics": metrics,
        "selectedJointCandidate": selected,
        "selectedCandidateMetCalibrationTargets": selected_feasible,
        "selectionRationale": rationale,
        "calibrationSourceGroups": len(CAL_GROUPS),
    }


def _manifest() -> dict[str, Any]:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    if _sha256_text(PROTOCOL_PATH) != manifest.get("protocolSha256"):
        raise RuntimeError("protocol digest differs from the pre-fit manifest")
    if manifest.get("experimentIdentity") != EXPERIMENT_ID or manifest.get("finalHoldoutIdentity") != HOLDOUT_ID:
        raise RuntimeError("experiment identities differ from the pre-fit manifest")
    if manifest.get("protocol") != PROTOCOL_PATH.name:
        raise RuntimeError("manifest protocol name differs from the executable contract")
    return manifest


def _configuration_payload(model: dict[str, Any], calibration: dict[str, Any]) -> dict[str, Any]:
    manifest = _manifest()
    candidates = {}
    for key, candidate in model["candidates"].items():
        candidates[key] = {name: value for name, value in candidate.items() if name != "factor"}
        if candidate.get("factor") is not None:
            candidates[key]["factor"] = candidate["factor"]
        if key in calibration["thresholds"]:
            candidates[key]["threshold"] = calibration["thresholds"][key]
    payload = {
        "schema": "elitescada.w15.intelligence.synthetic-bench-frozen-config.v1",
        "experimentIdentity": EXPERIMENT_ID,
        "finalHoldoutIdentity": HOLDOUT_ID,
        "baseCommit": manifest["baseCommit"],
        "assignedBranch": manifest["assignedBranch"],
        "protocolSha256": manifest["protocolSha256"],
        "manifestCanonicalSha256": _sha256_json(manifest),
        "numericSourceSha256": _sha256_text(OLD_NUMERIC_SOURCE),
        "benchSourceSha256": _sha256_text(Path(__file__)),
        "testSourceSha256": _sha256_text(TEST_SOURCE),
        "partitionSha256": partition_digest(),
        "reusedPureNumericalSymbols": list(REUSED_NUMERICAL_SYMBOLS),
        "fit": {
            "contextCoefficients": model["contextCoefficients"],
            "robustCenters": model["robustCenters"],
            "robustScales": model["robustScales"],
            "covariance": model["covariance"],
            "covarianceMean": model["covarianceMean"],
            "contextDesignRank": model["contextDesignRank"],
            "contextDesignCondition": model["contextDesignCondition"],
            "unregularizedRank": model["unregularizedRank"],
            "unregularizedCondition": model["unregularizedCondition"],
        },
        "candidates": candidates,
        "selectedJointCandidate": calibration["selectedJointCandidate"],
        "selectedCandidateMetCalibrationTargets": calibration["selectedCandidateMetCalibrationTargets"],
        "selectionRationale": calibration["selectionRationale"],
        "calibrationCandidateMetrics": calibration["candidateMetrics"],
        "fitGroupIds": [FIT_GROUPS[0], FIT_GROUPS[-1]],
        "calibrationGroupIds": [CAL_GROUPS[0], CAL_GROUPS[-1]],
        "holdoutGroupIds": [HOLDOUT_GROUPS[0], HOLDOUT_GROUPS[-1]],
    }
    payload["configurationSha256"] = _sha256_json(payload)
    return payload


def _verify_configuration(config: dict[str, Any]) -> None:
    copy = dict(config)
    claimed = copy.pop("configurationSha256", None)
    actual = _sha256_json(copy)
    if claimed != actual:
        raise RuntimeError("frozen configuration digest mismatch")
    manifest = _manifest()
    if config.get("protocolSha256") != manifest["protocolSha256"]:
        raise RuntimeError("frozen configuration protocol hash mismatch")
    if config.get("manifestCanonicalSha256") != _sha256_json(manifest):
        raise RuntimeError("frozen configuration manifest hash mismatch")
    if config.get("benchSourceSha256") != _sha256_text(Path(__file__)):
        raise RuntimeError("bench source changed after freeze")
    if config.get("testSourceSha256") != _sha256_text(TEST_SOURCE):
        raise RuntimeError("mechanism-test source changed after freeze")
    if config.get("numericSourceSha256") != _sha256_text(OLD_NUMERIC_SOURCE):
        raise RuntimeError("reused numeric source changed after freeze")
    if config.get("partitionSha256") != partition_digest():
        raise RuntimeError("partition assignment changed after freeze")


def _peak_rss_bytes() -> int:
    if os.name == "nt":
        class PROCESS_MEMORY_COUNTERS(ctypes.Structure):
            _fields_ = [
                ("cb", ctypes.c_ulong), ("PageFaultCount", ctypes.c_ulong),
                ("PeakWorkingSetSize", ctypes.c_size_t), ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t), ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t), ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t), ("PeakPagefileUsage", ctypes.c_size_t),
            ]
        counters = PROCESS_MEMORY_COUNTERS()
        counters.cb = ctypes.sizeof(counters)
        handle = ctypes.windll.kernel32.GetCurrentProcess()
        if not ctypes.windll.psapi.GetProcessMemoryInfo(handle, ctypes.byref(counters), counters.cb):
            raise OSError("GetProcessMemoryInfo failed")
        return int(counters.PeakWorkingSetSize)
    try:
        import resource
        peak = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
        return int(peak * 1024)
    except (ImportError, AttributeError):
        raise RuntimeError("peak RSS measurement unavailable")


def run_fit_calibration(scratch: Path) -> dict[str, Any]:
    scratch.mkdir(parents=True, exist_ok=True)
    config_path = scratch / "frozen-config-candidate.json"
    summary_path = scratch / "fit-calibration-summary.json"
    if config_path.exists() or summary_path.exists():
        raise RuntimeError("fit/calibration scratch outputs already exist; refusing overwrite")
    started_wall, started_cpu = time.perf_counter(), time.process_time()
    model = fit_reference_model()
    calibration = _summarize_calibration(model)
    config = _configuration_payload(model, calibration)
    resource = {
        "os": platform.platform(), "machine": platform.machine(), "python": sys.version,
        "cpuSeconds": time.process_time() - started_cpu,
        "wallSeconds": time.perf_counter() - started_wall,
        "peakRssBytes": _peak_rss_bytes(),
    }
    summary = {
        "schema": "elitescada.w15.intelligence.synthetic-bench-fit-calibration.v1",
        "experimentIdentity": EXPERIMENT_ID,
        "partitionSha256": partition_digest(),
        "fitGroups": len(FIT_GROUPS), "calibrationGroups": len(CAL_GROUPS),
        "fitRows": len(FIT_GROUPS) * SAMPLES_PER_GROUP,
        "calibrationRows": len(CAL_GROUPS) * SAMPLES_PER_GROUP * len(SCENARIOS),
        "contextDesignRank": model["contextDesignRank"],
        "contextDesignCondition": model["contextDesignCondition"],
        "unregularizedRank": model["unregularizedRank"],
        "unregularizedCondition": model["unregularizedCondition"],
        "candidateMetrics": calibration["candidateMetrics"],
        "selectedJointCandidate": calibration["selectedJointCandidate"],
        "selectedCandidateMetCalibrationTargets": calibration["selectedCandidateMetCalibrationTargets"],
        "selectionRationale": calibration["selectionRationale"],
        "configurationSha256": config["configurationSha256"],
        "reusedPureNumericalSymbols": list(REUSED_NUMERICAL_SYMBOLS),
        "resource": resource,
    }
    summary_path.write_bytes(_canonical_json(summary) + b"\n")
    config_path.write_bytes(_canonical_json(config) + b"\n")
    return summary


def compare_fit_calibration(windows_config_path: Path, linux_config_path: Path) -> dict[str, Any]:
    left = json.loads(windows_config_path.read_text(encoding="utf-8"))
    right = json.loads(linux_config_path.read_text(encoding="utf-8"))
    _verify_configuration(left)
    _verify_configuration(right)

    def compare_values(a: Any, b: Any, path: str = "") -> list[str]:
        if isinstance(a, bool) or isinstance(b, bool):
            return [] if a is b else [path]
        if isinstance(a, int) and isinstance(b, int):
            return [] if a == b else [path]
        if isinstance(a, (int, float)) and isinstance(b, (int, float)):
            if any(not math.isfinite(float(value)) for value in (a, b)):
                return [path]
            relative_tokens = ("threshold", "metric", "condition", "rate", "recall", "latency", "fraction", "hours", "episodes", "quantile")
            if any(token in path.lower() for token in relative_tokens):
                tolerance = 1e-8 * max(abs(float(a)), abs(float(b)), 1e-12)
            else:
                tolerance = 1e-10
            return [] if abs(float(a) - float(b)) <= tolerance else [path]
        if type(a) is not type(b):
            return [path]
        if isinstance(a, dict):
            if set(a) != set(b):
                return [path + ".keys"]
            return [problem for key in sorted(a) for problem in compare_values(a[key], b[key], f"{path}.{key}")]
        if isinstance(a, list):
            if len(a) != len(b):
                return [path + ".length"]
            return [problem for index, (x, y) in enumerate(zip(a, b)) for problem in compare_values(x, y, f"{path}[{index}]")]
        return [] if a == b else [path]

    left_compare, right_compare = dict(left), dict(right)
    left_compare.pop("configurationSha256", None)
    right_compare.pop("configurationSha256", None)
    differences = compare_values(left_compare, right_compare)
    if differences:
        raise RuntimeError("cross-platform tolerance exceeded at: " + ", ".join(differences[:20]))
    return {
        "status": "MATCH_WITHIN_PREDECLARED_TOLERANCES",
        "windowsConfigurationSha256": left["configurationSha256"],
        "linuxConfigurationSha256": right["configurationSha256"],
        "candidate": left["selectedJointCandidate"],
        "tolerances": {"parametersAbsolute": 1e-10, "thresholdsAndMetricsRelative": 1e-8, "identitiesCountsLabels": "exact"},
    }


def publish_fit_calibration(config_path: Path, linux_config_path: Path, summary_paths: Sequence[Path]) -> None:
    config = json.loads(config_path.read_text(encoding="utf-8"))
    linux_config = json.loads(linux_config_path.read_text(encoding="utf-8"))
    _verify_configuration(config)
    _verify_configuration(linux_config)
    summaries = [json.loads(path.read_text(encoding="utf-8")) for path in summary_paths]
    if len(summaries) != 2:
        raise RuntimeError("publish requires both Windows and Linux fit/calibration summaries")
    if summaries[0].get("configurationSha256") != config["configurationSha256"]:
        raise RuntimeError("Windows summary and frozen configuration digests differ")
    if summaries[1].get("configurationSha256") != linux_config["configurationSha256"]:
        raise RuntimeError("Linux summary and frozen configuration digests differ")
    output_config = HERE / "frozen-config-v1.json"
    output_summary = HERE / "fit-calibration-summary-v1.json"
    if output_config.exists() or output_summary.exists():
        raise RuntimeError("published fit/calibration artifact already exists; refusing overwrite")
    published = dict(config)
    aggregate = {
        "schema": "elitescada.w15.intelligence.synthetic-bench-fit-calibration-report.v1",
        "configurationSha256": config["configurationSha256"],
        "platformConfigurationSha256": [config["configurationSha256"], linux_config["configurationSha256"]],
        "platformRuns": [summary["resource"] for summary in summaries],
        "candidateMetrics": config["calibrationCandidateMetrics"],
        "selectedJointCandidate": config["selectedJointCandidate"],
        "selectedCandidateMetCalibrationTargets": config["selectedCandidateMetCalibrationTargets"],
        "selectionRationale": config["selectionRationale"],
        "reusedPureNumericalSymbols": config["reusedPureNumericalSymbols"],
    }
    output_config.write_bytes(_canonical_json(published) + b"\n")
    output_summary.write_bytes(_canonical_json(aggregate) + b"\n")


def _git(*args: str) -> str:
    result = subprocess.run(["git", *args], cwd=REPO, check=True, capture_output=True, text=True)
    return result.stdout.strip()


def _claim_receipt(path: Path, initial: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = _canonical_json(initial) + b"\n"
    try:
        fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError as exc:
        raise RuntimeError("one-use holdout identity is already consumed or reserved; hard stop") from exc
    with os.fdopen(fd, "wb") as target:
        target.write(encoded)
        target.flush()
        os.fsync(target.fileno())


def _write_atomic(path: Path, value: Any) -> None:
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(_canonical_json(value) + b"\n")
    os.replace(temporary, path)


def _percentile_summary(values: Sequence[float]) -> dict[str, float | None]:
    return {"p50": _percentile(values, 0.50), "p95": _percentile(values, 0.95), "max": max(values) if values else None}


def _holdout_metric(blocks: Sequence[dict[str, float]], metric: str, denominator: str | None = None) -> dict[str, Any]:
    numerator = sum(item.get(metric, 0.0) for item in blocks)
    divisor = sum(item.get(denominator, 0.0) for item in blocks) if denominator else float(len(blocks))
    estimate = numerator / divisor if divisor else None
    return {"numerator": int(numerator) if numerator.is_integer() else numerator,
            "denominator": int(divisor) if divisor.is_integer() else divisor,
            "estimate": estimate,
            "clusterBootstrap95": _bootstrap_ci(blocks, metric, denominator)}


def _holdout_reports(per_candidate: dict[str, Any], group_count: int) -> dict[str, Any]:
    reports: dict[str, Any] = {}
    for key, item in per_candidate.items():
        baseline = item["baseline"]
        point_rate = _holdout_metric(baseline, "pointExceedances", "eligiblePoints")
        episodes = _holdout_metric(baseline, "episodes", "eligibleHours")
        rate_per_day = (episodes["estimate"] * 24.0) if episodes["estimate"] is not None else None
        episodes["estimatePer24EligibleHours"] = rate_per_day
        if episodes["clusterBootstrap95"] is not None:
            episodes["clusterBootstrap95Per24EligibleHours"] = [value * 24.0 for value in episodes["clusterBootstrap95"]]
        block_occurrences = sum(1 for block in baseline if block["episodes"] > 0)
        wilson = wilson_interval(block_occurrences, len(baseline))
        episodes["groupsWithEpisode"] = block_occurrences
        episodes["groupOccurrenceWilson95"] = wilson
        episodes["groupOccurrenceRateUpperPer24Hours"] = wilson[1] * 72.0 if wilson else None
        classes = {}
        for scenario, outcomes in item["classes"].items():
            recall = _holdout_metric(outcomes, "detected")
            within = _holdout_metric(outcomes, "within10")
            latencies = [row["latencySeconds"] for row in outcomes if row["latencyObserved"]]
            latency_blocks = [{"latency": row["latencySeconds"], "observed": row["latencyObserved"]} for row in outcomes]
            latency_mean = _bootstrap_ci(latency_blocks, "latency", "observed")
            classes[scenario] = {
                "sourceBlocks": len(outcomes),
                "recall": recall,
                "within10MinuteFraction": within,
                "detectedLatencySeconds": _percentile_summary(latencies),
                "meanDetectedLatencyBootstrap95Seconds": latency_mean,
                "misses": len(outcomes) - int(recall["numerator"]),
                "late": int(recall["numerator"]) - int(within["numerator"]),
                "syntheticStrongClassTarget": (
                    "PASS" if scenario in STRONG_CLASSES and len(outcomes) >= 100
                    and recall["estimate"] is not None and recall["estimate"] >= TARGETS[2]
                    and within["estimate"] is not None and within["estimate"] >= TARGETS[3]
                    else "FAIL" if scenario in STRONG_CLASSES and len(outcomes) >= 100 else "INSUFFICIENT"
                ),
            }
        eligible_hours = sum(row["eligibleHours"] for row in baseline)
        point_fp_value = point_rate["estimate"]
        episode_rate_value = rate_per_day
        sufficient_groups = group_count >= 100
        point_target_pass = point_fp_value is not None and point_fp_value <= TARGETS[0]
        episode_target_status = "INSUFFICIENT" if eligible_hours < 168.0 else (
            "PASS" if episode_rate_value is not None and episode_rate_value <= TARGETS[1] else "FAIL"
        )
        strong_statuses = [classes[name]["syntheticStrongClassTarget"] for name in STRONG_CLASSES]
        overall = "PASS_SYNTHETIC_CONDITIONAL" if (
            sufficient_groups and point_target_pass and episode_target_status == "PASS"
            and all(status == "PASS" for status in strong_statuses)
        ) else "INSUFFICIENT" if not sufficient_groups or episode_target_status == "INSUFFICIENT" else "FAIL"
        reports[key] = {
            "normalEligibleSimulatedHours": eligible_hours,
            "pointFalsePositiveRate": point_rate,
            "pointFalsePositiveTarget": "PASS" if point_target_pass else "FAIL",
            "falseEpisodes": episodes,
            "falseEpisodeTarget": episode_target_status,
            "classes": classes,
            "syntheticConditionalOverall": overall,
            "real168Hour14DayExposure": "UNMEASURED",
        }
    return reports


def run_holdout(config_path: Path, scratch: Path) -> dict[str, Any]:
    config_path = config_path.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    _verify_configuration(config)
    if config_path != (HERE / "frozen-config-v1.json").resolve():
        raise RuntimeError("holdout must use the committed frozen-config-v1.json")
    if _git("branch", "--show-current") != config["assignedBranch"]:
        raise RuntimeError("holdout branch does not match the frozen assignment")
    if _git("status", "--porcelain"):
        raise RuntimeError("holdout requires a clean checkout after the frozen-configuration commit")
    head = _git("rev-parse", "HEAD")
    committed_config = json.loads(_git("show", f"HEAD:{config_path.relative_to(REPO).as_posix()}"))
    _verify_configuration(committed_config)
    if committed_config != config:
        raise RuntimeError("committed configuration differs from the working frozen configuration")
    if os.name != "nt" or sys.version_info[:3] != (3, 14, 6):
        raise RuntimeError("the one-use final holdout is designated for Windows CPython 3.14.6 only")
    report_path = HERE / "holdout-aggregate-v1.json"
    receipt_mirror = HERE / "holdout-receipt-v1.json"
    if report_path.exists() or receipt_mirror.exists():
        raise RuntimeError("holdout aggregate or receipt already published; one-use rule forbids another run")
    if scratch.exists() and any(scratch.iterdir()):
        raise RuntimeError("holdout scratch already contains data; refusing reuse")
    scratch.mkdir(parents=True, exist_ok=True)
    receipt_path = scratch / "holdout-one-use-receipt.json"
    manifest = _manifest()
    receipt = {
        "schema": "elitescada.w15.intelligence.synthetic-bench-holdout-receipt.v1",
        "state": "reserved",
        "holdoutIdentity": HOLDOUT_ID,
        "experimentIdentity": EXPERIMENT_ID,
        "platform": platform.platform(),
        "python": sys.version,
        "frozenHead": head,
        "protocolSha256": manifest["protocolSha256"],
        "manifestCanonicalSha256": _sha256_json(manifest),
        "benchSourceSha256": _sha256_text(Path(__file__)),
        "testSourceSha256": _sha256_text(TEST_SOURCE),
        "numericSourceSha256": _sha256_text(OLD_NUMERIC_SOURCE),
        "configurationSha256": config["configurationSha256"],
        "partitionSha256": partition_digest(),
        "groupRange": [HOLDOUT_GROUPS[0], HOLDOUT_GROUPS[-1]],
        "startedAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    _claim_receipt(receipt_path, receipt)
    start_wall, start_cpu = time.perf_counter(), time.process_time()
    fit = config["fit"]
    model = {
        "contextCoefficients": fit["contextCoefficients"], "robustCenters": fit["robustCenters"],
        "robustScales": fit["robustScales"], "covariance": fit["covariance"],
        "covarianceMean": fit["covarianceMean"],
        "candidates": {key: {name: value for name, value in candidate.items()} for key, candidate in config["candidates"].items()},
    }
    thresholds = {key: candidate["threshold"] for key, candidate in model["candidates"].items() if "threshold" in candidate}
    per_candidate: dict[str, Any] = {
        key: {"baseline": [], "classes": {scenario: [] for scenario in STRONG_CLASSES + EXPLORATORY_CLASSES}}
        for key in thresholds
    }
    for group_id in HOLDOUT_GROUPS:
        traces = {"nonfault-context": generate_trace(group_id)}
        for scenario in STRONG_CLASSES + EXPLORATORY_CLASSES:
            traces[scenario] = generate_trace(group_id, scenario, fit["robustScales"])
        for scenario, trace_rows in traces.items():
            scores = score_trace(trace_rows, model)
            for key, decisions in scores.items():
                if key not in thresholds:
                    continue
                result = _trace_summary(decisions, thresholds[key], EVENT_START if scenario != "nonfault-context" else None)
                if scenario == "nonfault-context":
                    per_candidate[key]["baseline"].append({
                        "pointExceedances": float(result["pointExceedances"]),
                        "eligiblePoints": float(result["eligiblePoints"]),
                        "episodes": float(result["episodeCount"]),
                        "eligibleHours": _eligible_hours(result["eligiblePoints"]),
                    })
                else:
                    per_candidate[key]["classes"][scenario].append({
                        "detected": float(bool(result["detected"])),
                        "within10": float(bool(result["within10Minutes"])),
                        "latencySeconds": float(result["latencySeconds"]) if result["latencySeconds"] is not None else 0.0,
                        "latencyObserved": float(result["latencySeconds"] is not None),
                        "preOnsetEpisodes": float(result["preOnsetEpisodes"]),
                    })
    reports = _holdout_reports(per_candidate, len(HOLDOUT_GROUPS))
    resources = {
        "os": platform.platform(), "machine": platform.machine(), "python": sys.version,
        "cpuSeconds": time.process_time() - start_cpu,
        "wallSeconds": time.perf_counter() - start_wall,
        "peakRssBytes": _peak_rss_bytes(),
    }
    report = {
        "schema": "elitescada.w15.intelligence.synthetic-bench-holdout-results.v1",
        "holdoutIdentity": HOLDOUT_ID,
        "experimentIdentity": EXPERIMENT_ID,
        "frozenHead": head,
        "configurationSha256": config["configurationSha256"],
        "protocolSha256": manifest["protocolSha256"],
        "partitionSha256": partition_digest(),
        "sourceBlocks": len(HOLDOUT_GROUPS),
        "maxRowsProcessed": 100800,
        "resource": resources,
        "candidates": reports,
        "limitations": [
            "All outcomes are conditional on this deterministic synthetic pump stimulus; not a digital twin or field evidence.",
            "Only 40 simulated eligible normal hours are in this holdout; the 168-hour/14-day real exposure target remains UNMEASURED.",
            "No product API, database, Preview, runtime, HA, driver, licensing, owner data, or public archive was accessed.",
        ],
    }
    report["reportSha256"] = _sha256_json(report)
    receipt["state"] = "completed"
    receipt["completedAtUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    receipt["aggregateReportSha256"] = report["reportSha256"]
    _write_atomic(receipt_path, receipt)
    _write_atomic(report_path, report)
    _write_atomic(receipt_mirror, receipt)
    return report


def run_tests() -> int:
    result = subprocess.run([sys.executable, "-B", "-m", "unittest", "discover", "-s", str(HERE), "-p", "test_*.py", "-v"], cwd=REPO)
    return result.returncode


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("test")
    fit = sub.add_parser("fit-calibrate")
    fit.add_argument("--scratch", type=Path, required=True)
    compare = sub.add_parser("compare")
    compare.add_argument("windows_config", type=Path)
    compare.add_argument("linux_config", type=Path)
    publish = sub.add_parser("publish-fitcal")
    publish.add_argument("windows_config", type=Path)
    publish.add_argument("linux_config", type=Path)
    publish.add_argument("windows_summary", type=Path)
    publish.add_argument("linux_summary", type=Path)
    holdout = sub.add_parser("holdout-once")
    holdout.add_argument("--scratch", type=Path, required=True)
    holdout.add_argument("--config", type=Path, default=HERE / "frozen-config-v1.json")
    args = parser.parse_args(argv)
    if args.command == "test":
        return run_tests()
    if args.command == "fit-calibrate":
        print(json.dumps(run_fit_calibration(args.scratch), indent=2, allow_nan=False))
        return 0
    if args.command == "compare":
        print(json.dumps(compare_fit_calibration(args.windows_config, args.linux_config), indent=2, allow_nan=False))
        return 0
    if args.command == "publish-fitcal":
        compare_fit_calibration(args.windows_config, args.linux_config)
        publish_fit_calibration(args.windows_config, args.linux_config, (args.windows_summary, args.linux_summary))
        print("fit/calibration aggregates and frozen configuration published")
        return 0
    if args.command == "holdout-once":
        report = run_holdout(args.config, args.scratch)
        print(json.dumps({"holdoutIdentity": report["holdoutIdentity"], "reportSha256": report["reportSha256"]}, indent=2))
        return 0
    parser.error("unknown command")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
