# EliteSCADA OPC UA client, discovery and Engineering import

## Status

Locked product/architecture requirement recorded from product-owner direction on 2026-08-26.

OPC UA is a planned external protocol family for EliteSCADA. The production OPC UA driver/runtime remains gated by the mandatory sequence in `PROJECT GOAL.md` and `docs/ROADMAP.md`. An early research/design spike may run before that gate only when it does not add a production OPC UA runtime, central DI/API integration or an active external protocol Data Source.

This document locks the intended Engineering experience so the later implementation is not reduced to a text box containing an endpoint URL and a NodeId.

## Product goal

OPC UA Engineering should make discovery and TAG creation practical for an industrial engineer:

`Discover server -> inspect endpoint/security -> connect/test -> browse address space -> select nodes -> preview TAG mapping -> apply through canonical Engineering`

Manual configuration remains supported, but discovery/browse/import is a first-class workflow.

## Reference behavior studied

The Elipse E3 OPC UA workflow provides useful precedents:

- server selection can enumerate local/network OPC UA servers and their endpoints;
- endpoint selection exposes protocol/security/authentication alternatives and fills connection properties;
- communication can be activated/tested before TAG import;
- importing TAGs creates/uses an OPC UA Subscription and opens a multiple-selection browser;
- Tags share subscription scan/update behavior;
- imported TAGs retain browse-path information so NodeIds can later be refreshed/resolved again;
- server information and TAG counts are visible from Engineering;
- OPC UA items can be selected in bulk rather than entered one NodeId at a time.

EliteSCADA should preserve those conveniences while using its own public Engineering model and stronger security defaults.

## Technology direction

Primary implementation candidate: the official OPC Foundation UA .NET Standard client stack, currently the 2.x generation, because it is cross-platform, supports .NET 10 and exposes discovery, sessions, browsing, subscriptions/monitored items, reconnect and PKI/GDS/LDS capabilities.

The final dependency/version decision must be made during the implementation slice with license, package, security and interoperability review. No private OPC UA protocol implementation should be created when the official stack satisfies the requirement.

## Data Source model

One OPC UA Data Source represents one configured client relationship to an OPC UA server/application endpoint context.

Engineering configuration should eventually include, through a public versioned driver schema:

- stable Data Source identity;
- selected server/application identity where available;
- endpoint URL;
- transport profile;
- security mode;
- security policy;
- client certificate/trust-store reference;
- server certificate identity/thumbprint expectation where approved;
- authentication mode;
- secret reference for username/password or other credentials, never plaintext secrets in Engineering packages;
- connection/session timeout policy;
- reconnect policy;
- subscription defaults;
- sanitized discovery metadata useful for reconciliation.

The Data Source is not the OPC UA server itself. Several Data Sources may intentionally point to different endpoints or servers, and failures remain isolated per Data Source.

## Discovery workflow

Engineering must offer both manual and assisted discovery.

### Manual endpoint

The engineer can always enter a known OPC UA endpoint/discovery URL and ask EliteSCADA to inspect it.

### OPC UA discovery services

Where supported, use standard OPC UA discovery operations such as server discovery and endpoint enumeration rather than guessing endpoints.

The UI should expose discovered application/server identity and enumerate compatible endpoints with at least:

- endpoint URL;
- transport profile;
- security mode;
- security policy;
- supported user-token/authentication types;
- server certificate summary;
- compatibility/safety indication.

### Network discovery / Scan

EliteSCADA must provide a deliberate **Scan network for OPC UA devices/servers** tool.

The scan must be industrial-network friendly:

- opt-in and manually initiated;
- bounded by explicit subnet/CIDR or selected network interface;
- cancellable;
- rate/concurrency limited;
- show progress and failures without blocking Engineering;
- prefer OPC UA discovery mechanisms such as LDS/LDS-ME/mDNS/FindServersOnNetwork when available;
- optionally probe configured/common OPC UA TCP ports only as a bounded fallback;
- never perform an unbounded aggressive port scan;
- allow manual host/port addition when discovery is disabled by the device/network;
- return candidate servers/endpoints for inspection, not automatically add them to the project.

The discovery result is transient Engineering assistance. It is not authoritative project configuration until the engineer selects a server/endpoint and applies the resulting Data Source through normal Engineering Preview/Apply.

## Endpoint and security inspector

Before creating/activating a Data Source, Engineering should allow a connection test and show:

- application name/URI/product URI where reported;
- endpoint and transport;
- security mode/policy;
- user-token policies;
- certificate subject, issuer, validity and thumbprint;
- trust state;
- session/server state if connected;
- sanitized connection error when not connected.

