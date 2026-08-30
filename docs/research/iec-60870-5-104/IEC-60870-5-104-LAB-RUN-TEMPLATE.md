# IEC 60870-5-104 Lab Run Result Template

Use with:

- `IEC-60870-5-104-ACCEPTANCE-AND-INTEROPERABILITY.md`
- `IEC-60870-5-104-ACCEPTANCE-STATUS-ADDENDUM.md`
- `IEC-60870-5-104-INTEROPERABILITY-LAB-PLAYBOOK.md`

Do not commit this file after filling it with credentials, private addresses, certificates/private keys or protected process information. A sanitized copy may be committed or attached to the Driver 06 handoff evidence.

---

## Run metadata

| Field | Value |
|---|---|
| Run ID | |
| Date/time/timezone | |
| Operator | |
| EliteSCADA branch | `driver6/iec-60870-5-104` |
| EliteSCADA commit | |
| OS / architecture | |
| .NET SDK | |
| Peer tier | A / B / C / D |
| Peer implementation/vendor | |
| Peer version/firmware/commit | |
| Peer runtime/OS | |
| Network topology, sanitized | |
| TCP port | 2404 |
| COT length | 2 |
| CA length | 2 |
| IOA length | 3 |
| Originator Address | 0 |
| Station timezone | |
| Common Addresses | |
| T0/T1/T2/T3 | |
| K/W | |
| Packet capture reference | |
| EliteSCADA log reference | |
| Peer log reference | |
| Diagnostic snapshot reference | |
| Point-map reference | |

## Build/test evidence

```text
dotnet --info:

restore command/result:

build command/result:

test command/result:

pass/fail/skip counts:

TRX/JUnit/artifact reference:
```

---

## Peer capability record

| Capability | Supported by peer? | Notes |
|---|---|---|
| STARTDT / STOPDT | | |
| TESTFR | | |
| Multiple Common Addresses | | |
| GI | | |
| Spontaneous indications | | |
| SQ=0 | | |
| SQ=1 | | |
| CP56Time2a | | |
| Force IV/NT/SB/BL/OV | | |
| Direct single command | | |
| Direct double command | | |
| Direct setpoints | | |
| SBO single command | | |
| SBO double command | | |
| SBO setpoints | | |
| Negative command confirmation | | |
| Suppress ACT_CON/ACT_TERM | | |
| Controlled disconnect during command | | |
| Delayed/suppressed I-frame ACK | | |
| High-rate spontaneous generation | | |

---

## Acceptance cases

Allowed status values: `PASS`, `FAIL`, `BLOCKED`, `N/A`, `NOT RUN`.

### APCI / lifecycle

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A001 | NOT RUN | | |
| IEC104-A002 | NOT RUN | | |
| IEC104-A003 | NOT RUN | | |
| IEC104-A004 | NOT RUN | | |
| IEC104-A005 | NOT RUN | | |
| IEC104-A006 | NOT RUN | | |
| IEC104-A007 | NOT RUN | | |

### Sequence / windows / timers

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A010 | NOT RUN | | |
| IEC104-A011 | NOT RUN | | |
| IEC104-A012 | NOT RUN | | |
| IEC104-A013 | NOT RUN | | |
| IEC104-A014 | NOT RUN | | |
| IEC104-A015 | NOT RUN | | |
| IEC104-A016 | NOT RUN | | |
| IEC104-A017 | NOT RUN | | |
| IEC104-A018 | NOT RUN | | |

### GI / reconnect

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A020 | NOT RUN | | |
| IEC104-A021 | NOT RUN | | |
| IEC104-A022 | NOT RUN | | |
| IEC104-A023 | NOT RUN | | |
| IEC104-A024 | NOT RUN | | |
| IEC104-A025 | NOT RUN | | |
| IEC104-A026 | NOT RUN | | |

### Monitored data / identity

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A030 | NOT RUN | | |
| IEC104-A031 | NOT RUN | | |
| IEC104-A032 | NOT RUN | | |
| IEC104-A033 | NOT RUN | | |
| IEC104-A034 | NOT RUN | | |
| IEC104-A035 | NOT RUN | | |
| IEC104-A036 | NOT RUN | | |
| IEC104-A037 | NOT RUN | | |
| IEC104-A038 | NOT RUN | | |

### Quality / CP56

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A040 | NOT RUN | | |
| IEC104-A041 | NOT RUN | | |
| IEC104-A042 | NOT RUN | | |
| IEC104-A043 | NOT RUN | | |
| IEC104-A044 | NOT RUN | | |
| IEC104-A045 | NOT RUN | | |
| IEC104-A046 | NOT RUN | | |
| IEC104-A047 | NOT RUN | | |
| IEC104-A048 | NOT RUN | | |

