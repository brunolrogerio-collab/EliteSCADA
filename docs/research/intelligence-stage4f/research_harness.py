"""Bounded, standard-library-only Stage 4F offline research helpers.

This module is intentionally isolated under docs/research. It is not imported by
the product, does not access Historian, and never writes record-level scores.
"""

from __future__ import annotations

import array
import csv
import hashlib
import io
import json
import math
import os
import platform
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable, Sequence

try:
    import resource
except ImportError:  # Windows; peak RSS is collected via GetProcessMemoryInfo.
    resource = None  # type: ignore[assignment]


class ResearchDataError(ValueError):
    """The pinned research input or its declared shape is invalid."""


SAMPLES_PER_CYCLE = 6000
EXPECTED_CYCLE_LABEL = (100, 100, 0, 130, 0)
FIT_CYCLES = tuple(range(1788, 1794))
CALIBRATION_CYCLES = (1794, 1795)
HOLDOUT_CYCLES = (1796, 1797)
ALL_SELECTED_CYCLES = FIT_CYCLES + CALIBRATION_CYCLES + HOLDOUT_CYCLES
SIGNALS_BY_DIMENSION = {
    3: ("EPS1", "PS1", "PS2"),
    5: ("EPS1", "PS1", "PS2", "PS3", "PS4"),
    7: ("EPS1", "PS1", "PS2", "PS3", "PS4", "PS5", "PS6"),
}
LAMBDA_GRID = (0.05, 0.10, 0.25, 0.50)
CALIBRATION_QUANTILE = 0.99
MAD_NORMAL_SCALE = 1.4826
EIGEN_RELATIVE_TOLERANCE = 1e-12
SPLIT_CYCLES = {
    "fit": FIT_CYCLES,
    "calibration": CALIBRATION_CYCLES,
    "holdout": HOLDOUT_CYCLES,
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def sha256_json(value: dict) -> str:
    payload = json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")
    return sha256_bytes(payload)


def verify_archive(archive_path: Path, source_manifest_path: Path) -> tuple[dict, str]:
    try:
        manifest = json.loads(source_manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ResearchDataError(f"source manifest cannot be read: {exc}") from exc

    hydraulic = next(
        (item for item in manifest.get("sources", []) if item.get("studyRole") == "required hydraulic pump-like test-rig pilot"),
        None,
    )
    if hydraulic is None:
        raise ResearchDataError("source manifest has no pinned hydraulic pilot")
    actual = sha256_file(archive_path)
    expected = str(hydraulic.get("sha256", "")).upper()
    if actual != expected:
        raise ResearchDataError(f"hydraulic archive SHA-256 mismatch: expected {expected}, got {actual}")
    return manifest, actual


def validate_partition_mapping(partitions: dict[str, Sequence[int]]) -> None:
    if set(partitions) != set(SPLIT_CYCLES):
        raise ResearchDataError("partitions must be exactly fit, calibration, and holdout")
    seen: set[int] = set()
    for name in ("fit", "calibration", "holdout"):
        ids = tuple(int(value) for value in partitions[name])
        if not ids:
            raise ResearchDataError(f"partition {name} is empty")
        overlap = seen.intersection(ids)
        if overlap:
            raise ResearchDataError(f"cycle group appears in more than one partition: {sorted(overlap)}")
        seen.update(ids)
    if set(partitions["fit"]) != set(FIT_CYCLES):
        raise ResearchDataError("fit cycles differ from the frozen 1788–1793 split")
    if set(partitions["calibration"]) != set(CALIBRATION_CYCLES):
        raise ResearchDataError("calibration cycles differ from the frozen 1794–1795 split")
    if set(partitions["holdout"]) != set(HOLDOUT_CYCLES):
        raise ResearchDataError("holdout cycles differ from the frozen 1796–1797 split")


def read_split_manifest(path: Path) -> dict[str, tuple[int, ...]]:
    partitions: dict[str, list[int]] = {name: [] for name in SPLIT_CYCLES}
    try:
        with path.open("r", encoding="utf-8", newline="") as stream:
            reader = csv.DictReader(stream)
            required = {"cycle_id", "partition", "cooler_pct", "valve_pct", "pump_leakage", "accumulator_pressure_bar", "stable_flag", "source_row_index_zero_based", "within_cycle_positions", "selected_signals"}
            if not required.issubset(set(reader.fieldnames or ())):
                raise ResearchDataError("split manifest is missing required columns")
            rows = list(reader)
    except OSError as exc:
        raise ResearchDataError(f"split manifest cannot be read: {exc}") from exc

    if len(rows) != len(ALL_SELECTED_CYCLES):
        raise ResearchDataError("split manifest must contain exactly ten selected cycles")
    seen: set[int] = set()
    expected_signal_list = "|".join(SIGNALS_BY_DIMENSION[7])
    for row in rows:
        try:
            cycle = int(row["cycle_id"])
            zero_based = int(row["source_row_index_zero_based"])
            label = tuple(int(row[key]) for key in ("cooler_pct", "valve_pct", "pump_leakage", "accumulator_pressure_bar", "stable_flag"))
        except (KeyError, ValueError, TypeError) as exc:
            raise ResearchDataError("split manifest contains an invalid numeric field") from exc
        partition = row["partition"]
        if cycle in seen:
            raise ResearchDataError(f"duplicate cycle in split manifest: {cycle}")
        seen.add(cycle)
        if partition not in partitions:
            raise ResearchDataError(f"unexpected partition name: {partition}")
        if cycle not in SPLIT_CYCLES[partition]:
            raise ResearchDataError(f"cycle {cycle} is assigned to the wrong partition")
        if zero_based != cycle - 1:
            raise ResearchDataError(f"cycle {cycle} has an invalid zero-based source row index")
        if label != EXPECTED_CYCLE_LABEL:
            raise ResearchDataError(f"cycle {cycle} is not in the declared nominal+stable profile")
        if row["within_cycle_positions"] != f"0-{SAMPLES_PER_CYCLE - 1}":
            raise ResearchDataError(f"cycle {cycle} has an unexpected within-cycle range")
        if row["selected_signals"] != expected_signal_list:
            raise ResearchDataError(f"cycle {cycle} has an unexpected signal set")
        partitions[partition].append(cycle)

    result = {name: tuple(values) for name, values in partitions.items()}
    validate_partition_mapping(result)
    return result


def read_profile_cycles(archive_path: Path, cycle_ids: Sequence[int]) -> dict[int, tuple[int, ...]]:
    wanted = tuple(sorted(set(int(cycle) for cycle in cycle_ids)))
    if not wanted or wanted[0] < 1:
        raise ResearchDataError("profile cycle IDs must be positive one-based rows")
    found: dict[int, tuple[int, ...]] = {}
    try:
        with zipfile.ZipFile(archive_path) as archive:
            with archive.open("profile.txt") as raw:
                for row_index, line in enumerate(raw, start=1):
                    if row_index in wanted:
                        fields = line.decode("ascii").strip().split("\t")
                        if len(fields) != 5:
                            raise ResearchDataError(f"profile cycle {row_index} has {len(fields)} fields, expected 5")
                        try:
                            found[row_index] = tuple(int(value) for value in fields)
                        except ValueError as exc:
                            raise ResearchDataError(f"profile cycle {row_index} contains a noninteger label") from exc
                    if row_index >= wanted[-1]:
                        break
    except (OSError, KeyError, zipfile.BadZipFile, UnicodeDecodeError) as exc:
        raise ResearchDataError(f"cannot read selected profile rows: {exc}") from exc
    if set(found) != set(wanted):
        raise ResearchDataError(f"profile rows missing from source: {sorted(set(wanted) - set(found))}")
    for cycle, label in found.items():
        if label != EXPECTED_CYCLE_LABEL:
            raise ResearchDataError(f"cycle {cycle} does not match the frozen nominal+stable cohort")
    return found


def read_signal_cycles(
    archive_path: Path,
    signal: str,
    cycle_ids: Sequence[int],
    expected_samples: int = SAMPLES_PER_CYCLE,
) -> dict[int, array.array]:
    wanted = tuple(sorted(set(int(cycle) for cycle in cycle_ids)))
    if not wanted or wanted[0] < 1:
        raise ResearchDataError("signal cycle IDs must be positive one-based rows")
    try:
        with zipfile.ZipFile(archive_path) as archive:
            with archive.open(f"{signal}.txt") as raw:
                found: dict[int, array.array] = {}
                for row_index, line in enumerate(raw, start=1):
                    if row_index in wanted:
                        tokens = line.rstrip(b"\r\n").split(b"\t")
                        if len(tokens) != expected_samples:
                            raise ResearchDataError(
                                f"{signal} cycle {row_index} has {len(tokens)} samples, expected {expected_samples}"
                            )
                        values = array.array("d")
                        for position, token in enumerate(tokens):
                            try:
                                value = float(token)
                            except ValueError as exc:
                                raise ResearchDataError(
                                    f"{signal} cycle {row_index} position {position} is not numeric"
                                ) from exc
                            if not math.isfinite(value):
                                raise ResearchDataError(
                                    f"{signal} cycle {row_index} position {position} is not finite"
                                )
                            values.append(value)
                        found[row_index] = values
                    if row_index >= wanted[-1]:
                        break
    except ResearchDataError:
        raise
    except (OSError, KeyError, zipfile.BadZipFile) as exc:
        raise ResearchDataError(f"cannot read selected {signal} rows: {exc}") from exc
    if set(found) != set(wanted):
        raise ResearchDataError(f"{signal} rows missing from source: {sorted(set(wanted) - set(found))}")
    return found


def load_channels(archive_path: Path, signals: Sequence[str], cycle_ids: Sequence[int]) -> dict[str, dict[int, array.array]]:
    channels = {signal: read_signal_cycles(archive_path, signal, cycle_ids) for signal in signals}
    for cycle in cycle_ids:
        lengths = {len(channels[signal][cycle]) for signal in signals}
        if lengths != {SAMPLES_PER_CYCLE}:
            raise ResearchDataError(f"selected signals have mismatched within-cycle positions for cycle {cycle}")
    return channels


def iter_vectors(channels: dict[str, dict[int, array.array]], signals: Sequence[str], cycle_ids: Sequence[int]) -> Iterable[tuple[float, ...]]:
    for cycle in cycle_ids:
        series = [channels[signal][cycle] for signal in signals]
        for position in range(SAMPLES_PER_CYCLE):
            yield tuple(float(channel[position]) for channel in series)


def median(values: Sequence[float]) -> float:
    if not values:
        raise ResearchDataError("median requires at least one value")
    ordered = sorted(values)
    middle = len(ordered) // 2
    if len(ordered) % 2:
        return ordered[middle]
    return (ordered[middle - 1] + ordered[middle]) / 2.0


def fit_robust_scaling(rows: Sequence[Sequence[float]]) -> tuple[list[float], list[float]]:
    if not rows:
        raise ResearchDataError("robust scaling requires fitting observations")
    dimensions = len(rows[0])
    if dimensions < 1 or any(len(row) != dimensions for row in rows):
        raise ResearchDataError("fitting vectors have inconsistent dimensions")
    centers: list[float] = []
    scales: list[float] = []
    for dimension in range(dimensions):
        column = [float(row[dimension]) for row in rows]
        if any(not math.isfinite(value) for value in column):
            raise ResearchDataError(f"signal dimension {dimension} contains a nonfinite fitting value")
        center = median(column)
        mad = median([abs(value - center) for value in column])
        scale = MAD_NORMAL_SCALE * mad
        if not math.isfinite(scale) or scale <= 0.0:
            raise ResearchDataError(f"signal dimension {dimension} has a zero or invalid robust scale")
        centers.append(center)
        scales.append(scale)
    return centers, scales


def transform_rows(rows: Iterable[Sequence[float]], centers: Sequence[float], scales: Sequence[float]) -> list[list[float]]:
    if len(centers) != len(scales):
        raise ResearchDataError("center and scale dimensions do not match")
    result: list[list[float]] = []
    for row in rows:
        if len(row) != len(centers):
            raise ResearchDataError("input vector dimension does not match fitted scaling")
        transformed = [(float(value) - centers[index]) / scales[index] for index, value in enumerate(row)]
        if any(not math.isfinite(value) for value in transformed):
            raise ResearchDataError("transformed vector contains a nonfinite value")
        result.append(transformed)
    return result


def covariance_matrix(rows: Sequence[Sequence[float]]) -> tuple[list[list[float]], list[float]]:
    if len(rows) < 2:
        raise ResearchDataError("sample covariance requires at least two vectors")
    dimensions = len(rows[0])
    if dimensions < 1 or any(len(row) != dimensions for row in rows):
        raise ResearchDataError("covariance vectors have inconsistent dimensions")
    means = [sum(float(row[index]) for row in rows) / len(rows) for index in range(dimensions)]
    covariance = [[0.0 for _ in range(dimensions)] for _ in range(dimensions)]
    for row in rows:
        centered = [float(row[index]) - means[index] for index in range(dimensions)]
        for i in range(dimensions):
            for j in range(i, dimensions):
                covariance[i][j] += centered[i] * centered[j]
    denominator = len(rows) - 1
    for i in range(dimensions):
        for j in range(i, dimensions):
            value = covariance[i][j] / denominator
            covariance[i][j] = value
            covariance[j][i] = value
    return covariance, means


def jacobi_eigenvalues(matrix: Sequence[Sequence[float]], relative_tolerance: float = 1e-13) -> list[float]:
    size = len(matrix)
    if size == 0 or any(len(row) != size for row in matrix):
        raise ResearchDataError("eigenvalue input must be a nonempty square matrix")
    values = [[float(matrix[i][j]) for j in range(size)] for i in range(size)]
    if any(not math.isfinite(value) for row in values for value in row):
        raise ResearchDataError("eigenvalue matrix contains nonfinite entries")
    if any(abs(values[i][j] - values[j][i]) > 1e-10 for i in range(size) for j in range(size)):
        raise ResearchDataError("eigenvalue matrix is not symmetric within tolerance")
    max_sweeps = 100 * size * size
    for _ in range(max_sweeps):
        p, q, off_diagonal = 0, 0, 0.0
        for i in range(size):
            for j in range(i + 1, size):
                if abs(values[i][j]) > off_diagonal:
                    p, q = i, j
                    off_diagonal = abs(values[i][j])
        scale = max(1.0, max(abs(values[i][i]) for i in range(size)))
        if off_diagonal <= relative_tolerance * scale:
            break
        app, aqq, apq = values[p][p], values[q][q], values[p][q]
        tau = (aqq - app) / (2.0 * apq)
        if tau == 0.0:
            tangent = 1.0
        else:
            tangent = math.copysign(1.0, tau) / (abs(tau) + math.sqrt(1.0 + tau * tau))
        cosine = 1.0 / math.sqrt(1.0 + tangent * tangent)
        sine = tangent * cosine
        for k in range(size):
            if k == p or k == q:
                continue
            akp, akq = values[k][p], values[k][q]
            values[k][p] = values[p][k] = cosine * akp - sine * akq
            values[k][q] = values[q][k] = sine * akp + cosine * akq
        values[p][p] = app - tangent * apq
        values[q][q] = aqq + tangent * apq
        values[p][q] = values[q][p] = 0.0
    else:
        raise ResearchDataError("symmetric Jacobi eigenvalue routine did not converge")
    eigenvalues = sorted(values[i][i] for i in range(size))
    if any(not math.isfinite(value) for value in eigenvalues):
        raise ResearchDataError("eigenvalue result is nonfinite")
    return eigenvalues


def rank_and_condition(eigenvalues: Sequence[float]) -> tuple[int, float | None]:
    if not eigenvalues:
        raise ResearchDataError("rank requires eigenvalues")
    largest = max(eigenvalues)
    if largest <= 0.0 or not math.isfinite(largest):
        return 0, None
    tolerance = largest * EIGEN_RELATIVE_TOLERANCE
    positive = [value for value in eigenvalues if value > tolerance]
    rank = len(positive)
    if rank != len(eigenvalues):
        return rank, None
    smallest = min(positive)
    return rank, largest / smallest


def shrink_covariance(covariance: Sequence[Sequence[float]], shrinkage: float) -> list[list[float]]:
    if not 0.0 < shrinkage <= 1.0:
        raise ResearchDataError("shrinkage must be in (0, 1]")
    size = len(covariance)
    if size == 0 or any(len(row) != size for row in covariance):
        raise ResearchDataError("covariance must be a nonempty square matrix")
    return [
        [((1.0 - shrinkage) * float(covariance[i][j])) + (shrinkage if i == j else 0.0) for j in range(size)]
        for i in range(size)
    ]


def cholesky(matrix: Sequence[Sequence[float]]) -> list[list[float]]:
    size = len(matrix)
    if size == 0 or any(len(row) != size for row in matrix):
        raise ResearchDataError("Cholesky input must be a nonempty square matrix")
    lower = [[0.0 for _ in range(size)] for _ in range(size)]
    for i in range(size):
        for j in range(i + 1):
            value = float(matrix[i][j]) - sum(lower[i][k] * lower[j][k] for k in range(j))
            if i == j:
                if not math.isfinite(value) or value <= 0.0:
                    raise ResearchDataError("covariance is not positive definite")
                lower[i][j] = math.sqrt(value)
            else:
                lower[i][j] = value / lower[j][j]
    return lower


def mahalanobis_squared(vector: Sequence[float], lower_cholesky: Sequence[Sequence[float]]) -> float:
    size = len(vector)
    if size == 0 or len(lower_cholesky) != size:
        raise ResearchDataError("score vector and factor dimensions differ")
    forward = [0.0] * size
    for i in range(size):
        forward[i] = (float(vector[i]) - sum(lower_cholesky[i][j] * forward[j] for j in range(i))) / lower_cholesky[i][i]
    score = sum(value * value for value in forward)
    if not math.isfinite(score) or score < -1e-10:
        raise ResearchDataError("joint score is nonfinite or negative")
    return max(0.0, score)


def independent_max_abs(vector: Sequence[float]) -> float:
    if not vector:
        raise ResearchDataError("independent score requires at least one signal")
    score = max(abs(float(value)) for value in vector)
    if not math.isfinite(score):
        raise ResearchDataError("independent score is nonfinite")
    return score


def nearest_rank_quantile(values: Sequence[float], quantile: float) -> float:
    if not values or not 0.0 < quantile <= 1.0:
        raise ResearchDataError("quantile requires values and q in (0, 1]")
    if any(not math.isfinite(float(value)) for value in values):
        raise ResearchDataError("quantile input contains a nonfinite value")
    ordered = sorted(float(value) for value in values)
    return ordered[math.ceil(quantile * len(ordered)) - 1]


def _rows_for_cycles(channels: dict[str, dict[int, array.array]], signals: Sequence[str], cycles: Sequence[int]) -> list[tuple[float, ...]]:
    return list(iter_vectors(channels, signals, cycles))


def _group_score_summary(scores: Sequence[float], threshold: float) -> dict:
    if not scores:
        raise ResearchDataError("cannot summarize an empty score group")
    ordered = sorted(float(value) for value in scores)
    exceedances = sum(value > threshold for value in ordered)
    return {
        "vectorRows": len(ordered),
        "referenceExceedanceCount": exceedances,
        "referenceExceedanceRate": exceedances / len(ordered),
        "scoreMean": sum(ordered) / len(ordered),
        "scoreMedian": median(ordered),
        "scoreP95": ordered[math.ceil(0.95 * len(ordered)) - 1],
        "scoreP99": ordered[math.ceil(0.99 * len(ordered)) - 1],
        "scoreMax": ordered[-1],
    }


def _score_groups(
    channels: dict[str, dict[int, array.array]],
    signals: Sequence[str],
    cycles: Sequence[int],
    centers: Sequence[float],
    scales: Sequence[float],
    score_function,
    threshold: float,
) -> dict[str, dict]:
    output: dict[str, dict] = {}
    for cycle in cycles:
        scores = []
        for vector in iter_vectors(channels, signals, (cycle,)):
            z = [(vector[index] - centers[index]) / scales[index] for index in range(len(signals))]
            scores.append(float(score_function(z)))
        output[str(cycle)] = _group_score_summary(scores, threshold)
    return output


def _configuration_digest(config: dict) -> str:
    unsigned = dict(config)
    unsigned.pop("configurationSha256", None)
    return sha256_json(unsigned)


def verify_configuration_digest(config: dict) -> None:
    expected = str(config.get("configurationSha256", "")).upper()
    actual = _configuration_digest(config)
    if not expected or actual != expected:
        raise ResearchDataError(f"frozen configuration digest mismatch: expected {expected}, got {actual}")


def default_holdout_guard_directory() -> Path:
    """Return stable per-user state outside the checkout and source-data folder."""
    if os.name == "nt":
        local_app_data = os.environ.get("LOCALAPPDATA")
        base = Path(local_app_data) if local_app_data else Path.home() / "AppData" / "Local"
        return base / "EliteSCADA" / "Research" / "Stage4F" / "holdout-claims"
    state_home = os.environ.get("XDG_STATE_HOME")
    base = Path(state_home) if state_home else Path.home() / ".local" / "state"
    return base / "elitescada" / "research" / "stage4f" / "holdout-claims"


def claim_final_holdout_once(
    configuration_sha256: str,
    frozen_config_commit: str,
    study_commit: str,
    claim_directory: Path | None = None,
    provenance: dict | None = None,
) -> Path:
    """Atomically consume a frozen-config digest before any source archive read.

    O_CREAT|O_EXCL is atomic between independent processes sharing this local
    state directory. A crash after file creation remains fail-closed: the marker
    is deliberately never reclaimed or deleted automatically.
    """
    digest = str(configuration_sha256).upper()
    if len(digest) != 64 or any(character not in "0123456789ABCDEF" for character in digest):
        raise ResearchDataError("holdout configuration digest must be a full SHA-256")
    for label, commit in (("frozen-config", frozen_config_commit), ("study", study_commit)):
        if len(commit) != 40 or any(character not in "0123456789abcdef" for character in commit.lower()):
            raise ResearchDataError(f"{label} commit must be a full 40-character SHA")

    directory = claim_directory if claim_directory is not None else default_holdout_guard_directory()
    directory.mkdir(parents=True, exist_ok=True)
    marker = directory / f"{digest}.claim.json"
    flags = os.O_CREAT | os.O_EXCL | os.O_WRONLY
    if hasattr(os, "O_BINARY"):
        flags |= os.O_BINARY
    try:
        descriptor = os.open(marker, flags, 0o600)
    except FileExistsError as exc:
        raise ResearchDataError(
            f"final holdout for frozen configuration {digest} was already claimed/consumed; refusing a second read"
        ) from exc
    record = {
        "guardVersion": 1,
        "configurationSha256": digest,
        "frozenConfigCommit": frozen_config_commit.lower(),
        "studyCommit": study_commit.lower(),
        "claimedAtUtc": datetime.now(timezone.utc).isoformat(),
        "state": "consumed-fail-closed",
        "provenance": provenance or {"kind": "runtime-claim-before-holdout-read"},
    }
    try:
        with os.fdopen(descriptor, "wb") as stream:
            payload = (json.dumps(record, sort_keys=True, allow_nan=False) + "\n").encode("utf-8")
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
    except (OSError, TypeError, ValueError) as exc:
        # Keep an incomplete exclusive marker as a fail-closed tombstone.
        raise ResearchDataError(f"holdout claim marker could not be persisted; digest remains consumed: {exc}") from exc
    return marker


def _matrix_diagnostics(rows: Sequence[Sequence[float]]) -> dict:
    covariance, means = covariance_matrix(rows)
    eigenvalues = jacobi_eigenvalues(covariance)
    rank, condition = rank_and_condition(eigenvalues)
    return {
        "covariance": covariance,
        "meanOfRobustScaledFit": means,
        "eigenvalues": eigenvalues,
        "rank": rank,
        "conditionNumber": condition,
    }


def _fit_dimension(
    channels: dict[str, dict[int, array.array]],
    dimension: int,
    split: dict[str, tuple[int, ...]],
) -> dict:
    signals = SIGNALS_BY_DIMENSION[dimension]
    fit_rows = _rows_for_cycles(channels, signals, split["fit"])
    centers, scales = fit_robust_scaling(fit_rows)
    fit_z = transform_rows(fit_rows, centers, scales)
    diagnostics = _matrix_diagnostics(fit_z)
    calibration_rows_by_cycle = {
        cycle: _rows_for_cycles(channels, signals, (cycle,)) for cycle in split["calibration"]
    }
    calibration_rows = [row for cycle in split["calibration"] for row in calibration_rows_by_cycle[cycle]]
    calibration_z = transform_rows(calibration_rows, centers, scales)

    prefix_diagnostics: list[dict] = []
    for group_count in range(1, len(split["fit"]) + 1):
        cycles = split["fit"][:group_count]
        prefix_rows = _rows_for_cycles(channels, signals, cycles)
        entry = {
            "sourceGroups": len(cycles),
            "cycleIds": list(cycles),
            "vectorRows": len(prefix_rows),
            "sampleToDimensionRatio": len(prefix_rows) / dimension,
        }
        try:
            prefix_centers, prefix_scales = fit_robust_scaling(prefix_rows)
            prefix_z = transform_rows(prefix_rows, prefix_centers, prefix_scales)
            prefix_matrix = _matrix_diagnostics(prefix_z)
            entry.update({key: prefix_matrix[key] for key in ("rank", "conditionNumber", "eigenvalues")})
            entry["status"] = "measured"
        except ResearchDataError as exc:
            entry.update({"rank": None, "conditionNumber": None, "eigenvalues": [], "status": f"degenerate: {exc}"})
        prefix_diagnostics.append(entry)

    joint_candidates: list[dict] = []
    for shrinkage in LAMBDA_GRID:
        regularized = shrink_covariance(diagnostics["covariance"], shrinkage)
        eigenvalues = jacobi_eigenvalues(regularized)
        rank, condition = rank_and_condition(eigenvalues)
        factor = cholesky(regularized)
        cal_scores = [mahalanobis_squared(row, factor) for row in calibration_z]
        threshold = nearest_rank_quantile(cal_scores, CALIBRATION_QUANTILE)
        per_cycle = {}
        offset = 0
        for cycle in split["calibration"]:
            cycle_scores = cal_scores[offset : offset + SAMPLES_PER_CYCLE]
            per_cycle[str(cycle)] = _group_score_summary(cycle_scores, threshold)
            offset += SAMPLES_PER_CYCLE
        joint_candidates.append({
            "shrinkage": shrinkage,
            "regularizedCovariance": regularized,
            "regularizedEigenvalues": eigenvalues,
            "regularizedRank": rank,
            "regularizedConditionNumber": condition,
            "calibrationThresholdQ99": threshold,
            "calibrationVectorRows": len(cal_scores),
            "calibrationCycleReferenceExceedances": per_cycle,
            "metricLabel": "calibration reference exceedance; not a false-positive rate",
        })

    independent_scores = [independent_max_abs(row) for row in calibration_z]
    independent_threshold = nearest_rank_quantile(independent_scores, CALIBRATION_QUANTILE)
    independent_per_cycle: dict[str, dict] = {}
    offset = 0
    for cycle in split["calibration"]:
        cycle_scores = independent_scores[offset : offset + SAMPLES_PER_CYCLE]
        independent_per_cycle[str(cycle)] = _group_score_summary(cycle_scores, independent_threshold)
        offset += SAMPLES_PER_CYCLE

    return {
        "signals": list(signals),
        "dimension": dimension,
        "fitSourceGroups": len(split["fit"]),
        "fitCycleIds": list(split["fit"]),
        "fitVectorRows": len(fit_rows),
        "fitRawSignalRows": len(fit_rows) * dimension,
        "fitSampleToDimensionRatio": len(fit_rows) / dimension,
        "robustCentersFitOnly": centers,
        "robustScalesFitOnly": scales,
        "unregularizedCovarianceDiagnostics": diagnostics,
        "sampleSizePrefixes": prefix_diagnostics,
        "calibrationSourceGroups": len(split["calibration"]),
        "calibrationCycleIds": list(split["calibration"]),
        "calibrationVectorRows": len(calibration_rows),
        "calibrationRawSignalRows": len(calibration_rows) * dimension,
        "jointCandidates": joint_candidates,
        "independentControl": {
            "scoreDefinition": "max absolute robust-scaled marginal signal score",
            "calibrationThresholdQ99": independent_threshold,
            "calibrationVectorRows": len(independent_scores),
            "calibrationCycleReferenceExceedances": independent_per_cycle,
            "metricLabel": "calibration reference exceedance; not a false-positive rate",
        },
    }


def build_frozen_configuration(
    archive_path: Path,
    source_manifest_path: Path,
    split_manifest_path: Path,
    protocol_path: Path,
    study_commit: str,
) -> dict:
    if not study_commit or len(study_commit) != 40 or any(character not in "0123456789abcdef" for character in study_commit.lower()):
        raise ResearchDataError("study commit must be a full 40-character SHA")
    manifest, archive_sha = verify_archive(archive_path, source_manifest_path)
    split = read_split_manifest(split_manifest_path)
    fit_calibration_cycles = split["fit"] + split["calibration"]

    # Important isolation: this phase requests only fit/calibration source rows.
    # read_profile_cycles/read_signal_cycles stop at cycle 1795; holdout matrix
    # rows 1796–1797 are not parsed or scored before the configuration commit.
    read_profile_cycles(archive_path, fit_calibration_cycles)
    channels = load_channels(archive_path, SIGNALS_BY_DIMENSION[7], fit_calibration_cycles)

    protocol_hash = sha256_file(protocol_path)
    split_hash = sha256_file(split_manifest_path)
    source_manifest_hash = sha256_file(source_manifest_path)
    dimensions = {
        str(dimension): _fit_dimension(channels, dimension, split)
        for dimension in (3, 5, 7)
    }
    config = {
        "configVersion": 1,
        "researchOnly": True,
        "studyCommit": study_commit,
        "protocolSha256": protocol_hash,
        "splitManifestSha256": split_hash,
        "sourceManifestSha256": source_manifest_hash,
        "archiveSha256": archive_sha,
        "sourceRecord": "UCI 447 / DOI 10.24432/C5CW21",
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "runtime": {
            "python": sys.version,
            "platform": platform.platform(),
            "processor": platform.processor(),
        },
        "fitCycles": list(split["fit"]),
        "calibrationCycles": list(split["calibration"]),
        "holdoutCycles": list(split["holdout"]),
        "featureLookbackPositions": 0,
        "purgeNote": "No rolling/lookback features; whole-cycle partitions are disjoint. Unknown inter-cycle dependence is not represented as a time purge.",
        "regularizationGrid": list(LAMBDA_GRID),
        "calibrationQuantile": CALIBRATION_QUANTILE,
        "quantileRule": "nearest-rank sorted[ceil(q*n)-1]",
        "candidateSelection": "No winner selected; carry the complete predeclared finite grid. Only two calibration source groups.",
        "finalMethodDecision": None,
        "dimensions": dimensions,
        "holdoutOpened": False,
        "scoreRowsWritten": False,
    }
    config["configurationSha256"] = _configuration_digest(config)
    return config


def write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    payload = json.dumps(value, sort_keys=True, indent=2, allow_nan=False) + "\n"
    temporary.write_text(payload, encoding="utf-8", newline="\n")
    os.replace(temporary, path)


def run_holdout(
    archive_path: Path,
    source_manifest_path: Path,
    split_manifest_path: Path,
    protocol_path: Path,
    config_path: Path,
    frozen_config_commit: str,
    claim_directory: Path | None = None,
) -> dict:
    try:
        config = json.loads(config_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ResearchDataError(f"frozen configuration cannot be read: {exc}") from exc
    verify_configuration_digest(config)
    if config.get("holdoutOpened") is not False:
        raise ResearchDataError("configuration does not prove that holdout was unopened at freeze")
    if len(frozen_config_commit) != 40 or any(character not in "0123456789abcdef" for character in frozen_config_commit.lower()):
        raise ResearchDataError("frozen configuration commit must be a full 40-character SHA")
    if sha256_file(protocol_path) != config.get("protocolSha256"):
        raise ResearchDataError("protocol changed after frozen configuration commit")
    if sha256_file(split_manifest_path) != config.get("splitManifestSha256"):
        raise ResearchDataError("split manifest changed after frozen configuration commit")

    split = read_split_manifest(split_manifest_path)
    if list(split["holdout"]) != config.get("holdoutCycles"):
        raise ResearchDataError("holdout cycle groups do not match frozen configuration")

    # Claim before hashing or parsing the source archive, so concurrent/repeated
    # invocations cannot both inspect final-holdout rows for the same config.
    claim_final_holdout_once(
        config["configurationSha256"],
        frozen_config_commit,
        config["studyCommit"],
        claim_directory,
    )

    manifest, archive_sha = verify_archive(archive_path, source_manifest_path)
    if archive_sha != config.get("archiveSha256"):
        raise ResearchDataError("holdout archive SHA does not match frozen configuration")

    # Holdout phase reads only holdout profile and matrix rows.
    read_profile_cycles(archive_path, split["holdout"])
    channels = load_channels(archive_path, SIGNALS_BY_DIMENSION[7], split["holdout"])
    dimension_results: dict[str, dict] = {}
    for dimension in (3, 5, 7):
        model = config["dimensions"][str(dimension)]
        signals = tuple(model["signals"])
        centers = model["robustCentersFitOnly"]
        scales = model["robustScalesFitOnly"]
        per_method: dict[str, dict] = {"jointCandidates": {}, "independentControl": {}}
        for candidate in model["jointCandidates"]:
            shrinkage = float(candidate["shrinkage"])
            covariance = model["unregularizedCovarianceDiagnostics"]["covariance"]
            regularized = shrink_covariance(covariance, shrinkage)
            factor = cholesky(regularized)
            threshold = float(candidate["calibrationThresholdQ99"])
            per_method["jointCandidates"][str(shrinkage)] = {
                "calibrationThresholdQ99": threshold,
                "holdoutReferenceExceedanceByCycle": _score_groups(
                    channels, signals, split["holdout"], centers, scales,
                    lambda z, factor=factor: mahalanobis_squared(z, factor), threshold,
                ),
            }
        control_threshold = float(model["independentControl"]["calibrationThresholdQ99"])
        per_method["independentControl"] = {
            "scoreDefinition": model["independentControl"]["scoreDefinition"],
            "calibrationThresholdQ99": control_threshold,
            "holdoutReferenceExceedanceByCycle": _score_groups(
                channels, signals, split["holdout"], centers, scales,
                independent_max_abs, control_threshold,
            ),
        }
        dimension_results[str(dimension)] = {
            "signals": list(signals),
            "sourceGroups": len(split["holdout"]),
            "holdoutCycleIds": list(split["holdout"]),
            "vectorRows": len(split["holdout"]) * SAMPLES_PER_CYCLE,
            "rawSignalRows": len(split["holdout"]) * SAMPLES_PER_CYCLE * dimension,
            "methods": per_method,
        }

    return {
        "reportVersion": 1,
        "assignment": "#662",
        "status": "offline reference-exceedance study; not product acceptance",
        "frozenConfigSha256": config["configurationSha256"],
        "frozenConfigCommit": frozen_config_commit,
        "studyCommit": config["studyCommit"],
        "archiveSha256": archive_sha,
        "sourceRecord": "UCI 447 / DOI 10.24432/C5CW21",
        "holdoutCycles": list(split["holdout"]),
        "holdoutOpenedAfterFreeze": True,
        "metrics": {
            "pointFalsePositiveRate": "not_measured: no independent known-healthy point labels",
            "falseEpisodesPerOperatingHour": "not_measured: no canonical source-time/exposure",
            "eventSensitivityAndLatency": "not_measured: no natural labels or qualifying contiguous event windows",
            "confidenceIntervals": "not_measured: only two holdout cycle groups; independence is not established",
            "canonicalGoodSynchronizedCohort": "ineligible: source lacks UTC source timestamps and explicit quality/synchronization status",
            "acceptance": "INSUFFICIENT EVIDENCE",
        },
        "dimensions": dimension_results,
    }


def peak_rss_bytes() -> int:
    if sys.platform == "win32":
        import ctypes
        from ctypes import wintypes

        class ProcessMemoryCountersEx(ctypes.Structure):
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
                ("PrivateUsage", ctypes.c_size_t),
            ]

        counters = ProcessMemoryCountersEx()
        counters.cb = ctypes.sizeof(counters)
        psapi = ctypes.WinDLL("psapi", use_last_error=True)
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        get_current_process = kernel32.GetCurrentProcess
        get_current_process.argtypes = []
        get_current_process.restype = wintypes.HANDLE
        get_process_memory_info = psapi.GetProcessMemoryInfo
        get_process_memory_info.argtypes = [
            wintypes.HANDLE,
            ctypes.POINTER(ProcessMemoryCountersEx),
            wintypes.DWORD,
        ]
        get_process_memory_info.restype = wintypes.BOOL
        ok = get_process_memory_info(get_current_process(), ctypes.byref(counters), counters.cb)
        if not ok:
            raise OSError(ctypes.get_last_error(), "GetProcessMemoryInfo failed")
        return int(counters.PeakWorkingSetSize)
    if sys.platform.startswith("linux"):
        if resource is None:
            raise ResearchDataError("Linux peak RSS support requires the resource module")
        return int(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss * 1024)
    raise ResearchDataError(f"peak RSS measurement is unsupported on {sys.platform}")
