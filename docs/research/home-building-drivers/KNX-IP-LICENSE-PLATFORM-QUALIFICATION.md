# KNX/IP licensing and platform qualification

**Lane:** #577 / `DEV-HOME-KNX-IP-QUALIFICATION`<br>
**Current recommendation:** `WAIT / FUTURE_ONLY` pending acceptance of the exact distributable runtime image<br>
**Main state:** `STACK_CANDIDATE_SELECTED / K0_LICENSE_RUNTIME_FREEZE_ACTIVE / NO_PRODUCT_IMPLEMENTATION_RELEASE / NO_MERGE`<br>
**Evidence date:** 2026-10-12 BRT<br>
**Current integration observed:** `wave15/corrections-integration@ff567102bef09fba8ab26a6b13db678b0a85b842`<br>
**Scope:** documentation and issue evidence only. No product code, dependency, test, schema, workflow, Runtime, or registration change. No merge.

## Current Main disposition and K0 result

Main selected **XKNX 3.20.0** from upstream commit `e68c024e561dbc486c55dc15d401d070250bfba5` as the production-sidecar candidate. Main's [stack/security/DPT decision](https://github.com/brunolrogerio-collab/EliteSCADA/issues/577#issuecomment-6097438437) supersedes the earlier blanket `WAIT / FUTURE_ONLY` assessment. It selects a candidate; it does not release product implementation or accept an unclosed distribution image.

**Candidate license gate:** `GO` for the exact five-package Python wheel closure below: all have permissive license expressions; the five distributions have no license fee or proprietary term, and all hashes and full wheel notices are recorded. Runtime hosting/support costs were not priced in K0. **Overall recommendation:** `WAIT / FUTURE_ONLY` because the selected official base image currently has a High zlib advisory and known pip installer advisories, and Main retains final acceptance of the complete distribution/license closure.

