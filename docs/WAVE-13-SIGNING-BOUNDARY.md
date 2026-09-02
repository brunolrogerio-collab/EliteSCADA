# Wave 13 — Protected Authenticode Signing Boundary

**Scope:** Windows x64 Wave 13 release engineering  
**Private-key policy:** private Authenticode material never enters normal EliteSCADA CI

## 1. Trust domains

EliteSCADA has two independent signing domains:

1. **license authority signing** — protects EliteSCADA license documents/claims;
2. **Windows Authenticode signing** — establishes Windows publisher identity and integrity of release PE files.

The keys, certificates, access policies and operators for these domains must not be reused merely because both operations are called “signing”.

## 2. Normal CI boundary

Normal GitHub Actions may:

- checkout an exact source SHA;
- build Web/Pyodide;
- publish the self-contained `win-x64` product;
- publish the graphical License Generator authority artifact;
- discover the candidate PE inventory;
- smoke-test unsigned binaries;
- generate candidate metadata;
- upload a clearly named `UNSIGNED` candidate artifact.

Normal CI must not receive:

- PFX/P12 containers;
- Authenticode private keys;
- private-key passwords/PINs;
- exportable signing-key material;
- equivalent credentials that permit normal CI to become the signing authority.

Putting a PFX in a generic GitHub Secret is not the Wave 13 signing architecture.

## 3. Protected signing authority

The signing authority must be an organizationally controlled service or hardware-backed key boundary whose private key is not exportable into normal CI.

The exact provider is deliberately not hard-coded into the repository. Provider selection is an operational/security decision, while the repository defines the input/output contract that every acceptable provider must satisfy.

Required signing properties:

- organizational code-signing certificate suitable for Authenticode;
- expected publisher Subject recorded exactly for release verification;
- valid certificate chain under the target Windows trust policy;
- SHA-256 Authenticode digest;
- trusted **RFC3161** timestamp for every required PE;
- returned signed bytes correspond to the exact unsigned candidate/source SHA submitted for signing.

## 4. Signing input

The protected boundary receives one exact Wave 13 unsigned candidate produced by `.github/workflows/wave13-windows-release.yml`.

`candidate-metadata.json` identifies at minimum:

- schema version;
- product/release version;
- exact 40-character source SHA;
- `win-x64` RID;
- package format;
- `signingState: unsigned-candidate`;
- audited DNP3 dependency/commercial gate;
- `commercialDistributionAuthorized: false` while that gate remains blocked.

The signer must not rebuild the product. Rebuilding would create different bytes outside the accepted source/build evidence. The signing boundary signs the submitted PE files and returns those signed bytes.

## 5. PE signing rule

All PE files discovered in the candidate are part of the signing/verification surface. The current single-file design is expected to expose at least:

- `product/Scada.Api.exe`;
- `authority/EliteSCADA.LicenseGenerator.exe`.

This list is not a loophole or a hard-coded exemption: if future publish output contains another PE (`MZ`) file, the final manifest classifies it as PE and the verifier requires valid Authenticode, the expected publisher and RFC3161 timestamp for it.

Unexpected/undeclared PE files in the final release fail verification.

## 6. Signing output

The protected authority returns the candidate directory with the required PE bytes Authenticode-signed and RFC3161-timestamped. It must not add private-key material or unrelated files.

No final package hash or release manifest is generated before signing, because Authenticode changes PE bytes.

After the signed return:

1. `Complete-WindowsSignedRelease.ps1` validates candidate provenance and moves metadata to `signed-return`;
2. `New-WindowsReleaseManifest.ps1` hashes the signed bytes and records publisher/RFC3161 requirements;
3. `Test-WindowsRelease.ps1` validates hashes, content allowlist, Authenticode trust, exact publisher, timestamp certificate and RFC3161 token;
4. `New-WindowsReleasePackage.ps1` re-runs verification and creates the deterministic ZIP plus SHA-256 record.

## 7. Fail-closed release conditions

No final package is accepted if any of the following is true:

- source SHA is absent/invalid or differs from the submitted candidate;
- required file is missing;
- undeclared content is present;
- an unexpected PE exists;
- a manifest hash differs;
- required Authenticode is missing/invalid;
- certificate publisher differs from the expected Subject;
- trusted timestamp evidence is missing;
- the signature lacks an RFC3161 timestamp token;
- private signing material appears in the candidate/release;
- metadata claims commercial distribution while the DNP3 gate remains blocked.

## 8. Evidence retained for acceptance

Wave 13 acceptance records, without private material:

- release version;
- exact source SHA;
- final package SHA-256;
- final manifest;
- publisher Subject;
- signer certificate thumbprint/chain evidence available from the signing/verification run;
- RFC3161 timestamp evidence;
- exact workflow/run IDs;
- post-merge `main` SHA and required CI results.

## 9. Commercial DNP3 gate

The current product graph contains Step Function I/O `dnp3` 1.6.0. Authenticode signing does not change its licensing status.

While `dnp3CommercialGate` is `blocked`, the signed package may be used only as a controlled technical release artifact within the applicable licensing constraints and must remain `commercialDistributionAuthorized: false`.

Commercial distribution requires recorded commercial clearance from Step Function or an approved/revalidated replacement.