### Commands / safety

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A050 | NOT RUN | | |
| IEC104-A051 | NOT RUN | | |
| IEC104-A052 | NOT RUN | | |
| IEC104-A053 | NOT RUN | | |
| IEC104-A054 | NOT RUN | | |
| IEC104-A055 | NOT RUN | | |
| IEC104-A056 | NOT RUN | | |
| IEC104-A057 | NOT RUN | | |
| IEC104-A058 | NOT RUN | | |
| IEC104-A059 | NOT RUN | | |
| IEC104-A060 | NOT RUN | | |
| IEC104-A061 | NOT RUN | | |
| IEC104-A062 | NOT RUN | | |
| IEC104-A063 | NOT RUN | | |
| IEC104-A064 | NOT RUN | | |

### Engineering

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A070 | NOT RUN | | |
| IEC104-A071 | NOT RUN | | |
| IEC104-A072 | NOT RUN | | |
| IEC104-A073 | NOT RUN | | |
| IEC104-A074 | NOT RUN | | |
| IEC104-A075 | NOT RUN | | |
| IEC104-A076 | NOT RUN | | |
| IEC104-A077 | NOT RUN | | |
| IEC104-A078 | BLOCKED | | Coordinator/shared Preview/Apply integration |
| IEC104-A079 | BLOCKED | | Coordinator/shared rich binding schema |

### Diagnostics / isolation

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A080 | NOT RUN | | |
| IEC104-A081 | NOT RUN | | |
| IEC104-A082 | NOT RUN | | |
| IEC104-A083 | NOT RUN | | |
| IEC104-A084 | BLOCKED | | Common public-driver integration |
| IEC104-A085 | BLOCKED | | Common bound-TAG communication-quality path |

### Robustness / load

| ID | Status | Evidence reference | Notes/defect |
|---|---|---|---|
| IEC104-A090 | NOT RUN | | |
| IEC104-A091 | NOT RUN | | |
| IEC104-A092 | NOT RUN | | |
| IEC104-A093 | NOT RUN | | |
| IEC104-A094 | NOT RUN | | |
| IEC104-A095 | NOT RUN | | |

---

## Monitored Type ID evidence

| Type | GI | Spontaneous | SQ=0 | SQ=1 | Quality | CP56 where applicable | Evidence |
|---|---|---|---|---|---|---|---|
| M_SP_NA_1 | | | | | | N/A | |
| M_SP_TB_1 | | | | | | | |
| M_DP_NA_1 | | | | | | N/A | |
| M_DP_TB_1 | | | | | | | |
| M_BO_NA_1 | | | | | | N/A | |
| M_BO_TB_1 | | | | | | | |
| M_ME_NA_1 | | | | | | N/A | |
| M_ME_TD_1 | | | | | | | |
| M_ME_NB_1 | | | | | | N/A | |
| M_ME_TE_1 | | | | | | | |
| M_ME_NC_1 | | | | | | N/A | |
| M_ME_TF_1 | | | | | | | |

## Command evidence

| Type | Direct + | Direct rejection | SBO + | SBO rejection/timeout | Interrupted/ambiguous | No replay | Evidence |
|---|---|---|---|---|---|---|---|
| C_SC_NA_1 | | | | | | | |
| C_DC_NA_1 | | | | | | | |
| C_SE_NA_1 | | | | | | | |
| C_SE_NB_1 | | | | | | | |
| C_SE_NC_1 | | | | | | | |

---

## Resource/load observations

| Metric | Start | Peak | End | Notes |
|---|---:|---:|---:|---|
| Process memory | | | | |
| CPU | | | | |
| Threads/tasks if measured | | | | |
| Open sockets/handles if measured | | | | |
| I-frames received | | | | |
| I-frames sent | | | | |
| Protocol errors | | | | |
| Session failures | | | | |
| Reconnects | | | | |
| Command ambiguous outcomes | | | | |

Soak duration:

Burst rate/duration:

Observed leak/degradation:

---

## Defects / deviations

| Ref | Severity | Case IDs | Description | Reproduction | Disposition |
|---|---|---|---|---|---|
| | | | | | |

## Summary

| Status | Count |
|---|---:|
| PASS | |
| FAIL | |
| BLOCKED | |
| N/A | |
| NOT RUN | |

Overall run verdict: `PASS / FAIL / INCOMPLETE`

Production acceptance impact:

Follow-up actions:
