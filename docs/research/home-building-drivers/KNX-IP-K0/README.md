# KNX K0 runtime freeze evidence

**Evidence date:** 2026-10-12 BRT<br>
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

The exact five PyPI distributions are XKNX 3.20.0 (MIT), cryptography 50.0.2 (Apache-2.0 OR BSD-3-Clause), ifaddr 0.2.0 (MIT), cffi 2.1.1 (MIT), and pycparser 3.11 (BSD-3-Clause). No license fee or proprietary term was identified in this Python wheel closure. The full texts, including both cryptography license options, are preserved in the notice archive. The `cryptography` wheel reports statically linked OpenSSL 4.0.3; the CFFI extension's Linux linkage inspection showed libc and pthread, with no dynamic libffi dependency. Binary wheels were available for this exact CPython/Linux target, so no compiler or Rust toolchain was required for this resolution/install. The exact lock was re-audited on 2026-10-12; [`pip-audit-runtime-2026-10-12.json`](pip-audit-runtime-2026-10-12.json) reports no known vulnerabilities in the five PyPI distributions.

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

The image scan used Docker Scout CLI 1.24.0. The exact official Trixie digest was re-confirmed on 2026-10-12 and re-scanned; the refreshed full output is [`docker-scout-python-trixie-2026-10-12.txt`](docker-scout-python-trixie-2026-10-12.txt): 0 critical, 1 high, 6 medium and 27 low findings across 14 packages with findings (127 indexed). Debian still lists Trixie zlib as vulnerable, without a fixed version. The image's pip 25.0.1 installer also has advisories. The older scan remains preserved separately.

```sh
docker scout cves --details --format packages \
  local://python:3.12.15-slim-trixie
```

The original full scanner output is preserved as [`docker-scout-python-trixie.txt`](docker-scout-python-trixie.txt). Debian's security tracker independently marks Trixie's zlib vulnerable for [CVE-2026-85091](https://security-tracker.debian.org/tracker/CVE-2026-85091), still without a fixed Trixie version. Docker Scout also flags the image's pip 25.0.1 installer; the CVE list and fixed versions appear in each report.

## Bounded official Alpine runtime screen (2026-10-12)

To check whether the zlib High was specific to the selected Debian image, one bounded official Linux/amd64 alternative was screened: Docker Official `python:3.12.15-alpine3.24`, index digest `sha256:1b668429b3511ab407d8e00648891631b0b1a4d7e15e3ca70f38ab5b91ad4ab4`, amd64 manifest `sha256:2055081c860db8c842b663415b795b026e7e5cc63b59c5193fcc9997713ea5b8`, Alpine `3.24.2`, CPython `3.12.15`.

The unmodified official image scan is [`docker-scout-python-alpine3.24-base.txt`](docker-scout-python-alpine3.24-base.txt): 52 packages, **0 critical, 1 high, 5 medium, 1 low**. The High is zlib `1.3.2-r0`, CVE-2026-85091. In a disposable container based on that exact manifest, `apk update && apk add --no-cache --upgrade zlib` installed `zlib-1.3.2-r1` from the configured official Alpine 3.24 repositories. The committed research-only local image had digest `sha256:25053641cab7b5ce7b0f3812bcd70f87581e5ba112556d5b99459ff2df6e69bd`; its scan is [`docker-scout-python-alpine3.24-zlib-r1.txt`](docker-scout-python-alpine3.24-zlib-r1.txt): 52 packages, **0 critical, 0 high, 5 medium, 1 low**. All remaining findings belong to image-bundled pip 25.0.1. The CVE text and Alpine package's current version are available from [Debian's CVE record](https://security-tracker.debian.org/tracker/CVE-2026-85091) and the [Alpine v3.24 zlib catalog](https://pkgs.alpinelinux.org/package/v3.24/main/x86_64/zlib); the exact installed package was also checked with `apk list -I zlib` in the pinned image.

All five pinned Python distributions resolved as binary wheels for this Alpine/musl platform, with their filenames and SHA-256 hashes in [`alpine-runtime-wheel-resolution.txt`](alpine-runtime-wheel-resolution.txt). This was a download/platform-availability check only; the wheels were not installed or smoke-tested under Alpine. No complete Alpine OS/Python/pip package inventory or license/notice archive was produced, and the locally updated overlay is not a released image. Thus Alpine is a possible future base to take to Main's review, not a cleared replacement for the Trixie candidate.

Reproduction commands (the package update stays in the disposable research container; do not treat the local overlay as a product image):

```sh
docker scout cves --platform linux/amd64 --details --format packages \
  local://python:3.12.15-alpine3.24@sha256:2055081c860db8c842b663415b795b026e7e5cc63b59c5193fcc9997713ea5b8

docker run --rm --platform linux/amd64 \
  python:3.12.15-alpine3.24@sha256:2055081c860db8c842b663415b795b026e7e5cc63b59c5193fcc9997713ea5b8 \
  sh -lc 'apk update && apk add --no-cache --upgrade zlib && apk list -I zlib'
```

## Synthetic runtime/keyring/configuration smoke

The smoke ran in the pinned Debian Trixie image after installing only the five wheels from their hash lock. It created an in-memory XKNX `Keyring` with synthetic group `1/2/3`, sender `1.1.1`, receiver `1.1.10`, and a dummy 16-byte key; it confirmed XKNX returns that key/sender association. It constructed `SecureConfig` with synthetic credentials and the in-memory keyring, then constructed `ConnectionConfig` for `TUNNELING_TCP_SECURE` at documentation-only address `192.0.2.10:3671`, and constructed `XKNX` without calling `start()`.

A separate synthetic preflight assertion rejected a missing gateway before constructing XKNX. That assertion demonstrates the intended adapter guard only; it is not an EliteSCADA adapter or product test. No socket, gateway, packet, customer key, or physical device was used. The concise result is in [`synthetic-linux-smoke.txt`](synthetic-linux-smoke.txt).

## K0 scope and limits

- Main selected KNXUltimate 6.0.8 as a **test-only independent peer candidate**, not a shipped dependency. Its packets still need independent software L2 review in the later implementation-acceptance work.
- The smoke confirms library imports and in-memory configuration shape only. It does not prove Secure tunneling handshake, Data Secure validation, fail-closed behavior, replay/tamper rejection, Protected Material resolver integration, a product sidecar lifecycle, L0–L3 acceptance, or physical compatibility.
- Main's frozen boundary remains explicit Secure TCP tunneling, no automatic discovery or secure-to-plain retry, existing host Protected Material authority/resolver, and the first stateful DPT slice `1.001`, `1.002`, `5.001`, `9.001`, `9.004`, `9.007`, `9.024`.
- L0–L3 are later product implementation acceptance work. L4 remains deferred until after Wave 16, partner disclosure, and stable installation. No physical compatibility claim is made.
- .NET SDK 10.0.401 was already present from the prior lane; K0 uses a Python sidecar candidate and needed no SDK install or .NET compile.
- Overall recommendation remains `WAIT / FUTURE_ONLY`: the selected Trixie image still carries a High zlib finding, while the Alpine screen leaves pip advisories and has no complete distribution/notice closure. Main must select and accept an exact runtime image and its complete notices. The five-wheel license subgate is `GO`; this is not an implementation release. The selected XKNX candidate, existing Protected Material path, Secure contract and test-only KNXUltimate peer remain as Main specified. No product behavior or physical compatibility was tested.
