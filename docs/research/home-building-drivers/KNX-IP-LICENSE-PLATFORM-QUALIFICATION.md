# KNX/IP licensing and platform qualification

**Lane:** #577 / `DEV-HOME-KNX-IP-QUALIFICATION`
**Decision:** `WAIT / FUTURE_ONLY`
**Evidence date:** 2026-10-09 BRT
**Base:** `wave15/corrections-integration@3f82487a16792945d663e9cf8c5e15199799d208`
**Scope:** documentation and issue evidence only. No driver, product code, dependency, test, schema, workflow, Runtime, or registration change. No merge.

## Recommendation

Do not start KNX/IP implementation in the current development wave. No evaluated stack has cleared the complete licensing, redistribution, .NET 10/Linux/container, Secure fail-closed, Protected Material, and independent L2 gates. The Product Owner excludes paid or restrictive licenses; any unresolved redistribution right remains `WAIT / FUTURE_ONLY`.

Keep Falcon 6 Public SDK as a conditional built-in .NET candidate only if legal review confirms this server/container distribution model and a future lane proves the target platform. Keep XKNX as the preferred independent L2 peer and Python sidecar fallback. Keep Calimero as a secondary independent L2 peer. KNXUltimate is the strongest newly found no-cost license alternative by exact dependency inventory, but its Node/native deployment, key custody, and Secure fail-closed behavior are unproven. No alternative is authorized for implementation by this report.

## Candidate decision matrix

| Candidate (current source version) | License, cost, redistribution | Runtime and platform evidence | Secure and peer assessment | Lane disposition |
| --- | --- | --- | --- | --- |
| KNX Falcon Public SDK 6.4.8671 (6.4.0) | KNX v6.18 terms state no license fee. Falcon distribution grant is conditional on distributing to end users who obtained a software product created using Falcon; the licensee carries support/service/upgrades, and KNX/Falcon marks need written permission. Whether EliteSCADA server/container delivery fits is unresolved. | NuGet package provides `net8.0` and `net48` assets; an isolated restore for `net10.0` succeeded with SDK 10.0.401. This proves restore only, not KNX-supported runtime behavior or container acceptance. KNX requirements list .NET 5/6 platform support including Linux. KNX notes multicast routing needs an IGMP-capable network. | Falcon 6.4 release material mentions a Secure tunneling compatibility fix. Current public evidence located here did not establish Falcon 6 Data Secure support or end-to-end fail-closed behavior. No independent L2 result. A net10 restore also reported a high transitive NuGet advisory for Pkcs 7.0.0. | `WAIT / FUTURE_ONLY`; legal, Data Secure, vulnerable dependency, platform, and L2 gates open. |
| XKNX 3.20.0 | MIT; no license fee shown. The tag has no `uv.lock`. An audit-time Python 3.12/Linux manylinux wheel resolution found five runtime packages, all permissive; it is a snapshot, not a release lock. | Python `>=3.10`; direct requirements are `cryptography>=35.0.0`, `ifaddr>=0.1.7`, plus Python `<3.11` conditional `async_timeout>=4.0.0` and `typing_extensions`. Wheels resolved for manylinux x86_64; no EliteSCADA container smoke. It is not a .NET library. | Official tag includes Secure examples and IP/Data Secure tests, suitable as independent L2 peer. Mixed keyring handling and Protected Material adaptation need an explicit contract. | `WAIT / FUTURE_ONLY`; preferred independent L2 peer, not an approved production sidecar. |
| Calimero core v3.0-M2 | GPL-2 with Classpath Exception; no fee indicated. The exception permits linking independent modules, but distribution and source/notice obligations still require legal review against product packaging. | Requires JDK 21; core Gradle runtime dependency block is empty apart from a commented serial I/O test/runtime dependency. It is a Java process/library, not .NET; no EliteSCADA container or IPC proof. | Project documents KNX IP Secure and Data Secure; useful independent L2 peer. Not selected as shipped runtime. | `WAIT / FUTURE_ONLY`; secondary peer pending legal and sidecar decision. |
| KNXUltimate 6.0.8 | MIT; exact lockfile scan found 55 non-development packages: 53 MIT, 2 ISC, no unknown license metadata. | Node.js service, not .NET; package closure includes native `serialport` bindings. Linux/container compatibility for EliteSCADA is unproven. Exact lockfile SHA-256 is recorded in the dependency inventory. | Project documentation claims KNX IP Secure and Data Secure. However, it also documents accepting non-wrapped routing frames when Secure is enabled and allows mixed secure/plain group-address handling. Strict fail-closed behavior has not been established. Keyring-to-Protected-Material path and independent L2/L3 evidence are absent. | `WAIT / FUTURE_ONLY`; retain as a license-clean alternative for future security/platform evaluation. |
| `knx-dotnet` current `main` / published alpha | MIT; no paid term identified. | Source targets `net7.0`; package metadata is alpha/pre-release. Direct dependencies include `Microsoft.Extensions.Logging.Abstractions 8.0.1`, `System.Reactive 6.0.0`, and build-only `GitVersion.MsBuild 5.12.0`. | README describes tunneling/routing, with no Secure evidence located. Not suitable for current Secure-capable contract. | `REJECT` for current Tier A contract; reconsider only after stable release and Secure evidence. |
| Rust `dphi/knust` repository | MIT; no paid term identified. | Rust library, no .NET 10 integration evidence or published release. | Repository describes KNX IP Secure, while Data Secure is experimental/unverified against an independent reference. | `REJECT` for the current Tier A contract. |

