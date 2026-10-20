"""Isolated, standard-library-only implementation of the preregistered Stage 4F study."""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import math
import os
import platform
import subprocess
import sys
import time
from collections import Counter, deque
from dataclasses import asdict, dataclass, replace
from pathlib import Path
from typing import Any, Iterable, Sequence


ROOT = Path(__file__).resolve().parent
PROTOCOL_PATH = ROOT / "protocol-v1.md"
MANIFEST_PATH = ROOT / "experiment-manifest-v1.json"
SEED_NAMESPACE = "ELITESCADA-W15-4F-WINDOWED-v1"
EXPERIMENT_ID = "W15-INTEL-4F-WINDOWED-2026-10-20-01"
HOLDOUT_ID = "W15-INTEL-4F-WINDOWED-HOLDOUT-2026-10-20-01"
UNIT_NAMESPACE = "ELITESCADA-W15-4F-WINDOWED-UNIT-v1"
GROUPS = tuple(range(240))
FIT_GROUPS = tuple(range(0, 60))
CAL_GROUPS = tuple(range(60, 120))
HOLDOUT_GROUPS = tuple(range(120, 240))
SAMPLES = 120
CADENCE_S = 10
EVENT_START = 12
WINDOW = 30
TARGET_IDS = ("current", "flow", "pressure")
TARGET_SDS = (0.70, 1.00, 0.10)
AR_PHI = 0.65
AR_SCALE = math.sqrt(1.0 - AR_PHI * AR_PHI)
MAD_NORMAL_SCALE = 1.4826
MAX_CONDITION = 1e6
MAX_RAW_ROWS = 50_000
MAX_WINDOW_DAYS = 31
MAX_OUTPUTS = 500
PAGE_SIZE = 200
BOOTSTRAP_REPLICATES = 2_000
CPU_LIMIT_SECONDS = 15 * 60
WALL_LIMIT_SECONDS = 20 * 60
RSS_LIMIT_BYTES = 512 * 1024 * 1024
ARTIFACT_LIMIT_BYTES = 1024 * 1024
LINUX_IMAGE = "python:3.14.6-slim-bookworm@sha256:4c92ffcde4dd6f1ff72a24518f49fd4990b27134987dfa31a733badde66df9f8"
MASK64 = (1 << 64) - 1
R0 = ((1.0, 0.65, 0.25), (0.65, 1.0, 0.20), (0.25, 0.20, 1.0))
R1 = ((1.0, -0.65, 0.25), (-0.65, 1.0, 0.20), (0.25, 0.20, 1.0))
SCENARIOS = (
    "nonfault-context",
    "current-plus-3-fit-scales",
    "all-targets-plus-3-fit-scales",
    "relationship-current-flow-flip",
    "residual-dispersion-2x",
    "residual-dispersion-0.5x",
    "mixed-relationship-and-current-plus-3",
    "current-plus-1-fit-scale",
    "current-plus-2-fit-scales",
    "current-linear-ramp-to-3",
)
STRONG_SCENARIOS = (
    "current-plus-3-fit-scales",
    "all-targets-plus-3-fit-scales",
    "relationship-current-flow-flip",
    "residual-dispersion-2x",
    "residual-dispersion-0.5x",
    "mixed-relationship-and-current-plus-3",
)


class StudyError(RuntimeError):
    """Fail-closed study input, numerical, provenance, or lifecycle error."""


@dataclass(frozen=True)
class Cohort:
    group_id: int
    scenario: str
    index: int
    source_time_s: int
    targets: tuple[float, ...]
    speed_rpm: float
    load_fraction: float
    running: bool = True
    quality: str = "Good"
    synchronized: bool = True
    source_verified: bool = True
    cadence_s: int = CADENCE_S
    source_id: str = ""
    context_id: str = "pump-context-v1"
    profile_id: str = "pump-profile-v1"
    model_version: str = "windowed-model-v1"


@dataclass(frozen=True)
class Reference:
    coefficients: tuple[tuple[float, ...], ...]
    target_centers: tuple[float, ...]
    target_scales: tuple[float, ...]
    reference_correlations: tuple[tuple[float, ...], ...]
    component_centers: tuple[float, ...] = ()
    component_scales: tuple[float, ...] = ()
    design_rank: int = 0
    design_condition: float = math.inf


@dataclass(frozen=True)
class RunSummary:
    group_id: int
    scenario: str
    input_cohorts: int
    eligible_cohorts: int
    ineligible_reasons: dict[str, int]
    warmup_cohorts: int
    numerical_no_score: int
    reset_count: int
    scored_decisions: int
    exceedances: int
    episode_confirmations: tuple[int, ...]
    scores: tuple[float, ...]


