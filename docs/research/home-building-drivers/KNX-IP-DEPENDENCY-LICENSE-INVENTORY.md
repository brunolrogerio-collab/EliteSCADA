# KNX/IP dependency license inventory

## Selected XKNX 3.20.0 — K0 exact Linux x64 candidate closure

**Role:** Main-selected production-sidecar candidate; candidate selection only, not a product implementation or shipping release. Upstream tag/commit: [3.20.0 / `e68c024e561dbc486c55dc15d401d070250bfba5`](https://github.com/XKNX/xknx/tree/e68c024e561dbc486c55dc15d401d070250bfba5). The tagged `pyproject.toml` and `LICENSE` show MIT, and tagged `SecureConfig` accepts an already-loaded in-memory keyring.

The runtime was re-resolved on the exact CPython 3.12.15 Linux/amd64 candidate image `python:3.12.15-slim-trixie`, image index `sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1`, amd64 manifest `sha256:2b4f19dae3a777dfc3b76730bda1e82e1f66ab2a2686fa93ca78edbfb4f04ffe`, Debian 13.7. The exact package wheel filenames and SHA-256 hashes are in [`KNX-IP-K0/xknx-runtime-cp312-linux-amd64.lock`](KNX-IP-K0/xknx-runtime-cp312-linux-amd64.lock); full license texts are in [`KNX-IP-K0/xknx-runtime-wheel-notices.tar.gz`](KNX-IP-K0/xknx-runtime-wheel-notices.tar.gz).

| Package | Version | Route | License evidence | Native/runtime details |
| --- | --- | --- | --- | --- |
| `xknx` | `3.20.0` | direct | MIT; wheel notice captured | Pure Python. |
| `cryptography` | `50.0.2` | direct | Apache-2.0 OR BSD-3-Clause; both full license texts captured | Linux wheel statically links OpenSSL `4.0.3`; runtime `backend.openssl_version_text()` confirmed version. |
| `ifaddr` | `0.2.0` | direct | MIT; wheel notice captured | Pure Python. |
| `cffi` | `2.1.1` | transitive from cryptography | MIT; wheel notice captured | Exact Linux extension inspection showed libc and pthread dynamic links; no dynamic libffi dependency. |
| `pycparser` | `3.11` | transitive from cffi | BSD-3-Clause; wheel notice captured | Pure Python. |

All five PyPI packages are no-fee permissive licenses; no proprietary or restrictive term was identified in this Python closure. The official image itself has a broader OS/runtime closure: 87 installed Debian package records plus image-bundled Python/pip components. [`python-trixie-base-packages.tsv`](KNX-IP-K0/python-trixie-base-packages.tsv) and [`python-trixie-base-notices.tar.gz`](KNX-IP-K0/python-trixie-base-notices.tar.gz) preserve that inventory and its full notice files for Main's complete distribution acceptance. The image-bundled installer is pip `25.0.1`.

`pip-audit 2.10.1` used a separate exact 29-package audit-tool lock and notice archive; it is not part of the XKNX runtime closure. Audit result: all five locked runtime packages, **zero known PyPI vulnerabilities** on 2026-10-10. This does not cover the OCI base image.

Docker Scout CLI 1.24.0 indexed 127 total packages in the official base image and found vulnerabilities in 14 packages: `0 critical / 1 high / 6 medium / 27 low`. The high is Debian Trixie's `zlib 1:1.3.dfsg+really1.3.1-1` / CVE-2026-85091, marked vulnerable with no fixed version in Debian's tracker at audit time. Scout also reports known vulnerabilities in base pip `25.0.1`. Exact report: [`KNX-IP-K0/docker-scout-python-trixie.txt`](KNX-IP-K0/docker-scout-python-trixie.txt). This is `RED-KNX-07` for the exact base image; the candidate is not security-cleared for distribution.

A hash-locked offline install of all five wheels passed in the pinned image. Synthetic smoke confirmed module imports, an in-memory `Keyring` group key/sender association, and explicit `TUNNELING_TCP_SECURE` configuration with a synthetic gateway. No XKNX start, socket, product adapter, Secure packet/fail-closed test, real credential, or physical device was used. The smoke result and scope are in [`KNX-IP-K0/README.md`](KNX-IP-K0/README.md).

Main also pinned the current contract: use the existing host Protected Material authority/resolver; explicit Secure TCP only, no automatic discovery or downgrade; and the bounded state DPT set `1.001`, `1.002`, `5.001`, `9.001`, `9.004`, `9.007`, `9.024`. KNXUltimate 6.0.8 is a test-only independent peer candidate. These decisions are not implementation evidence.

## Falcon 6.4.8671 — isolated .NET 10 restore

This is a scratch restore, not an EliteSCADA product dependency. Target: `net10.0`; SDK: `10.0.401`; package: `Knx.Falcon.Sdk 6.4.8671`. The generated `packages.lock.json` SHA-256 is `E1E2712E1E594EB871754DA8ABAD9E386BF36B696023443ACFC3CA0784AF4CC9`. The resolved graph is target-specific; Falcon's nuspec minimums are not an exact cross-target lock.

| Package | Resolved version | Route | License evidence | Security result |
| --- | --- | --- | --- | --- |
| [`Knx.Falcon.Sdk`](https://www.nuget.org/packages/Knx.Falcon.Sdk/6.4.8671) | `6.4.8671` | direct | KNX proprietary license v6.18; no license fee, conditional redistribution; see qualification report | Vendor terms gate remains open |
| [`Microsoft.Bcl.AsyncInterfaces`](https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces/7.0.0) | `7.0.0` | Falcon nuspec | MIT in package nuspec | No NuGet audit alert in this restore |
| [`System.Security.Cryptography.ProtectedData`](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData/7.0.0) | `7.0.0` | Falcon nuspec | MIT in package nuspec | No NuGet audit alert in this restore |
| [`System.Security.Cryptography.Xml`](https://www.nuget.org/packages/System.Security.Cryptography.Xml/7.0.0) | `7.0.0` | Falcon nuspec | MIT in package nuspec | Pulls Pkcs below |
| [`System.Security.Cryptography.Pkcs`](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/7.0.0) | `7.0.0` | transitive from Xml | MIT in package nuspec | `NU1903` High; [GHSA-555c-2p6r-68mm](https://github.com/advisories/GHSA-555c-2p6r-68mm), CVE-2023-29331. NuGet advisory lists package 7.0.2 as patched. |

The Falcon nuspec also declares minimum versions `Microsoft.Win32.Registry 5.0.0`, `System.Memory 4.5.5`, `System.Runtime.CompilerServices.Unsafe 6.0.0`, `System.Threading.Channels 5.0.0`, and `System.Threading.Tasks.Dataflow 7.0.0`. They did not appear as separate package entries in the `net10.0` lock produced by the isolated restore. Their declared versions and license links are listed below; do not add them as independent resolved packages in this graph.

| Declared package | Nuspec minimum | License evidence |
| --- | --- | --- |
| `Microsoft.Win32.Registry` | `5.0.0` | [NuGet package](https://www.nuget.org/packages/Microsoft.Win32.Registry/5.0.0), MIT |
| `System.Memory` | `4.5.5` | [NuGet package](https://www.nuget.org/packages/System.Memory/4.5.5), [upstream license](https://github.com/dotnet/corefx/blob/master/LICENSE.TXT), MIT |
| `System.Runtime.CompilerServices.Unsafe` | `6.0.0` | [NuGet package](https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.0.0), MIT |
| `System.Threading.Channels` | `5.0.0` | [NuGet package](https://www.nuget.org/packages/System.Threading.Channels/5.0.0), MIT |
| `System.Threading.Tasks.Dataflow` | `7.0.0` | [NuGet package](https://www.nuget.org/packages/System.Threading.Tasks.Dataflow/7.0.0), MIT |

Commands and results:

```text
dotnet restore work/research/falcon-net10-audit/FalconAudit.csproj --packages work/research/falcon-net10-audit/packages --use-lock-file
# restore succeeded for net10.0; emitted NU1903 High for System.Security.Cryptography.Pkcs 7.0.0

dotnet list work/research/falcon-net10-audit/FalconAudit.csproj package --include-transitive --vulnerable --format json
# 1 vulnerable transitive package: System.Security.Cryptography.Pkcs 7.0.0, High, GHSA-555c-2p6r-68mm
```

The restore proves NuGet resolution under SDK 10.0.401 only. It is not a compile, runtime, platform/container, or KNX interoperability result. No product source, dependency, or package was changed.

## Historical XKNX 3.20.0 audit-time Linux/Python resolution (2026-10-09)

The 3.20.0 tag has no runtime lockfile. To inventory licenses without adding packages, pip downloaded wheels only for Python 3.12 and x86_64 manylinux tags; nothing was installed. This is a point-in-time dependency resolution, not a supported product/container image or reproducible XKNX lock. Python `<3.11` conditional dependencies `async_timeout` and `typing_extensions` are not in this Python 3.12 snapshot.

| Package | Resolved version | Route | Wheel metadata license |
| --- | --- | --- | --- |
| [`xknx`](https://pypi.org/project/xknx/3.20.0/) | `3.20.0` | direct | MIT |
| [`cryptography`](https://pypi.org/project/cryptography/50.0.2/) | `50.0.2` | direct | Apache-2.0 OR BSD-3-Clause |
| [`ifaddr`](https://pypi.org/project/ifaddr/0.2.0/) | `0.2.0` | direct | MIT |
| [`cffi`](https://pypi.org/project/cffi/2.1.1/) | `2.1.1` | transitive from cryptography | MIT-0 |
| [`pycparser`](https://pypi.org/project/pycparser/3.11/) | `3.11` | transitive from cffi | BSD-3-Clause |

Command and result:

```text
python -m pip download --dest work/research/xknx-linux-py312-compatible --platform manylinux_2_17_x86_64 --platform manylinux_2_28_x86_64 --python-version 3.12 --implementation cp --abi cp312 --only-binary=:all: xknx==3.20.0
# downloaded 5 wheels; license expressions/readme metadata parsed from each wheel's METADATA
```

The five-package license snapshot is permissive, but because upstream provides no lock and the selected version can change with package index state, the production transitive-license gate remains open until an approved image and lock are selected.

## KNXUltimate 6.0.8

**Upstream tag:** [v6.0.8](https://github.com/Supergiovane/KNXUltimate/tree/v6.0.8)
**Manifest:** [package-lock.json](https://github.com/Supergiovane/KNXUltimate/blob/v6.0.8/package-lock.json), lockfile v2
**Manifest SHA-256:** `D94036D15207D23126BA24477D5982BA89AE89E1CBC004373F1538E61774706C`
**Audit method:** exact non-development package entries from the tag lockfile; license field read from the exact version record at npm registry on 2026-10-09 BRT. This is manifest metadata, not a legal opinion or file-by-file source audit.

Total: 55 locked runtime/non-development package entries; 53 MIT and 2 ISC; 0 unknown/lookup failures. Duplicate package names at distinct versions are preserved as separate lock entries.

| Package | Locked version | Registry license metadata | Exact registry record |
| --- | --- | --- | --- |
| [@colors/colors@1.6.0](https://www.npmjs.com/package/@colors/colors/v/1.6.0) | `1.6.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40colors%2Fcolors/1.6.0) |
| [@dabh/diagnostics@2.0.3](https://www.npmjs.com/package/@dabh/diagnostics/v/2.0.3) | `2.0.3` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40dabh%2Fdiagnostics/2.0.3) |
| [@serialport/binding-mock@10.2.2](https://www.npmjs.com/package/@serialport/binding-mock/v/10.2.2) | `10.2.2` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fbinding-mock/10.2.2) |
| [@serialport/bindings-cpp@13.0.0](https://www.npmjs.com/package/@serialport/bindings-cpp/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fbindings-cpp/13.0.0) |
| [@serialport/bindings-interface@1.2.2](https://www.npmjs.com/package/@serialport/bindings-interface/v/1.2.2) | `1.2.2` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fbindings-interface/1.2.2) |
| [@serialport/parser-byte-length@13.0.0](https://www.npmjs.com/package/@serialport/parser-byte-length/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-byte-length/13.0.0) |
| [@serialport/parser-cctalk@13.0.0](https://www.npmjs.com/package/@serialport/parser-cctalk/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-cctalk/13.0.0) |
| [@serialport/parser-delimiter@12.0.0](https://www.npmjs.com/package/@serialport/parser-delimiter/v/12.0.0) | `12.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-delimiter/12.0.0) |
| [@serialport/parser-delimiter@13.0.0](https://www.npmjs.com/package/@serialport/parser-delimiter/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-delimiter/13.0.0) |
| [@serialport/parser-inter-byte-timeout@13.0.0](https://www.npmjs.com/package/@serialport/parser-inter-byte-timeout/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-inter-byte-timeout/13.0.0) |
| [@serialport/parser-packet-length@13.0.0](https://www.npmjs.com/package/@serialport/parser-packet-length/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-packet-length/13.0.0) |
| [@serialport/parser-readline@12.0.0](https://www.npmjs.com/package/@serialport/parser-readline/v/12.0.0) | `12.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-readline/12.0.0) |
| [@serialport/parser-readline@13.0.0](https://www.npmjs.com/package/@serialport/parser-readline/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-readline/13.0.0) |
| [@serialport/parser-ready@13.0.0](https://www.npmjs.com/package/@serialport/parser-ready/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-ready/13.0.0) |
| [@serialport/parser-regex@13.0.0](https://www.npmjs.com/package/@serialport/parser-regex/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-regex/13.0.0) |
| [@serialport/parser-slip-encoder@13.0.0](https://www.npmjs.com/package/@serialport/parser-slip-encoder/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-slip-encoder/13.0.0) |
| [@serialport/parser-spacepacket@13.0.0](https://www.npmjs.com/package/@serialport/parser-spacepacket/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fparser-spacepacket/13.0.0) |
| [@serialport/stream@13.0.0](https://www.npmjs.com/package/@serialport/stream/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40serialport%2Fstream/13.0.0) |
| [@types/triple-beam@1.3.5](https://www.npmjs.com/package/@types/triple-beam/v/1.3.5) | `1.3.5` | `MIT` | [npm registry JSON](https://registry.npmjs.org/%40types%2Ftriple-beam/1.3.5) |
| [async@3.2.6](https://www.npmjs.com/package/async/v/3.2.6) | `3.2.6` | `MIT` | [npm registry JSON](https://registry.npmjs.org/async/3.2.6) |
| [binary-parser@2.3.0](https://www.npmjs.com/package/binary-parser/v/2.3.0) | `2.3.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/binary-parser/2.3.0) |
| [color@3.2.1](https://www.npmjs.com/package/color/v/3.2.1) | `3.2.1` | `MIT` | [npm registry JSON](https://registry.npmjs.org/color/3.2.1) |
| [color-convert@1.9.3](https://www.npmjs.com/package/color-convert/v/1.9.3) | `1.9.3` | `MIT` | [npm registry JSON](https://registry.npmjs.org/color-convert/1.9.3) |
| [color-name@1.1.4](https://www.npmjs.com/package/color-name/v/1.1.4) | `1.1.4` | `MIT` | [npm registry JSON](https://registry.npmjs.org/color-name/1.1.4) |
| [color-name@1.1.3](https://www.npmjs.com/package/color-name/v/1.1.3) | `1.1.3` | `MIT` | [npm registry JSON](https://registry.npmjs.org/color-name/1.1.3) |
| [color-string@1.9.1](https://www.npmjs.com/package/color-string/v/1.9.1) | `1.9.1` | `MIT` | [npm registry JSON](https://registry.npmjs.org/color-string/1.9.1) |
| [colorspace@1.1.4](https://www.npmjs.com/package/colorspace/v/1.1.4) | `1.1.4` | `MIT` | [npm registry JSON](https://registry.npmjs.org/colorspace/1.1.4) |
| [debug@4.4.0](https://www.npmjs.com/package/debug/v/4.4.0) | `4.4.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/debug/4.4.0) |
| [enabled@2.0.0](https://www.npmjs.com/package/enabled/v/2.0.0) | `2.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/enabled/2.0.0) |
| [fecha@4.2.3](https://www.npmjs.com/package/fecha/v/4.2.3) | `4.2.3` | `MIT` | [npm registry JSON](https://registry.npmjs.org/fecha/4.2.3) |
| [fn.name@1.1.0](https://www.npmjs.com/package/fn.name/v/1.1.0) | `1.1.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/fn.name/1.1.0) |
| [inherits@2.0.4](https://www.npmjs.com/package/inherits/v/2.0.4) | `2.0.4` | `ISC` | [npm registry JSON](https://registry.npmjs.org/inherits/2.0.4) |
| [is-arrayish@0.3.2](https://www.npmjs.com/package/is-arrayish/v/0.3.2) | `0.3.2` | `MIT` | [npm registry JSON](https://registry.npmjs.org/is-arrayish/0.3.2) |
| [is-stream@2.0.1](https://www.npmjs.com/package/is-stream/v/2.0.1) | `2.0.1` | `MIT` | [npm registry JSON](https://registry.npmjs.org/is-stream/2.0.1) |
| [kuler@2.0.0](https://www.npmjs.com/package/kuler/v/2.0.0) | `2.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/kuler/2.0.0) |
| [logform@2.7.0](https://www.npmjs.com/package/logform/v/2.7.0) | `2.7.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/logform/2.7.0) |
| [ms@2.1.3](https://www.npmjs.com/package/ms/v/2.1.3) | `2.1.3` | `MIT` | [npm registry JSON](https://registry.npmjs.org/ms/2.1.3) |
| [node-addon-api@8.3.0](https://www.npmjs.com/package/node-addon-api/v/8.3.0) | `8.3.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/node-addon-api/8.3.0) |
| [node-gyp-build@4.8.4](https://www.npmjs.com/package/node-gyp-build/v/4.8.4) | `4.8.4` | `MIT` | [npm registry JSON](https://registry.npmjs.org/node-gyp-build/4.8.4) |
| [one-time@1.0.0](https://www.npmjs.com/package/one-time/v/1.0.0) | `1.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/one-time/1.0.0) |
| [readable-stream@3.6.2](https://www.npmjs.com/package/readable-stream/v/3.6.2) | `3.6.2` | `MIT` | [npm registry JSON](https://registry.npmjs.org/readable-stream/3.6.2) |
| [safe-buffer@5.2.1](https://www.npmjs.com/package/safe-buffer/v/5.2.1) | `5.2.1` | `MIT` | [npm registry JSON](https://registry.npmjs.org/safe-buffer/5.2.1) |
| [safe-stable-stringify@2.5.0](https://www.npmjs.com/package/safe-stable-stringify/v/2.5.0) | `2.5.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/safe-stable-stringify/2.5.0) |
| [sax@1.2.4](https://www.npmjs.com/package/sax/v/1.2.4) | `1.2.4` | `ISC` | [npm registry JSON](https://registry.npmjs.org/sax/1.2.4) |
| [serialport@13.0.0](https://www.npmjs.com/package/serialport/v/13.0.0) | `13.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/serialport/13.0.0) |
| [simple-swizzle@0.2.2](https://www.npmjs.com/package/simple-swizzle/v/0.2.2) | `0.2.2` | `MIT` | [npm registry JSON](https://registry.npmjs.org/simple-swizzle/0.2.2) |
| [stack-trace@0.0.10](https://www.npmjs.com/package/stack-trace/v/0.0.10) | `0.0.10` | `MIT` | [npm registry JSON](https://registry.npmjs.org/stack-trace/0.0.10) |
| [string_decoder@1.3.0](https://www.npmjs.com/package/string_decoder/v/1.3.0) | `1.3.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/string_decoder/1.3.0) |
| [text-hex@1.0.0](https://www.npmjs.com/package/text-hex/v/1.0.0) | `1.0.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/text-hex/1.0.0) |
| [triple-beam@1.4.1](https://www.npmjs.com/package/triple-beam/v/1.4.1) | `1.4.1` | `MIT` | [npm registry JSON](https://registry.npmjs.org/triple-beam/1.4.1) |
| [util-deprecate@1.0.2](https://www.npmjs.com/package/util-deprecate/v/1.0.2) | `1.0.2` | `MIT` | [npm registry JSON](https://registry.npmjs.org/util-deprecate/1.0.2) |
| [winston@3.17.0](https://www.npmjs.com/package/winston/v/3.17.0) | `3.17.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/winston/3.17.0) |
| [winston-transport@4.9.0](https://www.npmjs.com/package/winston-transport/v/4.9.0) | `4.9.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/winston-transport/4.9.0) |
| [xml2js@0.6.0](https://www.npmjs.com/package/xml2js/v/0.6.0) | `0.6.0` | `MIT` | [npm registry JSON](https://registry.npmjs.org/xml2js/0.6.0) |
| [xmlbuilder@11.0.1](https://www.npmjs.com/package/xmlbuilder/v/11.0.1) | `11.0.1` | `MIT` | [npm registry JSON](https://registry.npmjs.org/xmlbuilder/11.0.1) |