EliteSCADA must not copy insecure convenience behavior that blindly trusts any server certificate. Secure connections use an explicit trust model. First-contact certificate approval may be offered as a deliberate user action that displays certificate identity/thumbprint and is auditable where appropriate.

Changing a previously trusted server identity/certificate must be visible and fail closed until explicitly reconciled.

## Address-space browser

After a successful temporary Engineering connection, the engineer can browse the OPC UA address space.

The browser should be lazy-loaded and cancellable so large servers do not require recursively reading the entire tree before the user can work.

For each node, show as available:

- BrowseName and DisplayName;
- namespace URI/index context;
- NodeId;
- node class;
- data type;
- scalar/array/value rank;
- access level and effective user access;
- writable/read-only indication;
- Historizing indication;
- description;
- engineering-unit/range metadata when exposed;
- current value/quality only when the engineer explicitly requests preview or when safely useful.

Folders/objects/variables should remain visually distinguishable.

## Browse search and filters

The browser must support practical filtering/search because industrial OPC UA address spaces can contain thousands of nodes.

Desired filters include:

- name/BrowseName text;
- namespace;
- variable nodes only;
- data type;
- readable/writable;
- historizing;
- selected subtree;
- already imported / not imported.

Recursive client-side search must have explicit depth/node/time limits and cancellation. Do not accidentally turn a search box into an uncontrolled full-server crawl.

## TAG selection and import

The engineer can select individual nodes, non-contiguous nodes, ranges and optionally a folder/subtree for recursive candidate collection.

Selection itself does not mutate the project. It produces an import preview.

The import preview should display each candidate with:

- proposed EliteSCADA TAG path/name;
- source OPC UA BrowsePath;
- resolved NodeId;
- namespace identity;
- mapped EliteSCADA data type;
- read/write capability;
- proposed subscription/profile;
- conflicts with existing TAG paths/IDs;
- unsupported/ambiguous types;
- warnings for arrays/structures or oversized imports.

The engineer may deselect, rename/repath, choose a destination folder/equipment context and adjust subscription policy before Apply.

Import then uses the canonical Engineering workflow:

`discover/browse -> build candidates -> validate -> preview -> choose merge semantics -> apply`

No OPC UA-specific importer may bypass the public/versioned Engineering model.

## Node identity and resilient re-resolution

Do not rely on NamespaceIndex or a transient NodeId alone as the only Engineering reference.

For imported nodes, preserve enough identity to reconcile later, including where available:

- NodeId as last resolved runtime identifier;
- namespace URI rather than only NamespaceIndex;
- portable BrowsePath composed from namespace-aware BrowseNames;
- server/application identity context.

The runtime may use the resolved NodeId for efficient access, but Engineering should retain the portable browse identity so it can re-resolve nodes when namespace indexes or NodeIds change after server/device redeployment.

A **Refresh/Re-resolve OPC UA Node IDs** action should:

1. connect to the configured server;
2. translate/rebrowse stored portable paths;
3. compare the resolved NodeId/type/access with the current Engineering binding;
4. present a preview of changes/mismatches;
5. apply only after explicit Engineering validation.

Missing, ambiguous or type-changed nodes must be surfaced explicitly. They are never silently rebound to a different variable merely because a name happens to resemble the old one.

## Rescan and synchronization

A configured OPC UA Data Source should later support **Rescan/Browse changes**.

The result is a diff, not automatic destructive synchronization:

- newly discovered nodes;
- nodes no longer found;
- NodeId/path changes;
- data-type/access changes;
- existing imported nodes still compatible.

The engineer chooses what to import/update. Removed server nodes do not silently delete EliteSCADA TAG Engineering or historian data.

## Subscriptions and monitored items

OPC UA should use native subscriptions/monitored items for normal realtime acquisition rather than pretending every point is an independent polling request.

Engineering needs reusable subscription/update profiles with at least:

- publishing/update interval;
- sampling interval/default behavior;
- queue size/discard policy where exposed;
- deadband/filter policy where appropriate;
- enabled state/priority when useful.

A sensible default profile should exist so imported TAGs work without forcing the engineer to understand every OPC UA subscription parameter immediately.

Large imports must be partitionable according to server capability/limits without changing public TAG semantics.

Future demand-driven optimization may reduce monitoring for unused points only if runtime semantics, alarms, historian and server logic remain correct. A TAG needed by alarms/historian/Gateway/server scripts is never considered unused merely because no screen is open.

## Data types

