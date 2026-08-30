# Interoperability scenario contract

The protocol peer and EliteSCADA must communicate over the actual protocol transport. A scenario that calls an EliteSCADA driver implementation directly is an integration/unit test, not evidence from this lab.

## Required scenario fields

Every automated scenario should eventually publish a result containing:

```json
{
  "scenario": "protocol.scenario-name",
  "peer": "implementation + version",
  "startedAt": "ISO-8601",
  "completedAt": "ISO-8601",
  "result": "pass|fail|skipped",
  "evidence": {
    "readiness": {},
    "reads": [],
    "writes": [],
    "timestamps": [],
    "quality": [],
    "diagnostics": []
  }
}
```

`skipped` is acceptable only when a capability is explicitly outside the current Driver scope or the required external peer is unavailable. It must not convert a failure into green.

## Common phases

1. **Clean start** — remove previous volatile scenario state.
2. **Peer ready** — prove the independent server/broker/outstation/PLC simulator is listening and initialized.
3. **EliteSCADA admission** — activate the Data Source and capture readiness evidence.
4. **Read path** — drive known values at the peer and verify EliteSCADA value, type, quality and source timestamp.
5. **Write/command path** — issue a write/operation through EliteSCADA and verify the peer received the intended semantic operation.
6. **Fault injection** — disconnect, reject, delay or restart the peer where supported.
7. **Recovery** — require deterministic degradation and recovery from fresh protocol evidence.
8. **No replay** — prove a command accepted before loss is not silently repeated after reconnect.
9. **Boundary cases** — numeric widths, invalid types, selectors, payload sizes or protocol-specific limits.
10. **Result capture** — retain enough sanitized evidence to diagnose a failure without logging credentials or private keys.

## Evidence levels

- **L0 — unit/model**: no wire protocol. Useful, not interoperability evidence.
- **L1 — same-stack loopback**: real wire but both peers use the same protocol implementation.
- **L2 — independent software peer**: real wire against a different implementation. This is the main target of this lab.
- **L3 — independent vendor/simulator**: third-party commercial/reference simulator or independent stack.
- **L4 — representative hardware**: physical device/PLC/RTU/controller. Required later for commercial confidence, but outside this Docker lab.

The first version of EliteSCADA does not need L4 for every development commit. It does need clear records of which claims are only L1/L2 so software confidence is not confused with hardware acceptance.
