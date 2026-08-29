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
