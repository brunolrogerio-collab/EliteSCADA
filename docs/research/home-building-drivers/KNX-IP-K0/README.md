# KNX K0 runtime freeze evidence

**Evidence date:** 2026-10-10 BRT<br>
**Owning issue / PR:** [#577](https://github.com/brunolrogerio-collab/EliteSCADA/issues/577) / [#580](https://github.com/brunolrogerio-collab/EliteSCADA/pull/580)<br>
**Current integration observed:** `wave15/corrections-integration@ff567102bef09fba8ab26a6b13db678b0a85b842`<br>
**Main decision:** [#577 comment 6097438437](https://github.com/brunolrogerio-collab/EliteSCADA/issues/577#issuecomment-6097438437)<br>
**Lane state:** `STACK_CANDIDATE_SELECTED / K0_LICENSE_RUNTIME_FREEZE_ACTIVE / NO_PRODUCT_IMPLEMENTATION_RELEASE / NO_MERGE`

This bundle is research evidence only. Wheels were downloaded and installed in disposable containers and scratch work; no product source, package, dependency, test, workflow, schema, Runtime, or registration was changed.

## Frozen candidate runtime

The selected production-sidecar *candidate* is XKNX 3.20.0 from upstream commit `e68c024e561dbc486c55dc15d401d070250bfba5`. The qualification target is CPython 3.12.15 on Linux x64 in the Docker Official Image `python:3.12.15-slim-trixie`.

| Component | Frozen value |
| --- | --- |
| Official image index digest | `sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1` |
| Linux/amd64 manifest digest | `sha256:2b4f19dae3a777dfc3b76730bda1e82e1f66ab2a2686fa93ca78edbfb4f04ffe` |
| Docker Official Images source revision | `2a3b794c223ab067d122719541cdd54a068732a5` |
| Image created | `2026-10-06T01:55:50Z` |
| Runtime | CPython 3.12.15, Debian GNU/Linux 13.7 (Trixie), `linux/amd64` |
| Image-bundled installer | pip 25.0.1 |
| XKNX runtime closure | 5 exact wheels; hash lock: [`xknx-runtime-cp312-linux-amd64.lock`](xknx-runtime-cp312-linux-amd64.lock) |
| Audit tool | pip-audit 2.10.1 with 29 exact packages, separately locked in [`pip-audit-tool-cp312-linux-amd64.lock`](pip-audit-tool-cp312-linux-amd64.lock); this is not an XKNX runtime dependency |

The package license texts are in [`xknx-runtime-wheel-notices.tar.gz`](xknx-runtime-wheel-notices.tar.gz). Full Debian package copyright notices, common licenses, the CPython license, pip license, and pip vendor manifest are in [`python-trixie-base-notices.tar.gz`](python-trixie-base-notices.tar.gz). The exact Debian package inventory is [`python-trixie-base-packages.tsv`](python-trixie-base-packages.tsv). The audit tool's notices are in [`pip-audit-tool-notices.tar.gz`](pip-audit-tool-notices.tar.gz). Artifact checksums are in [`SHA256SUMS.txt`](SHA256SUMS.txt).

The exact five PyPI distributions are XKNX 3.20.0 (MIT), cryptography 50.0.2 (Apache-2.0 OR BSD-3-Clause), ifaddr 0.2.0 (MIT), cffi 2.1.1 (MIT), and pycparser 3.11 (BSD-3-Clause). No license fee or proprietary term was identified in this Python wheel closure. The full texts, including both cryptography license options, are preserved in the notice archive. The `cryptography` wheel reports statically linked OpenSSL 4.0.3; the CFFI extension's Linux linkage inspection showed libc and pthread, with no dynamic libffi dependency. Binary wheels were available for this exact CPython/Linux target, so no compiler or Rust toolchain was required for this resolution/install.

The complete OCI image is broader than those five wheels. Docker Scout indexed 127 packages from the image; dpkg recorded the installed Debian package set. The image's complete distribution license/notice acceptance remains with Main. This evidence bundle is not a legal opinion.

## Reproduction commands

Run from a checkout root. These commands use only the exact image digest and the published locks; all install steps stay in disposable containers.

```sh
docker pull --platform linux/amd64 python:3.12.15-slim-trixie@sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1

docker run --rm --platform linux/amd64 \
  -v "$PWD/docs/research/home-building-drivers/KNX-IP-K0:/evidence" \
  python:3.12.15-slim-trixie@sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1 \
  sh -lc 'mkdir -p /tmp/wheels && python -m pip download --only-binary=:all: --require-hashes -r /evidence/xknx-runtime-cp312-linux-amd64.lock -d /tmp/wheels && python -m pip install --no-index --find-links=/tmp/wheels --require-hashes --target=/tmp/xknx -r /evidence/xknx-runtime-cp312-linux-amd64.lock && PYTHONPATH=/tmp/xknx python -c "from cryptography.hazmat.backends.openssl.backend import backend; print(backend.openssl_version_text())"'
```

The recorded dependency audit used pip-audit 2.10.1 from the pinned audit-tool closure, then audited all five exact runtime requirements with hashes:

```sh
PYTHONPATH=/tmp/k0-pip-audit python -m pip_audit \
  --requirement xknx-runtime-cp312-linux-amd64.lock \
  --require-hashes --no-deps --progress-spinner off --format json
```

The image scan used Docker Scout CLI 1.24.0:

```sh
docker scout cves --details --format packages \
  local://python:3.12.15-slim-trixie
```

The full scanner output is preserved as [`docker-scout-python-trixie.txt`](docker-scout-python-trixie.txt). The run reported zero critical, one high, six medium and 27 low findings across 14 packages with findings (127 packages indexed). Debian's security tracker independently marks the Trixie `zlib` package vulnerable for [CVE-2026-85091](https://security-tracker.debian.org/tracker/CVE-2026-85091), with no fixed version recorded at audit time. Docker Scout also flags the image's pip 25.0.1 installer for known advisories; the full CVE list and fixed versions appear in its report.

## Synthetic runtime/keyring/configuration smoke

The smoke ran in the pinned Debian Trixie image after installing only the five wheels from their hash lock. It created an in-memory XKNX `Keyring` with synthetic group `1/2/3`, sender `1.1.1`, receiver `1.1.10`, and a dummy 16-byte key; it confirmed XKNX returns that key/sender association. It constructed `SecureConfig` with synthetic credentials and the in-memory keyring, then constructed `ConnectionConfig` for `TUNNELING_TCP_SECURE` at documentation-only address `192.0.2.10:3671`, and constructed `XKNX` without calling `start()`.

A separate synthetic preflight assertion rejected a missing gateway before constructing XKNX. That assertion demonstrates the intended adapter guard only; it is not an EliteSCADA adapter or product test. No socket, gateway, packet, customer key, or physical device was used. The concise result is in [`synthetic-linux-smoke.txt`](synthetic-linux-smoke.txt).

## K0 scope and limits

- Main selected KNXUltimate 6.0.8 as a **test-only independent peer candidate**, not a shipped dependency. Its packets still need independent software L2 review in the later implementation-acceptance work.
- The smoke confirms library imports and in-memory configuration shape only. It does not prove Secure tunneling handshake, Data Secure validation, fail-closed behavior, replay/tamper rejection, Protected Material resolver integration, a product sidecar lifecycle, L0–L3 acceptance, or physical compatibility.
- Main's frozen boundary remains explicit Secure TCP tunneling, no automatic discovery or secure-to-plain retry, existing host Protected Material authority/resolver, and the first stateful DPT slice `1.001`, `1.002`, `5.001`, `9.001`, `9.004`, `9.007`, `9.024`.
- L0–L3 are later product implementation acceptance work. L4 remains deferred until after Wave 16, partner disclosure, and stable installation. No physical compatibility claim is made.
- .NET SDK 10.0.401 was already present from the prior lane; K0 uses a Python sidecar candidate and needed no SDK install or .NET compile.
- Overall recommendation remains `WAIT / FUTURE_ONLY` pending acceptance of this exact distributable image: the image security RED and Main's complete-stack distribution review remain open. This is specific to acceptance of the exact distributable image; XKNX remains the selected candidate and the Main-defined K0 continuation is complete.