“Compatible” framework calculation, project claims, and software-only tests are not certification, container acceptance, interoperability acceptance, or physical L4 evidence.

## Release and license evidence

### Falcon

The current public package reviewed is [`Knx.Falcon.Sdk` 6.4.8671](https://www.nuget.org/packages/Knx.Falcon.Sdk/6.4.8671), identified by KNX as Falcon 6.4.0 (released 2026-03-05). Its downloaded NuGet package declares `requireLicenseAcceptance=true`, links to the KNX Tools Software License Agreement, and lists eight Microsoft package dependencies. The package uses `net8.0` and `net48` assets.

The current KNX license article points to agreement v6.18, dated 2025-12-01. The Falcon grant has no license fee, but distribution is limited by the grant's end-user/product wording. The agreement also assigns support and upgrades to the licensee and restricts KNX/Falcon mark use. Because that grant is not clearly mapped to EliteSCADA's server/container and customer deployment model, it is unresolved under Product Owner policy. Do not ship Falcon until counsel or KNX provides written confirmation for this exact delivery model. The Manufacturer SDK is excluded: access is limited to KNX manufacturers with Manufacturer Code/public key and its terms differ from the Public SDK.

The package manifest declares minimum versions, so results depend on target framework. An isolated net10 restore resolves `Microsoft.Bcl.AsyncInterfaces 7.0.0`, `System.Security.Cryptography.ProtectedData 7.0.0`, `System.Security.Cryptography.Xml 7.0.0`, and transitive `System.Security.Cryptography.Pkcs 7.0.0`. Restore emitted `NU1903` High for Pkcs 7.0.0 (CVE-2023-29331; patched package version 7.0.2). The exact lock, license metadata, and security result are in the dependency inventory. No product override was tested or proposed.

### XKNX

The [3.20.0 tag](https://github.com/XKNX/xknx/releases/tag/3.20.0) was published 2026-08-16. Its tag `pyproject.toml` declares MIT, Python `>=3.10`, and the direct dependencies listed in the matrix. The tag tree has no `uv.lock`; current `main` having a lock does not freeze the released tag. Resolve and record the full production closure and licenses for the selected Python version/platform before distribution.

The tag contains Secure examples and tests, including Data Secure/IP Secure coverage. This supports its value as an independent protocol peer; it does not prove interoperability with EliteSCADA or physical products.

### Calimero

The [v3.0-M2 release](https://github.com/calimero-project/calimero-core/releases/tag/v3.0-M2) was published 2026-04-04. Its [license file](https://github.com/calimero-project/calimero-core/blob/v3.0-M2/LICENSE.txt) identifies GPL v2 with Classpath Exception. The [build file](https://github.com/calimero-project/calimero-core/blob/v3.0-M2/build.gradle.kts) requires the current JDK 21 toolchain and has no declared core runtime library dependency. The exception does not remove GPL obligations for Calimero itself; have counsel review the executable, notices, source offer, and distribution shape before any product use.

### KNXUltimate

The [v6.0.8 release](https://github.com/Supergiovane/KNXUltimate/releases/tag/v6.0.8) was published 2026-09-29. Its tag [`package.json`](https://github.com/Supergiovane/KNXUltimate/blob/v6.0.8/package.json) declares MIT and direct dependencies `binary-parser`, `serialport`, `winston`, and `xml2js`. The exact tag [`package-lock.json`](https://github.com/Supergiovane/KNXUltimate/blob/v6.0.8/package-lock.json) is lockfile v2; the dependency inventory records all 55 non-development entries and their exact versions/license metadata. The lockfile's SHA-256 is `D94036D15207D23126BA24477D5982BA89AE89E1CBC004373F1538E61774706C`.

The project's own documentation claims Secure tunneling/routing and Data Secure but also describes mixed secure/plain group addresses and acceptance of non-wrapped routing frames while Secure is enabled. This is a security acceptance blocker until the future contract proves a strict required-Secure mode or an independently enforced fail-closed boundary.

## Protected Material and integration constraints

The existing host-owned Protected Material Authority remains the only secret authority. The #539 proposal was to keep `ICommunicationDriverProtectedMaterialResolver` as the driver-facing contract backed by an adapter over generic `IProtectedMaterialAuthority` (Option C); #577 does not approve or implement that architecture. Main must confirm the contract before an implementation lane.

A future adapter must not create another key store or authority. ETS `.knxkeys` import rights and retention, the mapping of keys into an in-memory library keyring, access control, rotation, diagnostics redaction, and zeroization still need product/security review. Do not place keyring content in `.escadapkg`, Engineering JSON, logs, diagnostics, or source-controlled configuration. No candidate has yet demonstrated this path.

Keep tunneling first, Secure-required behavior fail-closed, and no automatic secure-to-plain fallback. A library's ability to process plain group addresses in a Secure-capable process is not itself proof that secured addresses can be safely handled. Preserve TAG identity and report quality/timestamp/source diagnostics through existing common contracts; do not create a KNX-specific Runtime authority.

## Exact bounded DPT set carried forward from #539

No universal DPT claim is made. Any later implementation decision must re-confirm this bounded set and the binding contract:

- `1.001 Switch`, `1.002 Bool`, `1.008 UpDown`, `1.009 OpenClose`, `1.100 HeatCool`
- `3.007 Control_Dimming`, `3.008 Control_Blinds`
- `5.001 Scaling`
- `9.001 Temperature`, `9.004 Lux`, `9.007 Humidity`, `9.020 Voltage`, `9.021 Current`, `9.024 Power`
- `10.001 Time`, `11.001 Date`
- `12.001` unsigned counter, `13.001` signed counter, `13.010` active energy Wh, `13.013` active energy kWh, `14.056` Power W
- `17.001 SceneNumber`, `18.001 SceneControl`, `20.102 HVACMode`

ETS importer is outside v1; if reconsidered later it needs separate rights/security review.

## Blocking gates and RED classification

All REDs below are qualification blockers, not failed Actions runs. This lane has no Actions failure to classify and has not run broad CI.

| ID / gate | RED evidence and source | Required closure before a later `GO` |
| --- | --- | --- |
| `RED-KNX-01 / L0 license & redistribution` | Falcon v6.18 grant is conditional and not mapped to server/container distribution; Calimero GPL-2+Classpath obligations need product-specific legal review; ETS/keyring rights remain open. | Written legal clearance for exact package and deployment model; complete license/notice/source inventory; confirm no paid or restrictive license conflicts with Product Owner policy. |
| `RED-KNX-02 / L1 runtime & platform` | Falcon restores under `net10.0` but no application runtime execution or container smoke was run. XKNX/Calimero/KNXUltimate require Python/Java/Node sidecars and an IPC/deployment design; no candidate has EliteSCADA Linux/container acceptance. | Candidate-specific clean restore/build and runtime/container smoke on supported Linux and .NET 10 host; pin complete transitive closure and native requirements. |
| `RED-KNX-03 / L2 independent peer` | No recorded independent packet-level L2 run. XKNX is the preferred independent peer; Calimero is secondary. KNXUltimate is not independent evidence until protocol cases and traces are reviewed. | Select an independent implementation; record paired tunneling/routing and Secure message vectors, reconnect/error behavior, and packet traces. This is software L2, not physical compatibility. |
| `RED-KNX-04 / L3 Protected Material & product contract` | Option C is a #539 recommendation, not a Main-approved interface decision. No candidate has demonstrated the existing resolver/authority path, fail-closed secret access, redaction, TAG mapping, or Runtime lifecycle. | Main freezes the contract; later DEV demonstrates reuse of the existing authority, secure-key custody, diagnostics/TAG/quality, and lifecycle through the approved driver boundary. |
| `RED-KNX-05 / Secure fail-closed` | Current Falcon Data Secure evidence is insufficient; KNXUltimate documents accepting non-wrapped routing while Secure is enabled. No end-to-end no-downgrade evidence exists. | Select and test exact IP Secure/Data Secure modes, keyring permissions, invalid-key and reconnect cases, and prove a secure-required request cannot downgrade to plain. |
| `RED-KNX-06 / Falcon transitive security` | SDK 10.0.401 restore for net10.0 resolved `System.Security.Cryptography.Pkcs 7.0.0`; NuGet audit reported `NU1903` High, CVE-2023-29331 (X.509 certificate processing DoS). The advisory marks 7.0.2 as patched for this package. | No Falcon selection or override is cleared; a later owner must confirm a compatible patched dependency resolution and repeat the exact-target audit before production consideration. |

`GO` is unavailable until the selected candidate clears L0-L3 and Secure gates. `L4` physical testing is explicitly deferred until after Wave 16, partner disclosure, and stable installation. No physical compatibility claim is made here.

## Research method, commands, and result

Live GitHub was rechecked as authority: #577, #305, #539, target branch, lane branch, and current-base Actions. At the final pre-edit checkpoint, base remained `3f82487a16792945d663e9cf8c5e15199799d208`, the lane branch did not exist remotely, and local worktree was clean. Base `EliteSCADA CI #1708` / run `37989607504` had succeeded on that exact SHA (Web, Backend/runtime, Chromium; full browser E2E step skipped). No new CI was started.

Evidence collection used the following commands from the repository/workspace:

```text
gh api repos/brunolrogerio-collab/EliteSCADA/issues/577
gh api repos/brunolrogerio-collab/EliteSCADA/issues/305/comments --paginate
gh api repos/brunolrogerio-collab/EliteSCADA/issues/539/comments --paginate
gh api repos/brunolrogerio-collab/EliteSCADA/git/ref/heads/wave15/corrections-integration --jq .object.sha
gh api repos/brunolrogerio-collab/EliteSCADA/git/ref/heads/research/home-knx-ip-license-qualification
gh api repos/Supergiovane/KNXUltimate/releases/tags/v6.0.8
python work/research/audit_knxultimate.py
Get-FileHash work/research/knxultimate-6.0.8-package-lock.json -Algorithm SHA256
dotnet restore work/research/falcon-net10-audit/FalconAudit.csproj --packages work/research/falcon-net10-audit/packages --use-lock-file
dotnet list work/research/falcon-net10-audit/FalconAudit.csproj package --include-transitive --vulnerable --format json
python -m pip download --dest work/research/xknx-linux-py312-compatible --platform manylinux_2_17_x86_64 --platform manylinux_2_28_x86_64 --python-version 3.12 --implementation cp --abi cp312 --only-binary=:all: xknx==3.20.0
```

The `gh api` release/ref queries returned the versions and SHAs listed above; the lane ref returned HTTP 404 (absent). The dependency audit fetched npm registry license metadata for every non-development package in the exact v6.0.8 lock: `55 total / 53 MIT / 2 ISC / 0 unknown`. Falcon NuGet package 6.4.8671 declared eight minimum dependencies; a scratch `net10.0` restore resolved the four-package Microsoft closure in the dependency inventory and emitted one high NuGet audit warning. Its lock SHA-256 is recorded there. XKNX's v3.20.0 tag contains no `uv.lock`; a wheel-only resolution for Python 3.12 and manylinux x86_64 produced the five-package license snapshot recorded in the dependency inventory. Calimero's tagged build contained no core runtime library dependency.

The environment already had .NET SDK 10.0.401; no SDK installation was needed. Falcon restore and XKNX wheel resolution were limited to scratch files outside the repository. No product code was compiled or package added to EliteSCADA. No implementation tests were run. For a documentation PR, run only exact-head Wave 15 T1 with `VALIDATION_PROFILE: DOCS_I18N_HELP`; do not run broad CI.

## Primary sources

- [Issue #577 — qualification scope and gates](https://github.com/brunolrogerio-collab/EliteSCADA/issues/577)
- [Issue #305 — Main release and coordination](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305)
- [Issue #539 — accepted KNX/DALI research](https://github.com/brunolrogerio-collab/EliteSCADA/issues/539)
- [KNX Falcon 6.4.0 release note](https://support.knx.org/hc/en-us/articles/31860216353426-Falcon-6-NET-SDK-v6-4-0)
- [Falcon platform requirements](https://support.knx.org/hc/en-us/articles/4410825434642-Requirements)
- [KNX Tools Software License Agreement page, v6.18](https://support.knx.org/hc/en-us/articles/360002909959-KNX-Tools-Software-License-Agreement)
- [Falcon Public vs Manufacturer SDK](https://support.knx.org/hc/en-us/articles/360000159859-Manufacturer-SDK-vs-Public-SDK)
- [Falcon Manufacturer SDK eligibility](https://support.knx.org/hc/en-us/articles/360000180240-How-to-get-the-Manufacturer-Falcon-SDK)
- [Falcon NuGet 6.4.8671](https://www.nuget.org/packages/Knx.Falcon.Sdk/6.4.8671)
- [NuGet audit advisory GHSA-555c-2p6r-68mm / CVE-2023-29331](https://github.com/advisories/GHSA-555c-2p6r-68mm)
- [XKNX 3.20.0 release](https://github.com/XKNX/xknx/releases/tag/3.20.0), [tagged pyproject](https://github.com/XKNX/xknx/blob/3.20.0/pyproject.toml), [tagged Secure tests](https://github.com/XKNX/xknx/tree/3.20.0/test/secure_tests)
- [Calimero v3.0-M2 release](https://github.com/calimero-project/calimero-core/releases/tag/v3.0-M2), [license](https://github.com/calimero-project/calimero-core/blob/v3.0-M2/LICENSE.txt), [build](https://github.com/calimero-project/calimero-core/blob/v3.0-M2/build.gradle.kts)
- [KNXUltimate v6.0.8 release](https://github.com/Supergiovane/KNXUltimate/releases/tag/v6.0.8), [package.json](https://github.com/Supergiovane/KNXUltimate/blob/v6.0.8/package.json), [lockfile](https://github.com/Supergiovane/KNXUltimate/blob/v6.0.8/package-lock.json), [Secure behavior notes](https://github.com/Supergiovane/KNXUltimate/blob/v6.0.8/README.md)
- [`knx-dotnet` project file](https://github.com/ChrisTTian667/knx-dotnet/blob/main/Knx/Knx.csproj), [README](https://github.com/ChrisTTian667/knx-dotnet/blob/main/README.md)
- [`knust` repository](https://github.com/dphi/knust)
