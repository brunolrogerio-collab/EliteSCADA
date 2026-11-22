# EliteSCADA remote timing policy v1

## Scope

`elitescada.timing-policy/v1` is the common, server-advertised contract for remote Web and future EliteGO transport behavior. It defines bounded network budgets, retry limits, request metadata and truthful failure categories. It does not execute requests or migrate existing callers; that belongs to later W15-TIMING slices.

Clients read the effective contract from `GET /api/system/timing-policy`. The endpoint is intentionally bootstrap-safe and exposes no project, user, plant, license or secret data.

## Adaptive values

All durations are milliseconds. Configuration uses the `TimingPolicyV1` section. Unknown keys fail startup instead of being silently accepted.

| Field | Default | Minimum | Maximum | Meaning |
|---|---:|---:|---:|---|
| `ConnectBudgetMilliseconds` | 10,000 | 3,000 | 30,000 | DNS/TCP/TLS or equivalent remote connection establishment |
| `ReadBudgetMilliseconds` | 30,000 | 5,000 | 120,000 | ordinary remote read |
| `BootstrapBudgetMilliseconds` | 45,000 | 10,000 | 180,000 | bounded initial client bootstrap read |
| `LongOperationBudgetMilliseconds` | 120,000 | 30,000 | 600,000 | long remote operation envelope; does not relax inner server safety limits |
| `RealtimeConnectBudgetMilliseconds` | 15,000 | 5,000 | 60,000 | realtime transport establishment |
| `ReconnectDelayMilliseconds` | 500/1,000/2,000/5,000/10,000/20,000/30,000 | 250 each | 30,000 each | strictly increasing reconnect schedule, 1–16 entries |
| `ReconnectJitterRatio` | 0.20 | 0 | 0.50 | bounded reconnect jitter ratio |
| `RealtimeObservationMilliseconds` | 15,000 | 5,000 | 120,000 | transport observation/heartbeat cadence |
| `RealtimeStaleAfterMilliseconds` | 45,000 | 10,000 | 120,000 | realtime stale threshold; at least twice observation cadence |
| `OrdinaryReadMaxRetries` | 2 | 0 | 3 | safe ordinary reads only |
| `CommandWriteResponseBudgetMilliseconds` | 30,000 | 5,000 | 120,000 | response wait for mutation; timeout means unknown outcome, never automatic replay |
| `SlowThresholdMilliseconds` | 2,000 | 500 | 10,000 | user-visible slow transition |
| `StaleMinimumMilliseconds` | 10,000 | 5,000 | 120,000 | lower bound for data-source/display freshness policy |

There is no global timeout multiplier. Deployment overrides must remain inside the advertised bounds and pass relational validation.

## Taxonomy

Adaptive request categories are `transportConnect`, `requestRead`, `bootstrap`, `longOperation`, `realtimeConnect`, `reconnect`, `realtimeHeartbeat`, `commandWriteResponse` and `staleData`.

The shared client contract distinguishes:

- caller cancellation;
- policy timeout;
- offline, DNS, connect, TLS and generic transport failures;
- response-integrity failure;
- HTTP status;
- parse/schema failure;
- stale data;
- unknown mutation outcome.

Only retry-safe reads may use `OrdinaryReadMaxRetries`. Mutation/write timeout or response loss is classified as unknown outcome and must not trigger an automatic replay.

## Correlation and observability seam

The shared request metadata contract provides a request ID, stable operation name, adaptive category, one-based attempt, UTC start time and optional authority-generation identity. Later request-executor slices will apply it consistently.

Clients may send the safe headers `X-EliteSCADA-Request-Id`, `X-EliteSCADA-Operation`, `X-EliteSCADA-Request-Category` and `X-EliteSCADA-Attempt`. The server validates these values before adding them to a structured logging scope. Invalid or unknown metadata is ignored. Every response carries `X-EliteSCADA-Correlation-Id`, sourced only from the server `HttpContext.TraceIdentifier`; client input cannot replace that correlation authority. Completion logs include status and elapsed duration.

## Ownership exclusions

The following domains cannot be configured through `TimingPolicyV1`:

- `securitySession`: JWT lifetime, clock skew and Runtime Session Lease remain Security-owned;
- `haAuthority`: peer heartbeat, election, epoch and fencing remain HA-owned;
- `driverProtocol`: industrial protocol connect/request/reconnect/keepalive semantics remain Driver-owned;
- `internalExecution`: Script sandbox, handler and activation-readiness safety limits remain their owning server/runtime contracts.

The configuration surface is an explicit allow-list derived from the v1 options. Unknown keys—including names for the excluded domains and arbitrary multipliers—fail before application startup. The advertised contract reports the exclusions so consumers cannot mistake network observation for authority.

## Deferred work

W15-TIMING-02 and later slices own request execution, caller migrations, latest-generation state commits, lifecycle unknown-outcome UX, Runtime freshness preservation, realtime reconnection/resynchronization, session continuity and deterministic latency harnesses. This v1 foundation deliberately changes none of those behaviors.
