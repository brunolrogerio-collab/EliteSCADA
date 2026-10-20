"""CLI for the Stage 4F offline research experiment; never calls product APIs."""

from __future__ import annotations

import argparse
import json
import platform
import sys
import time
import zipfile
from pathlib import Path

from research_harness import (
    ResearchDataError,
    build_frozen_configuration,
    peak_rss_bytes,
    run_holdout,
    write_json,
)


HERE = Path(__file__).resolve().parent


def runtime_info() -> dict:
    return {
        "python": sys.version,
        "platform": platform.platform(),
        "processor": platform.processor(),
    }


def run_fit_calibrate(args: argparse.Namespace) -> int:
    started = time.perf_counter()
    config = build_frozen_configuration(
        Path(args.archive),
        Path(args.source_manifest),
        Path(args.split_manifest),
        Path(args.protocol),
        args.study_commit,
    )
    config["fitCalibrationExecution"] = {
        "elapsedWallSeconds": time.perf_counter() - started,
        "peakResidentBytes": peak_rss_bytes(),
        "runtime": runtime_info(),
    }
    config["configurationSha256"] = ""
    # Refresh digest after attaching execution metadata; no score records are retained.
    from research_harness import _configuration_digest
    config["configurationSha256"] = _configuration_digest(config)
    write_json(Path(args.output), config)
    print(json.dumps({
        "status": "FIT_CALIBRATION_COMPLETE_HOLDOUT_UNREAD",
        "output": str(Path(args.output)),
        "configurationSha256": config["configurationSha256"],
        "fitCalibrationExecution": config["fitCalibrationExecution"],
    }, sort_keys=True, indent=2))
    return 0


def run_final_holdout(args: argparse.Namespace) -> int:
    started = time.perf_counter()
    report = run_holdout(
        Path(args.archive),
        Path(args.source_manifest),
        Path(args.split_manifest),
        Path(args.protocol),
        Path(args.config),
        args.frozen_config_commit,
    )
    report["execution"] = {
        "elapsedWallSeconds": time.perf_counter() - started,
        "peakResidentBytes": peak_rss_bytes(),
        "runtime": runtime_info(),
    }
    if args.output:
        write_json(Path(args.output), report)
    print(json.dumps(report, sort_keys=True, indent=2, allow_nan=False))
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)

    fit = commands.add_parser("fit-calibrate", help="fit only assigned fit groups and calibrate; never reads holdout rows")
    fit.add_argument("--archive", required=True)
    fit.add_argument("--source-manifest", default=str(HERE / "source-manifest.json"))
    fit.add_argument("--split-manifest", default=str(HERE / "cycle-split-manifest.csv"))
    fit.add_argument("--protocol", default=str(HERE / "protocol-v1.md"))
    fit.add_argument("--study-commit", required=True, help="full commit SHA containing the reviewed harness")
    fit.add_argument("--output", required=True, help="frozen config output path; holdout remains unopened")
    fit.set_defaults(handler=run_fit_calibrate)

    holdout = commands.add_parser("holdout", help="score the locked holdout once using a committed frozen config")
    holdout.add_argument("--archive", required=True)
    holdout.add_argument("--source-manifest", default=str(HERE / "source-manifest.json"))
    holdout.add_argument("--split-manifest", default=str(HERE / "cycle-split-manifest.csv"))
    holdout.add_argument("--protocol", default=str(HERE / "protocol-v1.md"))
    holdout.add_argument("--config", required=True)
    holdout.add_argument("--frozen-config-commit", required=True, help="full SHA of the commit that froze this config")
    holdout.add_argument("--output", help="optional aggregate-only JSON summary path; no record-level scores")
    holdout.set_defaults(handler=run_final_holdout)
    return parser


def main() -> int:
    args = build_parser().parse_args()
    try:
        return args.handler(args)
    except ResearchDataError as exc:
        print(f"research input/method error: {exc}", file=sys.stderr)
        return 2
    except (OSError, ValueError, KeyError, zipfile.BadZipFile) as exc:
        print(f"research execution failed: {exc}", file=sys.stderr)
        return 3


if __name__ == "__main__":
    raise SystemExit(main())
