# Intesis 700 DALI-2 Engineering profile

This starter maps documented process values through the existing `modbus.tcp` provider. It is a configuration example for an Intesis IN703DAL0640000 (one DALI channel) or IN704DAL1280000 (two channels). It does not add a DALI driver, Runtime path, commissioning authority, or production dependency.

## Official source and map revision

| Model | Official manual | Version | Modbus map pages (manual page numbers) |
| --- | --- | --- | --- |
| IN703DAL0640000 | [HMS Networks user manual (PDF)](https://www.hms-networks.com/docs/default-source/products/intesis/manuals-and-guides---manuals/user-manual-in703dal0640000.pdf) | 1.0.11 | 32–42 |
| IN704DAL1280000 | [HMS Networks user manual (PDF)](https://www.hms-networks.com/docs/default-source/products/intesis/manuals-and-guides---manuals/user-manual-in704dal1280000.pdf) | 1.0.11 | 29–39 |

The source is the Modbus TCP/RTU register map in section 5.2.1. The manuals list general Modbus functions (FC03 and FC04 reads; FC06 and FC16 holding-register writes) and mark each point R, W, RW, or trigger. They do not assign FC03 versus FC04 to each individual row. Accordingly, this starter uses `input:`/FC04 for R points and `holding:`/FC03 plus FC06/FC16 for RW points as an explicit profile convention. Confirm the selected data area against the installed gateway and Intesis MAPS configuration before live use. The local fixture test cannot validate this convention against hardware.

## Starter contents

[`intesis-700-dali-2-engineering-package.json`](./intesis-700-dali-2-engineering-package.json) is an importable schema-v23 Engineering package. It contains one sample DALI ballast at channel 0 / short address 0, and one already-commissioned DALI group 0 color projection. The `192.0.2.10` host is reserved for documentation fixtures; replace it and the unit ID for a real installation. Stable identities and TAG IDs are sample values.

The profile uses the canonical 0-based Modbus address grammar already implemented by the provider: `input:<address>` and `holding:<address>`. The generated sample covers channel 0 and therefore applies to either model. IN703 has channel 0 only. IN704 channel-1 displacement depends on the register family; do not apply 7000 as a universal channel offset. Basic ballast status, level, and on/off points use `address = 7000*y + (100*xx) + offset`; color-group points use `address = 7000*y + (20*xx) + offset`. Here `y` is the channel (0 or 1), `xx` is the ballast short address (0–63) or configured group (0–15), and `offset` is the point's listed offset. The individual Type 8 table (section 5.2.1.6, manual p. 36) and Type 51 table (section 5.2.1.9, p. 38) use a 6400 channel term. For Type 52 Control Gear Failure (section 5.2.1.10, p. 39), the address is `25000 + 6400*y + (100*xx) + 65`; channel 1 / short address 0 is register 31465. Check the Type 8, Type 51, and Type 52 formula gates below before using those address families.

| Function | Manual map item | Sample point | Area and access | Bounds/type |
| --- | --- | --- | --- | --- |
| Lamp power state | Ballast Status `b2: LampPwrOn`, offset +5 | `input:5`, bit 2 | R / read-only | Boolean |
| Lamp failure | Ballast Status `b1: LampFail`, offset +5 | `input:5`, bit 1 | R / read-only | Boolean |
| Ballast failure | Ballast Status `b0: BallFail`, offset +5 | `input:5`, bit 0 | R / read-only | Boolean |
| Actual level | Actual Level, offset +6 | `input:6` | R / read-only | Int16, 0–100% |
| Set level | Arc Power Level, offset +15 | `holding:15` | RW | Int16, 0–100% |
| Set on/off | Arc Power Off / On, offset +16 | `holding:16` | RW | Int16, 0=Off, 1=100% |
| Gear faults | Control Gear Failure bits 0–5, Type 52, offset +65 | `input:25065`, bits 0–5 | R / read-only | Boolean bits |
| Group CCT | Arc Colour level Tc, Type 8, group offset +6416 | `holding:6416` | RW | Int16, 1000–10000 K |

Bit selection uses the existing TAG `AddressSelector` and Modbus register-bit support. The three writable points remain ordinary TAG writes through `Runtime.WriteAsync(TAG) -> modbus.tcp -> gateway`. No Commands are included. The CCT group projection requires group 0 to have been assigned and commissioned outside ordinary Runtime/HMI operation. The individual-ballast Type 8 Arc Colour Level is write-only in the Modbus table, so this starter does not expose it as a polled TAG.

## Energy and diagnostic boundary

The manuals document Type 51 Energy Reporting, including Active Energy (`0..281474976710653 Wh`, offset +40) and Active Power (`0..4294967293 W`, offset +42), as well as Type 52 diagnostics. Type 52 Control Gear Failure is included as six read-only Boolean bit TAGs.

The Type 51 rows do not state an encoded Modbus width or word order per value. Active Energy's listed range needs more than two 16-bit words, while the next Active Power point starts two address units later. This package intentionally omits both Energy and Power TAGs until the vendor documents the wire packing clearly enough to select an existing TAG type without truncation or overlap. It does not claim Type 51 is unsupported by the gateway.

The manuals warn that the Type 8, Type 51, and Type 52 formulas differ for Intesis MAPS earlier than 1.2.24.0 or Modbus gateway firmware earlier than 2.0.1.0. Verify those versions before using the color or diagnostic addresses in this starter.

## Lifecycle and evidence

Ordinary Runtime/HMI operation is limited to the documented state, level, on/off, and configured-group CCT values above. DALI bus programming, short-address assignment, group association, reset, firmware, and gateway administration are outside this profile and must remain in the gateway's administration workflow. Existing lifecycle, Runtime, security, and diagnostics authorities remain in force.

The package and tests are fixtures. They verify schema import/export, stable TAG/Equipment/Capability bindings, address bounds, Modbus compilation, access direction, and the repeatability of an Engineering JSON roundtrip. They do not communicate with an Intesis gateway and do not establish physical interoperability. Physical L4 remains deferred until after Wave 16, partner disclosure, and a stable installation.
