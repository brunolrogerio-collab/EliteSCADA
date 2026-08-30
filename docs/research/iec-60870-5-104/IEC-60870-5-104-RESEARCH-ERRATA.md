# IEC 60870-5-104 research errata

Date: 2026-08-29
Applies to: `IEC-60870-5-104-RESEARCH.md`

## CP56Time2a Section 9.2 correction

Section 9.2 of the original research note incorrectly refers to a CP56Time2a `substituted-time` flag.

The implementation contract is instead:

- CP56Time2a minute bit 7 is `IV` (invalid time).
- CP56Time2a hour bit 7 is `SU` (summer-time/daylight-saving indication).
- CP56Time2a has no substituted-time quality flag.
- `SU` selects/indicates daylight-saving interpretation where the configured station timezone makes a local wall-clock value ambiguous. `SU` by itself does not downgrade EliteSCADA TAG quality.
- `IV` means no `SourceTimestamp` is fabricated from that CP56 value. The process value may remain usable according to its independent SIQ/DIQ/QDS quality.
- `SB` (substituted) remains a real process-value quality bit in SIQ/DIQ/QDS and continues to map to EliteSCADA `Uncertain`.

The current `Iec104Cp56Time2a` implementation follows this corrected layout. This errata is authoritative over the conflicting wording in Section 9.2 until the coordinator folds the correction back into the base research document.

## Short-float non-finite measurement policy

The base research robustness section identifies `NaN` and `Infinity` short-float indications as hostile/non-conforming edge inputs but does not define their publication quality.

The Driver 6 implementation contract is:

- `M_ME_NC_1` and `M_ME_TF_1` continue to decode the IEC short-float payload as IEEE-754 single precision.
- finite values follow the QDS quality mapping normally.
- `NaN`, positive infinity and negative infinity are preserved as their IEEE-754 `float` values so protocol evidence is not silently rewritten.
- a non-finite short-float with otherwise healthy QDS is published as EliteSCADA `TagQuality.Uncertain`.
- stronger QDS states keep their existing precedence. In particular, QDS `IV` still maps to `BadDevice` even when the numeric payload is non-finite.
- a non-finite numeric payload does not by itself fault the IEC-104 TCP/session state because the framing and ASDU remain structurally decodable.

This is a protocol semantic-quality decision local to IEC-104 and does not change the common EliteSCADA `TagValue` contract.

## Acceptance-runbook status deltas

The acceptance runbook was created before the following deterministic cases were added. Until the runbook is consolidated, these entries supersede the earlier `IMPLEMENTED / NO AUTOMATED CASE` wording:

- `IEC104-A007` and `IEC104-A091`: `Iec104ReceiveQueueBoundsTests.Adapter_ReceiveQueueOverflowFaultsSessionInsteadOfDroppingProcessData` now covers the bounded 1024-ASDU receive queue and verifies explicit session failure on overflow rather than silent dropping. Status: **WRITTEN / NOT EXECUTED**.
- `IEC104-A038`: `Iec104UnsupportedAsduIsolationTests` now proves an unsupported Type ID does not publish an operational point and does not become an Engineering observation candidate. Status: **WRITTEN / NOT EXECUTED**.
- `IEC104-A095`: `Iec104NonFiniteShortFloatTests` now covers NaN, positive infinity, negative infinity, timed non-finite values and QDS precedence. Status: **WRITTEN / NOT EXECUTED**.