| K0 area | Evidence and result |
| --- | --- |
| Runtime / platform | Frozen to Docker Official Image `python:3.12.15-slim-trixie`, CPython 3.12.15, Debian 13.7, Linux/amd64. Image index digest: `sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1`; amd64 manifest: `sha256:2b4f19dae3a777dfc3b76730bda1e82e1f66ab2a2686fa93ca78edbfb4f04ffe`. Exact OS package inventory and full notice archive are attached in the [K0 evidence bundle](KNX-IP-K0/README.md). |
| Python dependency closure | Five wheels were re-resolved on that exact runtime, downloaded as binaries, hash-locked, and installed offline with hash enforcement. No compiler or Rust build tool was needed. The complete lock and exact SHA-256 values are in [`KNX-IP-K0`](KNX-IP-K0/). |
| Python advisories | `pip-audit 2.10.1` reran against the exact five locked runtime distributions on 2026-10-12: **0 known vulnerabilities**. It does not cover OS/base-image packages or the image's pip installer. The new JSON result and separately locked audit-tool closure are in the bundle. |
| Base-image advisories | The official Trixie tag still resolves to index `sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1` and amd64 manifest `sha256:2b4f19dae3a777dfc3b76730bda1e82e1f66ab2a2686fa93ca78edbfb4f04ffe`. Docker Scout 1.24.0 reran on 2026-10-12: 127 packages, 14 with findings, **0 critical, 1 high, 6 medium, 27 low**. High remains Debian Trixie's `zlib 1:1.3.dfsg+really1.3.1-1`, CVE-2026-85091; Debian's tracker still lists it vulnerable without a fixed Trixie version. pip 25.0.1 also has installer advisories. See [`docker-scout-python-trixie-2026-10-12.txt`](KNX-IP-K0/docker-scout-python-trixie-2026-10-12.txt) and [Debian's CVE record](https://security-tracker.debian.org/tracker/CVE-2026-85091). |
| Bounded Alpine comparison | Official Python 3.12.15 Alpine 3.24.2 amd64 image (`sha256:2055081c860db8c842b663415b795b026e7e5cc63b59c5193fcc9997713ea5b8`) scanned at **0C/1H/5M/1L**; the High is zlib `1.3.2-r0`. In a disposable research container, official `apk update`/`apk add --upgrade zlib` installed `1.3.2-r1`; Scout on the resulting local image `sha256:25053641cab7b5ce7b0f3812bcd70f87581e5ba112556d5b99459ff2df6e69bd` found **0C/0H/5M/1L**, all six findings in bundled pip 25.0.1. All five selected wheel versions also resolve to Linux/musllinux binary wheels; filenames and hashes are recorded in [`alpine-runtime-wheel-resolution.txt`](KNX-IP-K0/alpine-runtime-wheel-resolution.txt). This is a screened alternative only: its complete OS/runtime license-notice closure was not inventoried or accepted, and this wheel availability check is not an install/smoke acceptance. |
| Secure/keyring smoke | Synthetic Linux/module/keyring/configuration smoke **passed**. XKNX accepted an in-memory keyring and explicit `TUNNELING_TCP_SECURE` configuration. A scratch preflight assertion rejected a missing gateway before constructing XKNX. XKNX was never started; no socket or physical device was used. This is not Secure packet, fail-closed, or EliteSCADA adapter evidence. |
| .NET 10 | This candidate is a Python sidecar, not a .NET library. The host remains .NET 10 under Main's contract. SDK 10.0.401 was already present from the previous lane; K0 did not install an SDK or compile product code. |
| Protected Material / peer | Main has frozen use of the existing host authority/resolver and the exact purposes. The smoke used synthetic in-memory data only; no authority/resolver binding was implemented. KNXUltimate 6.0.8 is the **test-only independent peer candidate**, not a shipped dependency; packet-level L2 evidence remains future implementation acceptance. |

The official Python image's complete system/package license and notice texts were captured, including Debian package copyright files, CPython and pip notices, and pip's vendor manifest. Main retains final redistribution acceptance for that complete image bundle. This qualification does not assert that the image is cleared for shipment.

## Current RED register

| ID | Current finding | Closure needed |
| --- | --- | --- |
| `RED-KNX-07 / runtime image security` | The selected Trixie digest still has High `zlib` CVE-2026-85091 and pip 25.0.1 advisories. A bounded Alpine 3.24.2 screen showed an official zlib update to `1.3.2-r1` removes the High in the scanned overlay, but leaves 5 Medium/1 Low in pip; no Alpine distribution closure was prepared. A clean pip-audit result for the five PyPI wheels does not clear either OCI image. | Main selects an exact distributable Linux runtime/overlay, determines whether pip belongs in the final runtime, closes its full package/license/notice inventory, and repeats the image scan. Keep wheel and base-image results separate. |
| `RED-KNX-08 / complete image distribution acceptance` | XKNX and its five locked Python distributions are permissively licensed and their full texts are bundled. The exact image also carries the Debian/Python/pip package set; full notices and inventory are preserved, but Main's final distribution/license acceptance is pending. | Main reviews the exact package inventory and notices against the Product Owner policy and accepts or replaces the base image. No paid SDK is required for the selected route. |
| `RED-KNX-03 / independent software L2` | Main selected KNXUltimate 6.0.8 as a test-only peer, but no emitted-packet comparison or independent L2 run exists yet. | Later implementation acceptance covers secure tunnel handshake, no downgrade, key/scope failures, tamper/replay/plain injection, read/write/status, reconnect, and process loss. No physical gateway is required for software L2. |
| `RED-KNX-05 / Secure fail-closed` | K0 confirmed configuration construction only; it did not test network packets, tamper/replay rejection, or secure-to-plain behavior. | Prove the Main contract in the later authorized product implementation lane. Explicit Secure TCP only; no automatic selection, discovery, routing, or fallback. |
| `RED-KNX-04 / Protected Material integration` | Main has now frozen the existing `IProtectedMaterialAuthority` and `ICommunicationDriverProtectedMaterialResolver` contract, but K0 did not implement the trusted adapter or host binding. | Main retains adapter/DI and canonical registration ownership. A later implementation lane proves scoped references, least-privilege leases, rotation/re-resolution, and redaction. |

No RED is an Actions failure. The previous docs-only T1 passed on its then-current exact SHA; this updated documentation candidate still needs its own exact-head `DOCS_I18N_HELP` T1. No broad CI is requested.

## Main's frozen future boundary (specified, not implemented)

- One managed local Python sidecar under the existing #543 supervisor/lifecycle. Canonical .NET Runtime/Data Source/TAG remains authoritative; no HAB dependency, Python service owner, second Runtime, discovery authority, or leader election.
- Explicitly configured `TUNNELING_TCP_SECURE` with validated gateway/port, tunnel user/individual address, and credentials. A missing gateway must fail before XKNX starts because its selector otherwise reaches automatic selection. No `AUTOMATIC`, routing, gateway scan, or secure-to-plain retry.
- Data Secure group keys are scoped in-memory with allowed senders. Wrong/missing keys, tampered/authentication-failed/replayed/plain telegrams fail closed. Plain operation requires explicit legacy-plain group configuration, never inference from missing keys or Secure failure.
- Reuse the existing authority/resolver. Opaque references bind project, canonical stable Data Source, driver, and purpose; allowed purposes are `knx.ip-secure.user-password`, `knx.ip-secure.device-authentication`, and `knx.data-secure.keyring`. No new secret store, raw material in Engineering/packages/source/argv/logs/diagnostics, or credential files.
- First stateful DPT slice: `1.001`, `1.002`, `5.001`, `9.001`, `9.004`, `9.007`, `9.024`. Write GA and authoritative status/readback GA are distinct. The broader #539 list is later expansion; transient dimming/blinds/scenes stay out of this state slice.
- L0–L3 are acceptance work for a subsequent implementation release, not a prerequisite to starting that authorized implementation lane. Main still owns final stack acceptance, the trusted host adapter, canonical registration/shared CI, and the implementation release.
- L4 physical testing is deferred until after Wave 16, partner disclosure, and stable installation. No physical compatibility claim is made.

## Historical disposition from 2026-10-09

The previous report said `WAIT / FUTURE_ONLY` for every candidate because no complete license/runtime/security/L2 package had been selected. That was a correct savepoint before Main's stack/security decision, and is retained here as historical context. It is superseded as the current cross-candidate disposition by Main's XKNX selection. Candidate-specific Falcon redistribution/Secure/security holds and Calimero distribution review remain candidate-specific; they do not block this selected permissive route.

- Falcon Public SDK 6.4.8671 had no fee but conditional redistribution wording not cleared for server/container delivery. The isolated `net10.0` restore resolved `System.Security.Cryptography.Pkcs 7.0.0` with `NU1903` High (CVE-2023-29331; advisory lists 7.0.2 as patched). Falcon is not a production dependency in this stage.
- Calimero v3.0-M2 is GPL-2 with Classpath Exception and JDK 21; product distribution review remains. It is not a production dependency in this stage.
- KNXUltimate v6.0.8's exact lock inventory found 55 runtime entries (53 MIT, 2 ISC, 0 unknown). It remains the test-only peer candidate; Node/native deployment and strict Secure behavior are not product acceptance evidence.
- `knx-dotnet` and `knust` remain `REJECT` for the current Tier A contract based on the earlier release/Secure evidence.

Historical Falcon, Calimero, KNXUltimate, and alternative source details remain in the [dependency/license inventory](KNX-IP-DEPENDENCY-LICENSE-INVENTORY.md). The Product Owner policy still excludes paid or restrictive driver licenses; unresolved license or distribution rights remain `WAIT`.

## Sources and reproducibility

The [K0 evidence bundle](KNX-IP-K0/README.md) records the exact locks, notices, artifact hashes, OS package inventory, smoke scope, audit outcomes, and commands. Primary decision and source references:

- [Main K0 selection and security boundary](https://github.com/brunolrogerio-collab/EliteSCADA/issues/577#issuecomment-6097438437), [coordination issue #305](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305)
- [XKNX 3.20.0 tag/manifest/license](https://github.com/XKNX/xknx/tree/e68c024e561dbc486c55dc15d401d070250bfba5), [`SecureConfig` and connection selector](https://github.com/XKNX/xknx/blob/e68c024e561dbc486c55dc15d401d070250bfba5/xknx/io/connection.py), [in-memory keyring](https://github.com/XKNX/xknx/blob/e68c024e561dbc486c55dc15d401d070250bfba5/xknx/secure/keyring.py)
- [Python 3.12.15 release](https://www.python.org/downloads/release/python-31215/), [Docker Official Python image](https://hub.docker.com/_/python), [exact Docker Official Images source revision](https://github.com/docker-library/python/tree/2a3b794c223ab067d122719541cdd54a068732a5/3.12/slim-trixie)
- [Docker Official Python 3.12.15 Alpine tag](https://hub.docker.com/layers/library/python/3.12.15-alpine3.24/images/sha256-2055081c860db8c842b663415b795b026e7e5cc63b59c5193fcc9997713ea5b8), [Alpine 3.24 zlib package catalog](https://pkgs.alpinelinux.org/package/v3.24/main/x86_64/zlib), [Docker Official Images Alpine source revision](https://github.com/docker-library/python/tree/2a3b794c223ab067d122719541cdd54a068732a5/3.12/alpine3.24)
- [cryptography 50.0.2 changelog](https://github.com/pyca/cryptography/blob/50.0.2/CHANGELOG.rst), [cryptography wheel/OpenSSL linkage](https://cryptography.io/en/latest/installation/)
- [pip-audit 2.10.1](https://github.com/pypa/pip-audit/tree/2.10.1), [Debian CVE-2026-85091 record](https://security-tracker.debian.org/tracker/CVE-2026-85091)
- [Earlier #539 accepted research](https://github.com/brunolrogerio-collab/EliteSCADA/issues/539)