The implementation must deliberately map OPC UA built-in types to EliteSCADA TAG types and report unsupported/lossy cases.

Initial high-priority scalar support should include Boolean, signed/unsigned integers with explicit range handling, Float, Double, String and DateTime. ByteString, Guid, LocalizedText, QualifiedName, enums, arrays and Structures require explicit mapping policy rather than silent conversion.

The spike must study the official stack complex-type support so custom structures can be added later without corrupting values.

## Read/write semantics

Imported TAGs must reflect actual server/user access rights as observed during Engineering and be validated again at runtime.

A node discovered as read-only must not become a writable EliteSCADA TAG merely because the UI checkbox was changed.

Writes use the normal EliteSCADA authorization/TAG write boundaries. The OPC UA driver never provides a private bypass around Engineering/security/Audit.

## Diagnostics direction

When OPC UA production implementation is eventually allowed, its Data Source diagnostics should integrate with the common multi-driver diagnostics model and add protocol-specific detail where meaningful, such as:

- session/connection state;
- endpoint identity;
- reconnect count;
- subscription health;
- monitored-item counts;
- publish/keepalive timing/failures;
- server status;
- sanitized OPC UA StatusCode/last error.

Discovery/browse failures are Engineering diagnostics and must not masquerade as active runtime Data Source health before the source exists in the Active Revision.

## Historical access and methods

OPC UA Historical Access and Method Calls are useful capabilities, but they are not required for the first realtime driver slice unless explicitly assigned later.

They must not distort the initial TAG acquisition/import architecture. If added, they use separate public contracts and normal security boundaries.

## UX target for EliteSCADA

A desirable first Engineering workflow is:

1. Add Data Source -> OPC UA.
2. Choose **Scan network**, **Discover from host**, or **Enter endpoint manually**.
3. Inspect discovered server/endpoints and security/authentication.
4. Approve/trust the expected certificate when required.
5. Test connection.
6. Open **Browse / Import TAGs**.
7. Navigate/search/filter the server tree.
8. Select variables or subtrees.
9. Review proposed TAG paths, types, permissions and subscription profile.
10. Preview canonical Engineering changes.
11. Apply.
12. Later use **Rescan** or **Refresh Node IDs** to reconcile server changes safely.

The goal is to make OPC UA onboarding easier than manual NodeId configuration while preserving deterministic, reviewable Engineering.

## Required research/spike outputs before production implementation

An early non-production spike may establish:

1. official OPC Foundation .NET client package/version/license recommendation;
2. discovery capabilities actually available cross-platform: FindServers/GetEndpoints, LDS/LDS-ME/mDNS/FindServersOnNetwork and bounded fallback scan strategy;
3. certificate/trust-store architecture compatible with EliteSCADA secrets/security rules;
4. address-space browse model and pagination/continuation-point handling;
5. portable BrowsePath + namespace-URI + NodeId reconciliation strategy;
6. TAG type/access mapping table;
7. subscription/profile mapping and server-limit handling;
8. import-preview UX/data contract compatible with canonical Engineering;
9. representative test servers/simulators and CI strategy;
10. limitations/risks that must be solved before the production driver begins.

The spike must not register an OPC UA Data Source in production runtime or bypass the roadmap gates.

## Required future validation scenarios

Production implementation must eventually validate at least:

1. manual endpoint connection;
2. discovery of one or more servers/endpoints;
3. bounded network scan cancellation and duplicate-result reconciliation;
4. secure endpoint with explicit certificate trust;
5. anonymous and username/password authentication where supported;
6. lazy browse of a large address space;
7. multi-select and subtree import preview;
8. type/access mapping including writable/read-only nodes;
9. namespace-index change with successful namespace-URI/browse-path re-resolution;
10. missing/replaced node fail-closed behavior;
11. reconnect/session recovery;
12. subscription recreation/transfer behavior as supported;
13. quality/timestamp propagation;
14. write authorization and write failure handling;
15. multiple simultaneous OPC UA Data Sources isolated from each other;
16. Gateway participation after Gateway/runtime prerequisites are complete;
17. no silent deletion of Engineering TAGs during rescan;
18. canonical import/export/project-package preservation of all OPC UA Engineering settings without plaintext secrets.

## Implementation gate

Research/design may proceed early because it does not alter active protocol runtime architecture.

The production OPC UA driver, runtime Data Source registration, central API/DI composition and end-user live protocol implementation remain blocked until the prerequisite sequence in `PROJECT GOAL.md` and `docs/ROADMAP.md` permits the external-protocol wave.
