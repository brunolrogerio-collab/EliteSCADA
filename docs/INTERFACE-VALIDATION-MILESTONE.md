# Interface Validation Milestone

Status: **locked intermediate product milestone**.

EliteSCADA must produce a practical user-testable build after the common multi-driver communication diagnostics slice is complete and **before development proceeds to additional external protocol families** such as MQTT, OPC UA, BACnet or Siemens S7.

## Purpose

This checkpoint exists so the product owner can use the software as a real engineering/runtime application and evaluate the interface, workflows and overall usability before a large amount of additional protocol and driver work is layered on top.

It is a development preview, not a production-certified industrial release.

## Required functional baseline before the preview

The preview milestone is reached only after the current ordered foundation has been integrated and validated:

1. trusted identity/login and basic user lifecycle;
2. current authorization/audit boundaries remain enforced;
3. audit durability/retention reliability slice;
4. historian retention/downsampling baseline;
5. built-in Client Memory and retentive Server Memory foundations;
6. protocol-independent TAG-to-TAG Gateway foundation;
7. common multi-driver/Data-Source communication diagnostics, including Modbus instrumentation and independent-failure behavior;
8. Engineering UI exposes the corresponding useful configuration/diagnostic surfaces needed to exercise those foundations.

The preview does not require MQTT, OPC UA, BACnet, S7 or other additional external protocols. Modbus TCP, simulation/internal sources and the Gateway are sufficient to validate the platform/interface architecture at this checkpoint.

## Delivery characteristics

The milestone must provide a build/package that the product owner can run locally without needing to reconstruct a developer environment by hand. The exact packaging mechanism may evolve, but the delivery must include:

- a clear startup procedure;
- the EliteSCADA backend/runtime and web interface required for the test;
- a known local login/bootstrap procedure;
- a sample/demo project suitable for exercising Runtime and Engineering;
- the database/services required by the build, either packaged/automated or documented with a reliable launcher;
- a visible build/version identifier so feedback can be tied to an exact software state;
- a short test checklist covering Runtime, Engineering navigation, TAG/Data Source/alarm work, memory, Gateway and driver diagnostics;
- no production credentials or committed secrets.

For the initial product-owner test, Windows x64 is the primary practical target unless the deployment strategy is deliberately changed before this milestone.

## Acceptance gate

Before handing the preview to the product owner:

- relevant .NET tests pass;
- frontend build passes;
- runtime smoke passes;
- Chromium E2E passes;
- the packaged/startup path is smoke-tested separately from repository-only developer execution;
- known blocking defects that prevent meaningful interface evaluation are fixed rather than merely documented away.

## Feedback gate

After the product owner tests the preview, interface/workflow feedback is reviewed and prioritized before the project invests heavily in the next external protocol wave.

Feedback may change UI/workflow implementation, but must not silently weaken the locked public Engineering model, security boundaries, revision lifecycle, source-provider architecture or protocol-independent Gateway principles.

## Position in development order

The locked sequence around this milestone is:

`internal memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

This milestone is therefore an explicit development gate, not an optional demo assembled only if convenient.
