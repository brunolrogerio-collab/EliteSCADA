# Portal integration status and development queue

Scope: #741. Operational resumption: #756. Product licensing: #301.

## Evidence and deployment boundaries — 29 October 2026

The authenticated production licensing status returned `onlineActivationAvailable: true`. This reports that the online signer loaded its approved key; it does not prove an activation, renewal, recovery or revocation cycle completed. The Administration banner previously contradicted this because it contained a fixed historical message. It now reads the runtime endpoint and reports an unavailable check separately from a disabled signer.

The exact production image/commit and running resource limits were not established in this audit. Deployment defaults and merged code are not evidence of live settings. The versioned Compose limits are 512 MB for the Portal API and 768 MB for PostgreSQL; no load capacity has been measured. Host identity verification must remain enabled when restoring operational access.

## Implemented contract

- Licensing runs within the Portal API, with its database isolated from SCADA/demo data. Product traffic uses outbound HTTPS/TCP 443.
- Preparation authenticates username/email and password, resolves one public license code and returns a five-minute confirmation token and application/rights summary. Preparation and cancellation consume no activation slot.
- Confirmation revalidates rights and commits activation atomically. Exact retries are idempotent. The old direct activation endpoint returns 410.
- Online leases last at most 72 hours; the product renews every 48 hours using rotating device credentials. Perpetual commercial rights still require online renewal. Revocation blocks subsequent renewal; an issued lease remains valid until its signed expiry.
- Interactive/view-only capacities, HA, Intelligence, term and activation count belong to each entitlement. The signed tier carries TAG capacity (Tags500 through Tags5000, or Unlimited); there is no separate numeric TAG-limit claim. Product interpretation remains governed by #301. The requested test configuration is perpetual, unlimited TAGs and 50 sessions of each modality; runtime availability alone does not prove a particular entitlement has those values.
- Preparation is limited to 10 requests per 10 minutes per IP. Confirmation/cancellation share a 12-per-minute IP partition; renewal uses activation plus IP. Portal write operations allow 40 per minute per account. Exceeded limits return 429. These in-process counters are not a distributed quota across replicas.
- The private online signing key is a runtime-only file. Offline machine signing uses a different key and remains a separate operational workflow.

See [activation confirmation](portal/ACTIVATION_CONFIRMATION.md) and [licensing API](portal/LICENSING_API.md) for fields and errors.

## Ordered remaining work

1. Integrate/review the Portal administration/downloads PR #784 and offline intake PR #777, retaining their authorization boundaries. Neither an open PR nor its local tests prove production deployment.
2. Complete and record the real production Portal → preview product acceptance cycle under #301: resolve, inspect summary, cancel without a seat, confirm, recover a lost response, renew with credential rotation, revoke and observe expiry behavior. Record image/commit, authentication type and limitations; do not replace this with synthetic signing evidence.
3. Complete offline delivery: review/approval alone does not sign a license. Define authorized signing, delivery, conversion and activation retirement so online and offline rights are not duplicated. Keep the offline key operation explicit.
4. Connect approved Windows/Linux/OCI release artifacts (#363/#408) to the download catalog, verify hashes and test authorized delivery. Add per-file/version download counts; a redirect records an initiated download, not proof the remote file was fully delivered.
5. Add restricted administrative profile indicators (counts/percentages), then site/Portal access charts with date filters and minimal event data. Define retention and counting before collection.
6. Finish real-renderer email template preview and remaining language/content consistency. Broad community translation remains deferred behind licensing.
7. Implement the commercial catalog/payment provider, authenticated idempotent webhooks, reconciliation and entitlement transitions. No automatic payment claim should appear before this integration is verified.
8. Complete operational evidence: backup restoration, offsite copy, alerts for API/database/email/renewal failures, deployment version identification and capacity testing. Finalize privacy, retention and commercial terms before broad launch.

All changes follow branch/PR review. Record results and deployment separately in #741 and update the single resumption comment in #756.
