# Online license recovery and Portal connectivity

This note describes the product-side contract for same-installation online-license recovery and the licensing-panel connectivity indicator. The Portal implementation is tracked separately in PR #825; this product change is tracked in PR #826.

## Recovering an existing installation

The user still enters their SCADIVO account, password, and one license code. The product sends the current refresh credential from protected local storage only when the local state is neither reset nor marked as requiring reauthentication. The credential is not projected to the UI and is not written to logs.

The Portal resolves the request against the authenticated account and exact license, application, and machine fingerprint. It returns the canonical activation ID, a `reactivation` flag, and a credential generation. The product validates these protocol fields and persists the canonical ID and confirmation intent before asking the user to confirm. The generation remains internal and is not sent as a separate field in confirmation v1.

If the Portal identifies one eligible activation for the same installation, the product explains that confirmation recovers that same activation and keeps the same Portal slot occupied. Confirmation rotates the protected credential and lease while preserving the existing activation. It does not create an additional slot. A reset or removal on the local computer does not revoke the Portal activation or release its slot.

Invalid proof, ambiguous candidates, incompatible bindings, revoked activations, and generation changes remain explicit failures; the product does not fall back to a new activation after a proof-based recovery failure. The user must confirm the resolved application and license before the Portal can complete recovery. Canceling a preparation does not confirm or consume a slot.

## Portal connectivity indicator

The licensing panel requests `GET /api/licensing/online/connectivity` from the product API when opened. The backend calls the Portal's public HTTPS health route, `GET /portal/api/licensing/device/health`, with a short timeout and no account, license, or activation data. The browser does not call the Portal directly. The user can retry manually; there is no background polling.

The health route only confirms that the Portal service responded. “Connected” does not mean a license is active, credentials are valid, or an activation will be accepted. An unsuccessful check does not change the local lease, license, activation slot, or renewal state. The indicator is separate from the authenticated Portal setting that enables or disables license issuance.

## Validation coverage

Automated coverage exercises new activation, recovery with and without local proof, reset recovery, canonical ID adoption when the local journal is absent, fail-closed errors, confirmation retry after a lost response, current credential rotation, and the connectivity request contract. Browser coverage verifies the recovery explanation, localized state, manual retry, and absence of background checks.

The joint preview flow remains a separate release check: activate a dedicated test license, remove only its local binding, recover the same installation, and verify that one Portal slot remains occupied. Do not use or modify a customer's real license for that check.