class SplitMix64:
    """Portable, study-local SplitMix64; no old generator is imported or called."""

    def __init__(self, seed: int):
        self.state = seed & MASK64

    def next_u64(self) -> int:
        self.state = (self.state + 0x9E3779B97F4A7C15) & MASK64
        value = self.state
        value = ((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
        value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & MASK64
        return (value ^ (value >> 31)) & MASK64

    def uniform_open(self) -> float:
        return ((self.next_u64() >> 11) + 0.5) / (1 << 53)

    def normal_approx(self) -> float:
        return sum(self.uniform_open() for _ in range(12)) - 6.0


class EpisodeTracker:
    """Complete five-decision 3-of-5 entry and five-below termination state."""

    def __init__(self) -> None:
        self.decisions: deque[bool] = deque(maxlen=5)
        self.active = False
        self.consecutive_below = 0
        self.confirmations: list[int] = []

    def reset(self) -> None:
        self.decisions.clear()
        self.active = False
        self.consecutive_below = 0

    def step(self, above: bool, sample_index: int) -> tuple[bool, bool]:
        """Return (new_confirmation, terminated); termination retains the rolling history."""
        self.decisions.append(bool(above))
        if self.active:
            if above:
                self.consecutive_below = 0
                return False, False
            self.consecutive_below += 1
            if self.consecutive_below >= 5:
                self.active = False
                self.consecutive_below = 0
                return False, True
            return False, False
        if len(self.decisions) == 5 and sum(self.decisions) >= 3:
            self.active = True
            self.consecutive_below = 0
            self.confirmations.append(sample_index)
            return True, False
        return False, False


def derive_seed(group_id: int, namespace: str = SEED_NAMESPACE) -> int:
    if namespace == SEED_NAMESPACE and group_id not in GROUPS:
        raise StudyError("study group id is outside the frozen 0..239 range")
    if namespace != SEED_NAMESPACE and namespace != UNIT_NAMESPACE:
        raise StudyError("generation namespace is neither the study nor UNIT namespace")
    if group_id < 0:
        raise StudyError("negative group identity")
    material = f"{namespace}|group-{group_id:03d}".encode("utf-8")
    return int.from_bytes(hashlib.sha256(material).digest()[:8], "big")


def audit_group_partition() -> None:
    partitions = (FIT_GROUPS, CAL_GROUPS, HOLDOUT_GROUPS)
    flattened = tuple(group for part in partitions for group in part)
    if flattened != GROUPS or len(set(flattened)) != 240:
        raise StudyError("group partition differs from the frozen exact 60/60/120 assignment")
    seeds = [derive_seed(group) for group in GROUPS]
    if len(set(seeds)) != len(GROUPS):
        raise StudyError("duplicate study seed detected")


def median(values: Sequence[float]) -> float:
    if not values:
        raise StudyError("median requires at least one finite value")
    ordered = sorted(float(value) for value in values)
    if not all(math.isfinite(value) for value in ordered):
        raise StudyError("median input contains nonfinite values")
    middle = len(ordered) // 2
    if len(ordered) % 2:
        return ordered[middle]
    return (ordered[middle - 1] + ordered[middle]) / 2.0


def mad(values: Sequence[float]) -> tuple[float, float]:
    center = median(values)
    return center, median([abs(value - center) for value in values])


def context_for(group_id: int, index: int) -> tuple[float, float]:
    speed = 1430.0 + 5.0 * (group_id % 9)
    offsets = (-0.05, 0.05, 0.0, 0.07)
    load_base = 0.30 + 0.05 * (group_id % 9)
    load = min(0.80, max(0.25, load_base + offsets[index // 30]))
    return speed, load


def expected_targets(speed_rpm: float, load_fraction: float) -> tuple[float, float, float]:
    return (
        4.0 + 20.0 * load_fraction + 0.012 * (speed_rpm - 1450.0),
        3.0 + 30.0 * load_fraction + 0.020 * (speed_rpm - 1450.0),
        1.2 + 3.0 * load_fraction - 0.0015 * (speed_rpm - 1450.0),
    )


def cholesky(matrix: Sequence[Sequence[float]]) -> tuple[tuple[float, ...], ...]:
    size = len(matrix)
    if size == 0 or any(len(row) != size for row in matrix):
        raise StudyError("correlation matrix is not square")
    lower = [[0.0] * size for _ in range(size)]
    for i in range(size):
        for j in range(i + 1):
            value = float(matrix[i][j]) - sum(lower[i][k] * lower[j][k] for k in range(j))
            if i == j:
                if not math.isfinite(value) or value <= 0.0:
                    raise StudyError("correlation matrix is not positive definite")
                lower[i][j] = math.sqrt(value)
            else:
                lower[i][j] = value / lower[j][j]
    if not all(math.isfinite(value) for row in lower for value in row):
        raise StudyError("correlation factor is nonfinite")
    return tuple(tuple(row) for row in lower)


def independent_stream(group_id: int, namespace: str = SEED_NAMESPACE) -> tuple[tuple[float, ...], ...]:
    rng = SplitMix64(derive_seed(group_id, namespace))
    return tuple(tuple(rng.normal_approx() for _ in TARGET_IDS) for _ in range(SAMPLES))


def generate_run(
    group_id: int,
    scenario: str,
    fit_scales: Sequence[float] | None = None,
    *,
    independent: Sequence[Sequence[float]] | None = None,
    namespace: str = SEED_NAMESPACE,
) -> tuple[Cohort, ...]:
    if scenario not in SCENARIOS:
        raise StudyError(f"undeclared scenario: {scenario}")
    if scenario != "nonfault-context" and (fit_scales is None or len(fit_scales) != 3):
        raise StudyError("injected run needs the three frozen fit-only target scales")
    if namespace == SEED_NAMESPACE and group_id not in GROUPS:
        raise StudyError("study run group id is outside the frozen partition")
    if independent is None:
        independent = independent_stream(group_id, namespace)
    if len(independent) != SAMPLES or any(len(row) != 3 for row in independent):
        raise StudyError("independent innovation stream has the wrong shape")
    lower0 = cholesky(R0)
    lower1 = cholesky(R1)
    state = [0.0, 0.0, 0.0]
    scales = tuple(float(value) for value in (fit_scales or ()))
    rows: list[Cohort] = []
    for index in range(SAMPLES):
        selected = lower1 if scenario in ("relationship-current-flow-flip", "mixed-relationship-and-current-plus-3") and index >= EVENT_START else lower0
        correlated = [
            sum(selected[row][column] * float(independent[index][column]) for column in range(row + 1))
            for row in range(3)
        ]
        state = [AR_PHI * state[j] + AR_SCALE * correlated[j] for j in range(3)]
        speed, load = context_for(group_id, index)
        expected = expected_targets(speed, load)
        multiplier = 1.0
        if index >= EVENT_START:
            if scenario == "residual-dispersion-2x":
                multiplier = 2.0
            elif scenario == "residual-dispersion-0.5x":
                multiplier = 0.5
        targets = [expected[j] + state[j] * TARGET_SDS[j] * multiplier for j in range(3)]
        if index >= EVENT_START:
            if scenario == "current-plus-3-fit-scales":
                targets[0] += 3.0 * scales[0]
            elif scenario == "all-targets-plus-3-fit-scales":
                for target in range(3):
                    targets[target] += 3.0 * scales[target]
            elif scenario == "mixed-relationship-and-current-plus-3":
                targets[0] += 3.0 * scales[0]
            elif scenario == "current-plus-1-fit-scale":
                targets[0] += scales[0]
            elif scenario == "current-plus-2-fit-scales":
                targets[0] += 2.0 * scales[0]
            elif scenario == "current-linear-ramp-to-3":
                targets[0] += 3.0 * min(1.0, (index - EVENT_START) / 60.0) * scales[0]
        rows.append(Cohort(
            group_id=group_id,
            scenario=scenario,
            index=index,
            source_time_s=index * CADENCE_S,
            targets=tuple(targets),
            speed_rpm=speed,
            load_fraction=load,
            source_id=f"synthetic-group-{group_id:03d}",
        ))
    return tuple(rows)


def design_row(cohort: Cohort) -> tuple[float, float, float]:
    return (1.0, cohort.load_fraction, (cohort.speed_rpm - 1450.0) / 20.0)


def solve_linear(matrix: Sequence[Sequence[float]], vector: Sequence[float]) -> tuple[float, ...]:
    size = len(vector)
    if size == 0 or len(matrix) != size or any(len(row) != size for row in matrix):
        raise StudyError("linear system dimensions do not agree")
    augmented = [[float(matrix[i][j]) for j in range(size)] + [float(vector[i])] for i in range(size)]
    matrix_scale = max(abs(value) for row in matrix for value in row)
    if not math.isfinite(matrix_scale) or matrix_scale == 0.0:
        raise StudyError("linear system is singular or nonfinite")
    for column in range(size):
        pivot = max(range(column, size), key=lambda row: abs(augmented[row][column]))
        if abs(augmented[pivot][column]) <= matrix_scale * 1e-14:
            raise StudyError("linear system is rank deficient")
        augmented[column], augmented[pivot] = augmented[pivot], augmented[column]
        divisor = augmented[column][column]
        augmented[column] = [value / divisor for value in augmented[column]]
        for row in range(size):
            if row == column:
                continue
            factor = augmented[row][column]
            augmented[row] = [augmented[row][i] - factor * augmented[column][i] for i in range(size + 1)]
    solution = tuple(augmented[i][-1] for i in range(size))
    if not all(math.isfinite(value) for value in solution):
        raise StudyError("linear system solution is nonfinite")
    return solution


def symmetric_eigenvalues(matrix: Sequence[Sequence[float]]) -> tuple[float, ...]:
    size = len(matrix)
    if size == 0 or any(len(row) != size for row in matrix):
        raise StudyError("eigenvalue matrix is not square")
    work = [[float(value) for value in row] for row in matrix]
    for _ in range(max(1, 100 * size * size)):
        p, q = 0, 0
        max_off = 0.0
        for i in range(size):
            for j in range(i + 1, size):
                if abs(work[i][j]) > max_off:
                    p, q = i, j
                    max_off = abs(work[i][j])
        scale = max(1.0, max(abs(work[i][i]) for i in range(size)))
        if max_off <= scale * 1e-15:
            break
        angle = 0.5 * math.atan2(2.0 * work[p][q], work[q][q] - work[p][p])
        cosine = math.cos(angle)
        sine = math.sin(angle)
        app, aqq, apq = work[p][p], work[q][q], work[p][q]
        work[p][p] = cosine * cosine * app - 2.0 * sine * cosine * apq + sine * sine * aqq
        work[q][q] = sine * sine * app + 2.0 * sine * cosine * apq + cosine * cosine * aqq
        work[p][q] = work[q][p] = 0.0
        for k in range(size):
            if k in (p, q):
                continue
            akp, akq = work[k][p], work[k][q]
            work[k][p] = work[p][k] = cosine * akp - sine * akq
            work[k][q] = work[q][k] = sine * akp + cosine * akq
    values = tuple(sorted((work[i][i] for i in range(size)), reverse=True))
    if not all(math.isfinite(value) for value in values):
        raise StudyError("eigenvalues are nonfinite")
    return values


def design_diagnostics(rows: Sequence[Cohort]) -> tuple[int, float]:
    gram = [[0.0] * 3 for _ in range(3)]
    for cohort in rows:
        x = design_row(cohort)
        for i in range(3):
            for j in range(3):
                gram[i][j] += x[i] * x[j]
    values = symmetric_eigenvalues(gram)
    if values[0] <= 0.0 or values[-1] <= values[0] * 1e-12:
        return 2, math.inf
    condition = math.sqrt(values[0] / values[-1])
    return (3 if condition <= MAX_CONDITION else 2), condition


def _pearson_columns(rows: Sequence[Sequence[float]]) -> tuple[tuple[float, ...], ...]:
    if not rows:
        raise StudyError("correlation requires non-empty residual rows")
    width = len(rows[0])
    if width < 2 or any(len(row) != width for row in rows):
        raise StudyError("correlation rows have inconsistent dimensions")
    if any(not math.isfinite(float(value)) for row in rows for value in row):
        raise StudyError("correlation input contains nonfinite values")
    means = [sum(float(row[j]) for row in rows) / len(rows) for j in range(width)]
    centered = [[float(row[j]) - means[j] for j in range(width)] for row in rows]
    sumsquares = [sum(row[j] * row[j] for row in centered) for j in range(width)]
    if any(value <= 0.0 or not math.isfinite(value) for value in sumsquares):
        raise StudyError("correlation requires positive finite column variance")
    matrix = [[1.0 if i == j else 0.0 for j in range(width)] for i in range(width)]
    for i in range(width):
        for j in range(i):
            cov = sum(row[i] * row[j] for row in centered)
            corr = cov / math.sqrt(sumsquares[i] * sumsquares[j])
            if not math.isfinite(corr):
                raise StudyError("correlation is nonfinite")
            matrix[i][j] = matrix[j][i] = corr
    return tuple(tuple(row) for row in matrix)


def residual_for(cohort: Cohort, coefficients: Sequence[Sequence[float]]) -> tuple[float, ...]:
    x = design_row(cohort)
    result = tuple(float(cohort.targets[j]) - sum(x[k] * coefficients[j][k] for k in range(3)) for j in range(len(coefficients)))
    if not all(math.isfinite(value) for value in result):
        raise StudyError("residual is nonfinite")
    return result


def fit_contextual_reference(rows: Sequence[Cohort]) -> Reference:
    if not rows:
        raise StudyError("fit partition is empty")
    if any(row.scenario != "nonfault-context" or row.group_id not in FIT_GROUPS for row in rows):
        raise StudyError("fit accepts only baseline rows from groups 0..59")
    by_group: dict[int, list[Cohort]] = {}
    for row in rows:
        by_group.setdefault(row.group_id, []).append(row)
    if tuple(sorted(by_group)) != FIT_GROUPS:
        raise StudyError("fit group identities do not equal the frozen 0..59 set")
    for group_id, group_rows in by_group.items():
        indices = [row.index for row in group_rows]
        if sorted(indices) != list(range(SAMPLES)) or len(indices) != len(set(indices)):
            raise StudyError(f"fit group {group_id} does not contain exactly one cohort per index")
        if any(row.source_time_s != row.index * CADENCE_S for row in group_rows):
            raise StudyError(f"fit group {group_id} has a cadence mismatch")
    rank, condition = design_diagnostics(rows)
    if rank != 3 or not math.isfinite(condition) or condition > MAX_CONDITION:
        raise StudyError("contextual design is rank deficient or ill-conditioned")
    normal = [[0.0] * 3 for _ in range(3)]
    right = [[0.0] * 3 for _ in TARGET_IDS]
    for row in rows:
        x = design_row(row)
        for i in range(3):
            for j in range(3):
                normal[i][j] += x[i] * x[j]
            for target in range(3):
                value = float(row.targets[target])
                if not math.isfinite(value):
                    raise StudyError("fit target contains a nonfinite value")
                right[target][i] += x[i] * value
    coefficients = tuple(solve_linear(normal, right[target]) for target in range(3))
    residual_rows = [residual_for(row, coefficients) for row in rows]
    centers: list[float] = []
    scales: list[float] = []
    for target in range(3):
        target_residuals = [row[target] for row in residual_rows]
        center, deviation = mad(target_residuals)
        scale = MAD_NORMAL_SCALE * deviation
        if not math.isfinite(scale) or scale <= 0.0:
            raise StudyError(f"fit residual scale is degenerate for {TARGET_IDS[target]}")
        centers.append(center)
        scales.append(scale)
    correlations = _pearson_columns(residual_rows)
    return Reference(coefficients, tuple(centers), tuple(scales), correlations, (), (), rank, condition)


def window_components(residual_window: Sequence[Sequence[float]], reference: Reference) -> tuple[float, float, float] | None:
    width = len(reference.target_scales)
    if len(residual_window) != WINDOW or width < 2 or any(len(row) != width for row in residual_window):
        return None
    if any(not math.isfinite(float(value)) for row in residual_window for value in row):
        return None
    column_values = [[float(row[j]) for row in residual_window] for j in range(width)]
    if len(reference.target_centers) != width or len(reference.reference_correlations) != width or any(len(row) != width for row in reference.reference_correlations):
        return None
    if any(not math.isfinite(reference.target_scales[j]) or reference.target_scales[j] <= 0.0 for j in range(width)):
        return None
    centers: list[float] = []
    deviations: list[float] = []
    for values in column_values:
        center, deviation = mad(values)
        variance = sum((value - sum(values) / len(values)) ** 2 for value in values) / len(values)
        if not math.isfinite(deviation) or deviation <= 0.0 or not math.isfinite(variance) or variance <= 0.0:
            return None
        centers.append(center)
        deviations.append(deviation)
    correlations = _pearson_columns(residual_window)
    level = max(abs(centers[j] - reference.target_centers[j]) / reference.target_scales[j] for j in range(width))
    dispersion = max(abs(math.log(deviations[j] / (reference.target_scales[j] / MAD_NORMAL_SCALE))) for j in range(width))
    relationship = 2.0 * sum(
        (correlations[i][j] - reference.reference_correlations[i][j]) ** 2
        for i in range(width)
        for j in range(i)
    )
    result = (level, dispersion, relationship)
    if not all(math.isfinite(value) for value in result):
        return None
    return result


def fit_component_normalization(rows: Sequence[Cohort], reference: Reference) -> Reference:
    components: list[list[float]] = [[], [], []]
    by_group: dict[int, list[Cohort]] = {}
    for row in rows:
        by_group.setdefault(row.group_id, []).append(row)
    if tuple(sorted(by_group)) != FIT_GROUPS:
        raise StudyError("component fit must use the complete fit group set only")
    for group_id in FIT_GROUPS:
        group_rows = sorted(by_group[group_id], key=lambda row: row.index)
        if len(group_rows) != SAMPLES or any(row.scenario != "nonfault-context" for row in group_rows):
            raise StudyError("component fit encountered incomplete or injected fit group")
        residuals = [residual_for(row, reference.coefficients) for row in group_rows]
        for end in range(WINDOW - 1, SAMPLES):
            value = window_components(residuals[end - WINDOW + 1:end + 1], reference)
            if value is None:
                raise StudyError(f"fit group {group_id} has a degenerate full window at {end}")
            for index, component in enumerate(value):
                components[index].append(component)
    centers: list[float] = []
    scales: list[float] = []
    for values in components:
        center, deviation = mad(values)
        scale = MAD_NORMAL_SCALE * deviation
        if not math.isfinite(scale) or scale <= 0.0:
            raise StudyError("fit full-window component normalization is degenerate")
        centers.append(center)
        scales.append(scale)
    if any(len(values) != 60 * (SAMPLES - WINDOW + 1) for values in components):
        raise StudyError("fit component window count differs from 5,460")
    return replace(reference, component_centers=tuple(centers), component_scales=tuple(scales))


def score_from_components(components: Sequence[float], reference: Reference) -> float:
    if len(components) != 3 or len(reference.component_centers) != 3 or len(reference.component_scales) != 3:
        raise StudyError("component score shape mismatch")
    z_values = tuple((float(components[i]) - reference.component_centers[i]) / reference.component_scales[i] for i in range(3))
    if not all(math.isfinite(value) for value in z_values):
        raise StudyError("component standardization is nonfinite")
    return max(z_values)


def _finite_values(values: Iterable[float]) -> bool:
    return all(math.isfinite(float(value)) for value in values)


def _ineligible_reason(row: Cohort, duplicate_time: bool, duplicate_index: bool) -> str | None:
    if duplicate_time or duplicate_index:
        return "duplicate-source-key"
    if row.index < 0 or row.index >= SAMPLES or row.source_time_s != row.index * CADENCE_S or row.cadence_s != CADENCE_S:
        return "cadence-alignment"
    if len(row.targets) != 3 or not _finite_values(row.targets):
        return "target-nonfinite-or-shape"
    if not _finite_values((row.speed_rpm, row.load_fraction)):
        return "context-nonfinite"
    if not row.running:
        return "not-running"
    if row.quality != "Good":
        return "quality-not-good"
    if not row.synchronized or not row.source_verified:
        return "unsynchronized-or-unverified-source"
    if row.group_id < 0 or not row.source_id or not row.context_id or not row.profile_id or not row.model_version:
        return "source-context-identity-missing"
    return None


def score_run(rows: Sequence[Cohort], reference: Reference, threshold: float | None = None) -> RunSummary:
    if not rows:
        raise StudyError("cannot score an empty source run")
    group_ids = {row.group_id for row in rows}
    scenarios = {row.scenario for row in rows}
    if len(group_ids) != 1 or len(scenarios) != 1:
        raise StudyError("score_run must receive one group and one scenario only")
    group_id = next(iter(group_ids))
    scenario = next(iter(scenarios))
    time_counts = Counter(row.source_time_s for row in rows)
    index_counts = Counter(row.index for row in rows)
    ring: deque[tuple[float, ...]] = deque(maxlen=WINDOW)
    tracker = EpisodeTracker()
    eligible = warmup = numerical_no_score = reset_count = scored = exceedances = 0
    reasons: Counter[str] = Counter()
    confirmations: list[int] = []
    scores: list[float] = []
    previous: Cohort | None = None

    def reset_state() -> None:
        ring.clear()
        tracker.reset()

    for row in rows:
        reason = _ineligible_reason(row, time_counts[row.source_time_s] > 1, index_counts[row.index] > 1)
        if previous is not None:
            identity_changed = (
                row.source_id,
                row.context_id,
                row.profile_id,
                row.model_version,
            ) != (
                previous.source_id,
                previous.context_id,
                previous.profile_id,
                previous.model_version,
            )
            gap = row.index != previous.index + 1 or row.source_time_s != previous.source_time_s + CADENCE_S
            if identity_changed or gap:
                reset_state()
                reset_count += 1
        if reason is not None:
            reasons[reason] += 1
            reset_state()
            reset_count += 1
            previous = row
            continue

        eligible += 1
        residual = residual_for(row, reference.coefficients)
        ring.append(residual)
        previous = row
        if len(ring) < WINDOW:
            warmup += 1
            continue
        components = window_components(tuple(ring), reference)
        if components is None:
            numerical_no_score += 1
            reset_state()
            reset_count += 1
            continue
        try:
            score = score_from_components(components, reference)
        except StudyError:
            numerical_no_score += 1
            reset_state()
            reset_count += 1
            continue
        scored += 1
        scores.append(score)
        above = threshold is not None and score > threshold
        if above:
            exceedances += 1
        confirmed, _terminated = tracker.step(bool(above), row.index)
        if confirmed:
            confirmations.append(row.index)

    return RunSummary(
        group_id=group_id,
        scenario=scenario,
        input_cohorts=len(rows),
        eligible_cohorts=eligible,
        ineligible_reasons=dict(sorted(reasons.items())),
        warmup_cohorts=warmup,
        numerical_no_score=numerical_no_score,
        reset_count=reset_count,
        scored_decisions=scored,
        exceedances=exceedances,
        episode_confirmations=tuple(confirmations),
        scores=tuple(scores),
    )


def nearest_rank(values: Sequence[float], quantile: float) -> float:
    if not values or not 0.0 < quantile <= 1.0:
        raise StudyError("nearest-rank quantile input is empty or q is outside (0,1]")
    if not _finite_values(values):
        raise StudyError("nearest-rank quantile input contains nonfinite values")
    ordered = sorted(float(value) for value in values)
    return ordered[math.ceil(quantile * len(ordered)) - 1]


def _latency(summary: RunSummary) -> int | None:
    post_onset = [index for index in summary.episode_confirmations if index >= EVENT_START]
    if not post_onset:
        return None
    return (min(post_onset) - EVENT_START) * CADENCE_S


def percentile(values: Sequence[float], quantile: float) -> float | None:
    if not values:
        return None
    return nearest_rank(values, quantile)


def _group_metrics(summary: RunSummary) -> dict[str, Any]:
    latency = _latency(summary)
    return {
        "groupId": summary.group_id,
        "inputCohorts": summary.input_cohorts,
        "eligibleCohorts": summary.eligible_cohorts,
        "rawEligibleSeconds": summary.eligible_cohorts * CADENCE_S,
        "scoredDecisions": summary.scored_decisions,
        "scoredEligibleSeconds": summary.scored_decisions * CADENCE_S,
        "exceedances": summary.exceedances,
        "episodes": len(summary.episode_confirmations),
        "preOnsetEpisodes": sum(index < EVENT_START for index in summary.episode_confirmations),
        "eventDetected": latency is not None,
        "timelyDetected": latency is not None and latency <= 600,
        "eventLatencySeconds": latency,
        "independentEligible": summary.eligible_cohorts > 0 and summary.scored_decisions > 0,
    }


def _aggregate_groups(summaries: Sequence[RunSummary], scenario: str) -> dict[str, Any]:
    groups = [_group_metrics(summary) for summary in summaries]
    if not groups:
        raise StudyError("cannot aggregate an empty scenario partition")
    group_count = len(groups)
    eligible = sum(row["eligibleCohorts"] for row in groups)
    raw_seconds = sum(row["rawEligibleSeconds"] for row in groups)
    scored = sum(row["scoredDecisions"] for row in groups)
    scored_seconds = sum(row["scoredEligibleSeconds"] for row in groups)
    exceedances = sum(row["exceedances"] for row in groups)
    episodes = sum(row["episodes"] for row in groups)
    detected = sum(bool(row["eventDetected"]) for row in groups)
    timely = sum(bool(row["timelyDetected"]) for row in groups)
    latencies = [int(row["eventLatencySeconds"]) for row in groups if row["eventLatencySeconds"] is not None]
    reasons: Counter[str] = Counter()
    warmup = no_score = resets = 0
    for summary in summaries:
        reasons.update(summary.ineligible_reasons)
        warmup += summary.warmup_cohorts
        no_score += summary.numerical_no_score
        resets += summary.reset_count
    is_nonfault = scenario == "nonfault-context"
    raw_rate = exceedances / scored if scored else None
    episode_rate = episodes * 24.0 * 3600.0 / scored_seconds if scored_seconds else None
    result = {
        "scenario": scenario,
        "sourceGroups": group_count,
        "inputCohorts": sum(row["inputCohorts"] for row in groups),
        "independentEligibleGroups": sum(bool(row["independentEligible"]) for row in groups),
        "eligibleCohorts": eligible,
        "ineligibleCohorts": sum(sum(summary.ineligible_reasons.values()) for summary in summaries),
        "ineligibleReasonCounts": dict(sorted(reasons.items())),
        "warmupCohorts": warmup,
        "numericalNoScoreWindows": no_score,
        "resetCount": resets,
        "scoredDecisions": scored,
        "strictExceedances": exceedances,
        "pointFalsePositiveRate" if is_nonfault else "pointExceedanceRate": raw_rate,
        "episodes": episodes,
        "groupsWithEpisodes": sum(row["episodes"] > 0 for row in groups),
        "preOnsetEpisodes": sum(row["preOnsetEpisodes"] for row in groups),
        "rawEligibleSeconds": raw_seconds,
        "scoredEligibleSeconds": scored_seconds,
        "availability": scored_seconds / raw_seconds if raw_seconds else 0.0,
        "episodesPer24ScoredEligibleHours": episode_rate,
        "eventDetectedGroups": detected if not is_nonfault else None,
        "eventRecall": detected / group_count if not is_nonfault else None,
        "timelyDetectedGroups": timely if not is_nonfault else None,
        "timelyFraction600s": timely / group_count if not is_nonfault else None,
        "lateOrMissedGroups": group_count - timely if not is_nonfault else None,
        "detectedLatencySeconds": {
            "p50": percentile(latencies, 0.50),
            "p95": percentile(latencies, 0.95),
            "max": max(latencies) if latencies else None,
            "detectedGroupDenominator": len(latencies),
        } if not is_nonfault else None,
        "_groups": groups,
    }
    if is_nonfault:
        result["falseEpisodesPer24ScoredEligibleHours"] = episode_rate
    return result


def _wilson_interval(successes: int, trials: int, z: float = 1.959963984540054) -> tuple[float, float] | None:
    if trials <= 0:
        return None
    proportion = successes / trials
    denominator = 1.0 + z * z / trials
    center = (proportion + z * z / (2.0 * trials)) / denominator
    margin = z * math.sqrt(proportion * (1.0 - proportion) / trials + z * z / (4.0 * trials * trials)) / denominator
    return max(0.0, center - margin), min(1.0, center + margin)


def _bootstrap_resamples(group_count: int) -> tuple[tuple[int, ...], ...]:
    resamples: list[tuple[int, ...]] = []
    for replicate in range(BOOTSTRAP_REPLICATES):
        seed_text = f"{EXPERIMENT_ID}|bootstrap|holdout|replicate-{replicate:04d}".encode("utf-8")
        seed = int.from_bytes(hashlib.sha256(seed_text).digest()[:8], "big")
        rng = SplitMix64(seed)
        limit = ((1 << 64) // group_count) * group_count
        indices: list[int] = []
        while len(indices) < group_count:
            value = rng.next_u64()
            if value < limit:
                indices.append(value % group_count)
        resamples.append(tuple(indices))
    return tuple(resamples)


def _interval(values: Sequence[float]) -> dict[str, float | None]:
    return {"lower95": percentile(values, 0.025), "upper95": percentile(values, 0.975), "validReplicates": len(values)}


def attach_bootstrap_intervals(
    scenario_aggregates: dict[str, dict[str, Any]],
    scenario_summaries: dict[str, Sequence[RunSummary]],
) -> None:
    if not scenario_summaries:
        return
    group_count = len(next(iter(scenario_summaries.values())))
    if any(len(values) != group_count for values in scenario_summaries.values()):
        raise StudyError("paired bootstrap requires same independent group count for all variants")
    resamples = _bootstrap_resamples(group_count)
    for scenario, summaries in scenario_summaries.items():
        samples: dict[str, list[float]] = {
            "pointRate": [],
            "episodeRate": [],
            "recall": [],
            "timely": [],
            "latencyP50": [],
            "latencyP95": [],
        }
        for indices in resamples:
            sampled = [summaries[index] for index in indices]
            aggregate = _aggregate_groups(sampled, scenario)
            rate_key = "pointFalsePositiveRate" if scenario == "nonfault-context" else "pointExceedanceRate"
            rate = aggregate[rate_key]
            episode_key = "falseEpisodesPer24ScoredEligibleHours" if scenario == "nonfault-context" else "episodesPer24ScoredEligibleHours"
            episode_rate = aggregate[episode_key]
            if rate is not None:
                samples["pointRate"].append(float(rate))
            if episode_rate is not None:
                samples["episodeRate"].append(float(episode_rate))
            if scenario != "nonfault-context":
                samples["recall"].append(float(aggregate["eventRecall"]))
                samples["timely"].append(float(aggregate["timelyFraction600s"]))
                latency_block = aggregate["detectedLatencySeconds"]
                if latency_block["p50"] is not None:
                    samples["latencyP50"].append(float(latency_block["p50"]))
                    samples["latencyP95"].append(float(latency_block["p95"]))
        base_key = "pointFalsePositiveRate" if scenario == "nonfault-context" else "pointExceedanceRate"
        ci: dict[str, Any] = {
            base_key: _interval(samples["pointRate"]),
            ("falseEpisodesPer24ScoredEligibleHours" if scenario == "nonfault-context" else "episodesPer24ScoredEligibleHours"): _interval(samples["episodeRate"]),
        }
        if scenario != "nonfault-context":
            ci.update({
                "eventRecall": _interval(samples["recall"]),
                "timelyFraction600s": _interval(samples["timely"]),
                "detectedLatencyP50Seconds": _interval(samples["latencyP50"]),
                "detectedLatencyP95Seconds": _interval(samples["latencyP95"]),
            })
        scenario_aggregates[scenario]["clusterBootstrap95"] = ci
        if scenario == "nonfault-context" and scenario_aggregates[scenario]["groupsWithEpisodes"] == 0:
            wilson = _wilson_interval(0, group_count)
            scenario_aggregates[scenario]["zeroEpisodeGroupOccurrenceWilson95"] = {
                "lower95": wilson[0] if wilson else None,
                "upper95": wilson[1] if wilson else None,
                "groups": group_count,
            }


def _public_aggregate(value: dict[str, Any]) -> dict[str, Any]:
    return {key: item for key, item in value.items() if key != "_groups"}


def summarize_partition(
    scenario_summaries: dict[str, Sequence[RunSummary]],
    *,
    with_intervals: bool,
) -> dict[str, Any]:
    aggregates = {scenario: _aggregate_groups(items, scenario) for scenario, items in scenario_summaries.items()}
    if with_intervals:
        attach_bootstrap_intervals(aggregates, scenario_summaries)
    results: dict[str, Any] = {scenario: _public_aggregate(value) for scenario, value in aggregates.items()}
    if "nonfault-context" not in aggregates:
        raise StudyError("nonfault aggregate is missing")
    baseline = aggregates["nonfault-context"]
    fp = baseline["pointFalsePositiveRate"]
    episode_rate = baseline["falseEpisodesPer24ScoredEligibleHours"]
    base_ok = fp is not None and fp <= 0.01 and episode_rate is not None and episode_rate <= 1.0
    statuses: dict[str, str] = {}
    for scenario in STRONG_SCENARIOS:
        item = aggregates[scenario]
        if item["independentEligibleGroups"] < 100:
            status = "INSUFFICIENT"
        elif not base_ok or item["eventRecall"] is None or item["timelyFraction600s"] is None:
            status = "FAIL"
        elif item["eventRecall"] < 0.90 or item["timelyFraction600s"] < 0.90:
            status = "FAIL"
        else:
            status = "PASS"
        statuses[scenario] = status
    return {
        "scenarioAggregates": results,
        "commonNonfaultTargets": {
            "pointFalsePositiveMaximum": 0.01,
            "pointFalsePositiveRate": fp,
            "pointFalsePositivePass": fp is not None and fp <= 0.01,
            "falseEpisodesPer24ScoredEligibleHoursMaximum": 1.0,
            "falseEpisodesPer24ScoredEligibleHours": episode_rate,
            "falseEpisodeRatePass": episode_rate is not None and episode_rate <= 1.0,
        },
        "strongClassDisposition": statuses,
        "sensitivityScenarios": {
            scenario: "DESCRIPTIVE_ONLY_NOT_A_STRONG_CLASS_TARGET"
            for scenario in SCENARIOS
            if scenario not in STRONG_SCENARIOS and scenario != "nonfault-context"
        },
    }


def validate_query_budget(
    reference_cohorts: int,
    evaluation_cohorts: int,
    target_tags: int,
    context_tags: int,
    *,
    extra_raw_rows: int = 0,
    raw_limit: int = MAX_RAW_ROWS,
    page_size: int = PAGE_SIZE,
    window_days: int = 1,
    outputs: int = 1,
) -> int:
    if min(reference_cohorts, evaluation_cohorts, target_tags, context_tags, extra_raw_rows) < 0:
        raise StudyError("negative request-budget dimension")
    raw_rows = (reference_cohorts + evaluation_cohorts) * (target_tags + context_tags) + extra_raw_rows
    if page_size != PAGE_SIZE or window_days > MAX_WINDOW_DAYS or outputs > MAX_OUTPUTS:
        raise StudyError("request violates page-size, time-window, or output bound")
    if raw_rows > raw_limit:
        raise StudyError(f"request exceeds raw-row limit before filtering: {raw_rows}>{raw_limit}")
    return raw_rows


def _json_bytes(value: Any, *, pretty: bool = False) -> bytes:
    if pretty:
        return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False, allow_nan=False) + "\n").encode("utf-8")
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False).encode("utf-8")


def _sha256_bytes(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest().upper()


def _normalized_file_bytes(path: Path) -> bytes:
    return path.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")


def _sha256_file(path: Path) -> str:
    return _sha256_bytes(_normalized_file_bytes(path))


def _seal(value: dict[str, Any]) -> dict[str, Any]:
    unsigned = {key: item for key, item in value.items() if key != "selfSha256"}
    sealed = dict(unsigned)
    sealed["selfSha256"] = _sha256_bytes(_json_bytes(unsigned))
    return sealed


def _verify_seal(value: dict[str, Any]) -> None:
    expected = value.get("selfSha256")
    if not isinstance(expected, str):
        raise StudyError("sealed configuration lacks a self hash")
    unsigned = {key: item for key, item in value.items() if key != "selfSha256"}
    if _sha256_bytes(_json_bytes(unsigned)) != expected:
        raise StudyError("sealed configuration self hash mismatch")


def _repository_root() -> Path:
    completed = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=ROOT, capture_output=True, text=True, check=False)
    if completed.returncode != 0:
        raise StudyError("research folder is not inside a Git worktree")
    return Path(completed.stdout.strip())


def _git(*args: str) -> str:
    completed = subprocess.run(["git", *args], cwd=_repository_root(), capture_output=True, text=True, check=False)
    if completed.returncode != 0:
        raise StudyError(f"git provenance command failed: {completed.stderr.strip()}")
    return completed.stdout.strip()


def _assert_clean_repository() -> None:
    status = _git("status", "--porcelain")
    if status:
        raise StudyError("repository must be clean at this lifecycle checkpoint")


def _current_runtime() -> dict[str, Any]:
    return {
        "python": platform.python_version(),
        "implementation": platform.python_implementation(),
        "platform": platform.platform(),
        "system": platform.system(),
        "imageRef": os.environ.get("ELITESCADA_IMAGE_REF"),
    }


def _peak_rss_bytes() -> int:
    if os.name == "nt":
        class PROCESS_MEMORY_COUNTERS_EX(ctypes.Structure):
            _fields_ = [
                ("cb", ctypes.c_ulong),
                ("PageFaultCount", ctypes.c_ulong),
                ("PeakWorkingSetSize", ctypes.c_size_t),
                ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t),
                ("PeakPagefileUsage", ctypes.c_size_t),
                ("PrivateUsage", ctypes.c_size_t),
            ]

        counters = PROCESS_MEMORY_COUNTERS_EX()
        counters.cb = ctypes.sizeof(counters)
        psapi = ctypes.WinDLL("Psapi.dll")
        kernel32 = ctypes.WinDLL("Kernel32.dll")
        kernel32.GetCurrentProcess.restype = ctypes.c_void_p
        psapi.GetProcessMemoryInfo.argtypes = [ctypes.c_void_p, ctypes.POINTER(PROCESS_MEMORY_COUNTERS_EX), ctypes.c_ulong]
        psapi.GetProcessMemoryInfo.restype = ctypes.c_int
        ok = psapi.GetProcessMemoryInfo(kernel32.GetCurrentProcess(), ctypes.byref(counters), counters.cb)
        if not ok:
            raise StudyError("Windows peak RSS measurement failed")
        return int(counters.PeakWorkingSetSize)
    import resource

    peak = int(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss)
    return peak if sys.platform == "darwin" else peak * 1024


def _resource_report(start_wall: float, start_cpu: float, generated_vectors: int, passes: dict[str, int]) -> dict[str, Any]:
    wall = time.perf_counter() - start_wall
    cpu = time.process_time() - start_cpu
    rss = _peak_rss_bytes()
    if wall > WALL_LIMIT_SECONDS or cpu > CPU_LIMIT_SECONDS or rss > RSS_LIMIT_BYTES:
        raise StudyError(f"study process exceeded preregistered resource limits: wall={wall:.3f}s cpu={cpu:.3f}s rss={rss}")
    return {
        "wallSeconds": wall,
        "cpuSeconds": cpu,
        "peakRssBytes": rss,
        "generatedUniqueVectors": generated_vectors,
        "generatedUniqueScalarTargets": generated_vectors * 3,
        "processingPassRows": passes,
        "limits": {"wallSeconds": WALL_LIMIT_SECONDS, "cpuSeconds": CPU_LIMIT_SECONDS, "peakRssBytes": RSS_LIMIT_BYTES},
    }


def reference_record(reference: Reference) -> dict[str, Any]:
    return {
        "design": {
            "columns": ["intercept", "load", "(speed-1450)/20"],
            "rank": reference.design_rank,
            "condition2Norm": reference.design_condition,
            "maxCondition": MAX_CONDITION,
        },
        "targetOrder": list(TARGET_IDS),
        "olsCoefficientsByTarget": {TARGET_IDS[i]: list(reference.coefficients[i]) for i in range(3)},
        "residualCenters": {TARGET_IDS[i]: reference.target_centers[i] for i in range(3)},
        "residualRobustScales": {TARGET_IDS[i]: reference.target_scales[i] for i in range(3)},
        "referencePearsonCorrelation": [list(row) for row in reference.reference_correlations],
        "componentCenters": {name: reference.component_centers[i] for i, name in enumerate(("L", "D", "C"))},
        "componentRobustScales": {name: reference.component_scales[i] for i, name in enumerate(("L", "D", "C"))},
        "windowCohorts": WINDOW,
        "componentScaleMultiplier": MAD_NORMAL_SCALE,
    }


def _score_scenario_rows(
    rows_by_group: Sequence[Sequence[Cohort]], reference: Reference, threshold: float
) -> tuple[RunSummary, ...]:
    return tuple(score_run(rows, reference, threshold) for rows in rows_by_group)


def _calibration_rows(reference: Reference) -> tuple[dict[str, list[tuple[Cohort, ...]]], list[float]]:
    rows_by_scenario = {scenario: [] for scenario in SCENARIOS}
    reference_scores: list[float] = []
    for group_id in CAL_GROUPS:
        independent = independent_stream(group_id)
        group_rows: dict[str, tuple[Cohort, ...]] = {}
        for scenario in SCENARIOS:
            rows = generate_run(group_id, scenario, reference.target_scales, independent=independent)
            rows_by_scenario[scenario].append(rows)
            group_rows[scenario] = rows
        baseline = score_run(group_rows["nonfault-context"], reference)
        if len(baseline.scores) != SAMPLES - WINDOW + 1:
            raise StudyError(f"calibration baseline group {group_id} did not yield all 91 complete windows")
        reference_scores.extend(baseline.scores)
    if len(reference_scores) != len(CAL_GROUPS) * (SAMPLES - WINDOW + 1):
        raise StudyError("calibration nonfault score count differs from the frozen complete-window count")
    return rows_by_scenario, reference_scores


def run_fit_calibration() -> dict[str, Any]:
    if platform.python_version() != "3.14.6":
        raise StudyError(f"fit/calibration requires CPython 3.14.6, found {platform.python_version()}")
    if platform.python_implementation() != "CPython":
        raise StudyError("fit/calibration requires CPython")
    audit_group_partition()
    raw_budget = validate_query_budget(7200, 120, 3, 3)
    start_wall, start_cpu = time.perf_counter(), time.process_time()
    fit_rows: list[Cohort] = []
    for group_id in FIT_GROUPS:
        fit_rows.extend(generate_run(group_id, "nonfault-context"))
    if len(fit_rows) != 7200:
        raise StudyError("fit baseline unique cohort count differs from 7,200")
    target_reference = fit_contextual_reference(fit_rows)
    reference = fit_component_normalization(fit_rows, target_reference)
    cal_rows, calibration_reference_scores = _calibration_rows(reference)
    threshold = nearest_rank(calibration_reference_scores, 0.999)
    calibration_summaries = {
        scenario: _score_scenario_rows(rows, reference, threshold)
        for scenario, rows in cal_rows.items()
    }
    calibration_report = summarize_partition(calibration_summaries, with_intervals=False)
    numerical = {
        "fitReference": reference_record(reference),
        "calibrationThreshold": {
            "quantile": 0.999,
            "nearestRankIndexZeroBased": math.ceil(0.999 * len(calibration_reference_scores)) - 1,
            "nonfaultScoreCount": len(calibration_reference_scores),
            "value": threshold,
        },
        "calibrationAggregates": calibration_report,
    }
    resource = _resource_report(
        start_wall,
        start_cpu,
        7200 + len(CAL_GROUPS) * len(SCENARIOS) * SAMPLES,
        {
            "fitGeneratedRows": len(fit_rows),
            "fitOlsRows": len(fit_rows),
            "fitComponentWindowInputRows": len(fit_rows),
            "calibrationGeneratedRows": len(CAL_GROUPS) * len(SCENARIOS) * SAMPLES,
            "calibrationBaselineThresholdRows": len(CAL_GROUPS) * SAMPLES,
            "calibrationDecisionRows": len(CAL_GROUPS) * len(SCENARIOS) * SAMPLES,
        },
    )
    if sum(path.stat().st_size for path in ROOT.glob("*" ) if path.is_file()) > ARTIFACT_LIMIT_BYTES:
        raise StudyError("existing committed research artifacts exceed 1 MiB")
    return {
        "schemaVersion": 1,
        "state": "fit-calibration-complete-no-holdout-consumed",
        "experimentId": EXPERIMENT_ID,
        "seedNamespace": SEED_NAMESPACE,
        "sourceCommit": _git("rev-parse", "HEAD"),
        "protocolSha256": _sha256_file(PROTOCOL_PATH),
        "manifestSha256": _sha256_file(MANIFEST_PATH),
        "runtime": _current_runtime(),
        "rawRequestBudget": {
            "rawRows": raw_budget,
            "limit": MAX_RAW_ROWS,
            "targetTags": 3,
            "contextTags": 3,
            "referenceCohorts": 7200,
            "evaluationCohorts": 120,
            "countBeforeFiltering": True,
        },
        "numerical": numerical,
        "resources": resource,
    }


def _walk_numeric_compare(left: Any, right: Any, path: str = "numerical") -> tuple[bool, float, float, str | None]:
    max_abs = 0.0
    max_rel = 0.0
    if isinstance(left, bool) or isinstance(right, bool):
        return (type(left) is type(right) and left == right, max_abs, max_rel, None if left == right else path)
    if isinstance(left, (int, float)) and isinstance(right, (int, float)):
        a, b = float(left), float(right)
        if not math.isfinite(a) or not math.isfinite(b):
            return (a == b, max_abs, max_rel, None if a == b else path)
        delta = abs(a - b)
        rel = delta / max(abs(a), abs(b), 1e-300)
        close = delta <= max(1e-9, 1e-9 * max(abs(a), abs(b)))
        return (close, delta, rel, None if close else path)
    if type(left) is not type(right):
        return False, max_abs, max_rel, path
    if isinstance(left, dict):
        if set(left) != set(right):
            return False, max_abs, max_rel, path
        for key in sorted(left):
            ok, child_abs, child_rel, failure = _walk_numeric_compare(left[key], right[key], f"{path}.{key}")
            max_abs, max_rel = max(max_abs, child_abs), max(max_rel, child_rel)
            if not ok:
                return False, max_abs, max_rel, failure
        return True, max_abs, max_rel, None
    if isinstance(left, list):
        if len(left) != len(right):
            return False, max_abs, max_rel, path
        for index, (a, b) in enumerate(zip(left, right, strict=True)):
            ok, child_abs, child_rel, failure = _walk_numeric_compare(a, b, f"{path}[{index}]")
            max_abs, max_rel = max(max_abs, child_abs), max(max_rel, child_rel)
            if not ok:
                return False, max_abs, max_rel, failure
        return True, max_abs, max_rel, None
    return (left == right, max_abs, max_rel, None if left == right else path)


def compare_fit_calibration(windows_path: Path, linux_path: Path, output_path: Path) -> dict[str, Any]:
    windows = json.loads(windows_path.read_text(encoding="utf-8"))
    linux = json.loads(linux_path.read_text(encoding="utf-8"))
    if windows.get("state") != "fit-calibration-complete-no-holdout-consumed" or linux.get("state") != windows.get("state"):
        raise StudyError("fit/calibration inputs are not both complete no-holdout artifacts")
    if windows.get("experimentId") != EXPERIMENT_ID or linux.get("experimentId") != EXPERIMENT_ID:
        raise StudyError("cross-platform artifact experiment identity mismatch")
    if windows.get("runtime", {}).get("system") != "Windows" or linux.get("runtime", {}).get("system") != "Linux":
        raise StudyError("fit/calibration artifacts are not from Windows and isolated Linux")
    if windows.get("runtime", {}).get("python") != "3.14.6" or linux.get("runtime", {}).get("python") != "3.14.6":
        raise StudyError("cross-platform runtimes are not both Python 3.14.6")
    if linux.get("runtime", {}).get("imageRef") != LINUX_IMAGE:
        raise StudyError("Linux fit/calibration did not use the pinned image digest")
    equal, max_abs, max_rel, mismatch = _walk_numeric_compare(windows.get("numerical"), linux.get("numerical"))
    result = {
        "schemaVersion": 1,
        "state": "MATCH" if equal else "CROSS-PLATFORM-DIVERGENCE",
        "windowsRuntime": windows["runtime"],
        "linuxRuntime": linux["runtime"],
        "absoluteTolerance": 1e-9,
        "relativeTolerance": 1e-9,
        "maximumAbsoluteDifference": max_abs,
        "maximumRelativeDifference": max_rel,
        "fixedThresholdRuleAndScenarioKeysMatch": equal,
        "firstMismatchPath": mismatch,
        "windowsNumericalSha256": _sha256_bytes(_json_bytes(windows["numerical"])),
        "linuxNumericalSha256": _sha256_bytes(_json_bytes(linux["numerical"])),
        "windowsArtifactSha256": _sha256_file(windows_path),
        "linuxArtifactSha256": _sha256_file(linux_path),
    }
    if output_path.exists():
        raise StudyError(f"refusing to overwrite comparison artifact: {output_path.name}")
    _write_json_exclusive(output_path, result)
    return result


def _assert_local_artifact_path(path: Path) -> Path:
    resolved = path.resolve()
    if resolved.parent != ROOT.resolve():
        raise StudyError("study artifacts must be written directly inside the new research directory")
    return resolved


def _assert_artifact_budget(extra_bytes: int = 0) -> None:
    current = sum(path.stat().st_size for path in ROOT.iterdir() if path.is_file())
    if current + extra_bytes > ARTIFACT_LIMIT_BYTES:
        raise StudyError("committed research artifacts would exceed the 1 MiB bound")


def _atomic_new_bytes(path: Path, payload: bytes) -> None:
    flags = os.O_CREAT | os.O_EXCL | os.O_WRONLY
    if hasattr(os, "O_BINARY"):
        flags |= os.O_BINARY
    try:
        fd = os.open(path, flags, 0o600)
    except FileExistsError as error:
        raise StudyError(f"refusing to overwrite existing file: {path.name}") from error
    with os.fdopen(fd, "wb") as stream:
        stream.write(payload)
        stream.flush()
        os.fsync(stream.fileno())


def _write_json_exclusive(path: Path, value: dict[str, Any]) -> None:
    path = _assert_local_artifact_path(path)
    payload = _json_bytes(value, pretty=True)
    if path.exists():
        raise StudyError(f"refusing to overwrite existing artifact: {path.name}")
    _assert_artifact_budget(len(payload))
    _atomic_new_bytes(path, payload)


def _load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise StudyError(f"expected a JSON object in {path.name}")
    return value


def freeze_configuration(
    windows_path: Path,
    linux_path: Path,
    comparison_path: Path,
    output_path: Path,
) -> dict[str, Any]:
    windows_path = _assert_local_artifact_path(windows_path)
    linux_path = _assert_local_artifact_path(linux_path)
    comparison_path = _assert_local_artifact_path(comparison_path)
    output_path = _assert_local_artifact_path(output_path)
    if output_path.exists():
        raise StudyError("frozen configuration already exists; refusing overwrite")
    _assert_clean_repository()
    branch = _git("branch", "--show-current")
    if branch != "work/w15-intelligence-stage4f-windowed-study":
        raise StudyError(f"unexpected branch at freeze: {branch}")
    windows = _load_json(windows_path)
    linux = _load_json(linux_path)
    comparison = _load_json(comparison_path)
    if comparison.get("state") != "MATCH":
        raise StudyError("cannot freeze: Windows/Linux fit/calibration comparison is not MATCH")
    if comparison.get("windowsArtifactSha256") != _sha256_file(windows_path) or comparison.get("linuxArtifactSha256") != _sha256_file(linux_path):
        raise StudyError("fit/calibration files changed after numerical comparison")
    if windows.get("numerical") != linux.get("numerical"):
        equal, _, _, _ = _walk_numeric_compare(windows.get("numerical"), linux.get("numerical"))
        if not equal:
            raise StudyError("numerical fit/calibration mismatch prevents freeze")
    source_commit = _git("rev-parse", "HEAD")
    hashes = {
        "protocol": _sha256_file(PROTOCOL_PATH),
        "manifest": _sha256_file(MANIFEST_PATH),
        "harness": _sha256_file(ROOT / "study.py"),
        "tests": _sha256_file(ROOT / "test_study.py"),
        "windowsFitCalibration": _sha256_file(windows_path),
        "linuxFitCalibration": _sha256_file(linux_path),
        "crossPlatformComparison": _sha256_file(comparison_path),
    }
    config = {
        "schemaVersion": 1,
        "state": "frozen-before-one-use-holdout",
        "experimentId": EXPERIMENT_ID,
        "holdoutId": HOLDOUT_ID,
        "seedNamespace": SEED_NAMESPACE,
        "branch": branch,
        "baseSha": "2da9cfa76a9d21b4d6b31097ed0921edaba2c01d",
        "sourceCommitBeforeFreeze": source_commit,
        "runtime": windows["runtime"],
        "linuxRuntime": linux["runtime"],
        "hashes": hashes,
        "fitReference": windows["numerical"]["fitReference"],
        "calibrationThreshold": windows["numerical"]["calibrationThreshold"],
        "calibrationAggregates": windows["numerical"]["calibrationAggregates"],
        "crossPlatformComparison": {
            "state": comparison["state"],
            "absoluteTolerance": comparison["absoluteTolerance"],
            "relativeTolerance": comparison["relativeTolerance"],
            "maximumAbsoluteDifference": comparison["maximumAbsoluteDifference"],
            "maximumRelativeDifference": comparison["maximumRelativeDifference"],
        },
        "freezeRule": "all fit/calibration values and one q=.999 threshold are frozen; holdout has no tuning or retry",
    }
    sealed = _seal(config)
    _write_json_exclusive(output_path, sealed)
    return sealed


def _reference_from_record(record: dict[str, Any]) -> Reference:
    coefficients_by_target = record["olsCoefficientsByTarget"]
    centers = record["residualCenters"]
    scales = record["residualRobustScales"]
    component_centers = record["componentCenters"]
    component_scales = record["componentRobustScales"]
    return Reference(
        coefficients=tuple(tuple(float(value) for value in coefficients_by_target[name]) for name in TARGET_IDS),
        target_centers=tuple(float(centers[name]) for name in TARGET_IDS),
        target_scales=tuple(float(scales[name]) for name in TARGET_IDS),
        reference_correlations=tuple(tuple(float(value) for value in row) for row in record["referencePearsonCorrelation"]),
        component_centers=tuple(float(component_centers[name]) for name in ("L", "D", "C")),
        component_scales=tuple(float(component_scales[name]) for name in ("L", "D", "C")),
        design_rank=int(record["design"]["rank"]),
        design_condition=float(record["design"]["condition2Norm"]),
    )


def _default_claim_directory() -> Path:
    local = os.environ.get("LOCALAPPDATA")
    if not local:
        raise StudyError("canonical Windows LOCALAPPDATA is unavailable; holdout must not run")
    return Path(local) / "EliteSCADA" / "research-holdout-claims"


def _identity_hash(identity: str) -> str:
    return hashlib.sha256(identity.encode("utf-8")).hexdigest()


def claim_final_holdout_once(
    state_directory: Path,
    identity: str,
    binding: dict[str, Any],
    receipt_path: Path,
) -> Path:
    """Atomically persist a claim before generation; never remove or repair it."""
    if receipt_path.exists():
        raise StudyError("committed holdout receipt already exists; replay is forbidden")
    state_directory.mkdir(parents=True, exist_ok=True)
    claim_path = state_directory / f"{_identity_hash(identity)}.json"
    claim = {
        "schemaVersion": 1,
        "state": "claimed-before-generation",
        "identity": identity,
        "identitySha256": _identity_hash(identity),
        "claimedAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "binding": binding,
    }
    payload = _json_bytes(claim, pretty=True)
    try:
        _atomic_new_bytes(claim_path, payload)
    except StudyError as error:
        raise StudyError("holdout identity claim already exists (possibly interrupted); hard stop") from error
    return claim_path


def _verify_frozen_inputs(config_path: Path, windows_path: Path, linux_path: Path, comparison_path: Path) -> tuple[dict[str, Any], str]:
    config_path = _assert_local_artifact_path(config_path)
    windows_path = _assert_local_artifact_path(windows_path)
    linux_path = _assert_local_artifact_path(linux_path)
    comparison_path = _assert_local_artifact_path(comparison_path)
    config = _load_json(config_path)
    _verify_seal(config)
    if config.get("state") != "frozen-before-one-use-holdout" or config.get("experimentId") != EXPERIMENT_ID or config.get("holdoutId") != HOLDOUT_ID:
        raise StudyError("frozen configuration identity/state mismatch")
    paths = {
        "protocol": PROTOCOL_PATH,
        "manifest": MANIFEST_PATH,
        "harness": ROOT / "study.py",
        "tests": ROOT / "test_study.py",
        "windowsFitCalibration": windows_path,
        "linuxFitCalibration": linux_path,
        "crossPlatformComparison": comparison_path,
    }
    for name, path in paths.items():
        if config.get("hashes", {}).get(name) != _sha256_file(path):
            raise StudyError(f"frozen input changed after config freeze: {name}")
    comparison = _load_json(comparison_path)
    if comparison.get("state") != "MATCH":
        raise StudyError("frozen cross-platform comparison is not MATCH")
    _assert_clean_repository()
    branch = _git("branch", "--show-current")
    if branch != config.get("branch"):
        raise StudyError("holdout branch differs from the frozen configuration")
    head = _git("rev-parse", "HEAD")
    parent = _git("rev-parse", "HEAD^")
    if parent != config.get("sourceCommitBeforeFreeze"):
        raise StudyError("holdout HEAD is not the single frozen-config commit over frozen source")
    if not subprocess.run(["git", "merge-base", "--is-ancestor", config["baseSha"], "HEAD"], cwd=_repository_root(), check=False).returncode == 0:
        raise StudyError("assigned base is not an ancestor of the holdout source commit")
    return config, head


def run_final_holdout(
    config_path: Path,
    windows_path: Path,
    linux_path: Path,
    comparison_path: Path,
    aggregate_path: Path,
    receipt_path: Path,
) -> dict[str, Any]:
    if os.name != "nt" or platform.python_version() != "3.14.6" or platform.python_implementation() != "CPython":
        raise StudyError("final holdout is authorized only on Windows CPython 3.14.6")
    aggregate_path = _assert_local_artifact_path(aggregate_path)
    receipt_path = _assert_local_artifact_path(receipt_path)
    if aggregate_path.exists() or receipt_path.exists():
        raise StudyError("existing holdout aggregate or receipt is a hard replay stop")
    config, source_head = _verify_frozen_inputs(config_path, windows_path, linux_path, comparison_path)
    binding = {
        "experimentId": EXPERIMENT_ID,
        "holdoutId": HOLDOUT_ID,
        "sourceCommitAtClaim": source_head,
        "frozenConfigSha256": _sha256_file(config_path),
        **config["hashes"],
    }
    claim_path = claim_final_holdout_once(_default_claim_directory(), HOLDOUT_ID, binding, receipt_path)

    # A claim is permanent from this point. Any exception leaves it in place and forbids retry.
    start_wall, start_cpu = time.perf_counter(), time.process_time()
    reference = _reference_from_record(config["fitReference"])
    threshold = float(config["calibrationThreshold"]["value"])
    scenario_summaries: dict[str, list[RunSummary]] = {scenario: [] for scenario in SCENARIOS}
    for group_id in HOLDOUT_GROUPS:
        independent = independent_stream(group_id)
        for scenario in SCENARIOS:
            rows = generate_run(group_id, scenario, reference.target_scales, independent=independent)
            summary = score_run(rows, reference, threshold)
            scenario_summaries[scenario].append(summary)
    holdout_report = summarize_partition(scenario_summaries, with_intervals=True)
    resources = _resource_report(
        start_wall,
        start_cpu,
        len(HOLDOUT_GROUPS) * len(SCENARIOS) * SAMPLES,
        {
            "holdoutGeneratedRows": len(HOLDOUT_GROUPS) * len(SCENARIOS) * SAMPLES,
            "holdoutWindowDecisionRows": sum(len(items) for items in scenario_summaries.values()) * SAMPLES,
            "bootstrapReplicates": BOOTSTRAP_REPLICATES,
        },
    )
    artifact = {
        "schemaVersion": 1,
        "state": "single-holdout-consumed",
        "experimentId": EXPERIMENT_ID,
        "holdoutId": HOLDOUT_ID,
        "sourceCommitAtClaim": source_head,
        "frozenConfigurationSha256": binding["frozenConfigSha256"],
        "protocolSha256": config["hashes"]["protocol"],
        "manifestSha256": config["hashes"]["manifest"],
        "harnessSha256": config["hashes"]["harness"],
        "testSha256": config["hashes"]["tests"],
        "threshold": config["calibrationThreshold"],
        "runtime": _current_runtime(),
        "resources": resources,
        "holdoutMetrics": holdout_report,
        "evidenceLimits": [
            "synthetic conditional only for three declared target signals",
            "no operational, equipment-health, diagnosis, RUL, standards, product or field validation",
            "overlapping windows and AR observations are not independent trials",
            "all sensitivity-only cases remain descriptive and cannot satisfy strong-class gates",
        ],
    }
    _write_json_exclusive(aggregate_path, artifact)
    receipt = {
        "schemaVersion": 1,
        "state": "committed-single-consumption",
        "experimentId": EXPERIMENT_ID,
        "holdoutId": HOLDOUT_ID,
        "sourceCommitAtClaim": source_head,
        "frozenConfigurationSha256": binding["frozenConfigSha256"],
        "claimPathSha256": _sha256_file(claim_path),
        "aggregateSha256": _sha256_file(aggregate_path),
        "groups": list(HOLDOUT_GROUPS),
        "consumptionCount": 1,
        "replayPermitted": False,
        "strongClassDisposition": holdout_report["strongClassDisposition"],
    }
    _write_json_exclusive(receipt_path, receipt)
    return artifact


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    fit_parser = subparsers.add_parser("fit-cal", help="run fit and calibration only; never generates holdout groups")
    fit_parser.add_argument("--output", type=Path, default=ROOT / "fit-calibration-windows-v1.json")
    compare_parser = subparsers.add_parser("compare", help="compare Windows/Linux numerical fit and calibration outputs")
    compare_parser.add_argument("--windows", type=Path, default=ROOT / "fit-calibration-windows-v1.json")
    compare_parser.add_argument("--linux", type=Path, default=ROOT / "fit-calibration-linux-v1.json")
    compare_parser.add_argument("--output", type=Path, default=ROOT / "fit-calibration-comparison-v1.json")
    freeze_parser = subparsers.add_parser("freeze", help="seal exactly one threshold/configuration before holdout")
    freeze_parser.add_argument("--windows", type=Path, default=ROOT / "fit-calibration-windows-v1.json")
    freeze_parser.add_argument("--linux", type=Path, default=ROOT / "fit-calibration-linux-v1.json")
    freeze_parser.add_argument("--comparison", type=Path, default=ROOT / "fit-calibration-comparison-v1.json")
    freeze_parser.add_argument("--output", type=Path, default=ROOT / "frozen-config-v1.json")
    holdout_parser = subparsers.add_parser("holdout", help="consume the exact one-use Windows holdout after config commit")
    holdout_parser.add_argument("--config", type=Path, default=ROOT / "frozen-config-v1.json")
    holdout_parser.add_argument("--windows", type=Path, default=ROOT / "fit-calibration-windows-v1.json")
    holdout_parser.add_argument("--linux", type=Path, default=ROOT / "fit-calibration-linux-v1.json")
    holdout_parser.add_argument("--comparison", type=Path, default=ROOT / "fit-calibration-comparison-v1.json")
    holdout_parser.add_argument("--aggregate", type=Path, default=ROOT / "holdout-aggregate-v1.json")
    holdout_parser.add_argument("--receipt", type=Path, default=ROOT / "holdout-receipt-v1.json")
    args = parser.parse_args(argv)

    if args.command == "fit-cal":
        output = _assert_local_artifact_path(args.output)
        if output.exists():
            raise StudyError(f"fit/calibration output already exists: {output.name}")
        _assert_clean_repository()
        result = run_fit_calibration()
        _write_json_exclusive(output, result)
        print(json.dumps({"state": result["state"], "output": str(output), "resources": result["resources"]}, sort_keys=True))
        return 0
    if args.command == "compare":
        result = compare_fit_calibration(args.windows, args.linux, args.output)
        print(json.dumps(result, sort_keys=True))
        return 0 if result["state"] == "MATCH" else 2
    if args.command == "freeze":
        result = freeze_configuration(args.windows, args.linux, args.comparison, args.output)
        print(json.dumps({"state": result["state"], "selfSha256": result["selfSha256"], "sourceCommitBeforeFreeze": result["sourceCommitBeforeFreeze"]}, sort_keys=True))
        return 0
    if args.command == "holdout":
        result = run_final_holdout(args.config, args.windows, args.linux, args.comparison, args.aggregate, args.receipt)
        print(json.dumps({"state": result["state"], "holdoutMetrics": result["holdoutMetrics"]["strongClassDisposition"], "resources": result["resources"]}, sort_keys=True))
        return 0
    raise StudyError("unhandled command")


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except StudyError as error:
        print(f"STUDY STOP: {error}", file=sys.stderr)
        raise SystemExit(2)
