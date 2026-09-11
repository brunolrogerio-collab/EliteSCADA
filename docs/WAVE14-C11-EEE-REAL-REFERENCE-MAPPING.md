# W14-C11 — EEE Real Reference Mapping, Alarm/Event Separation and PLC Variant

**State:** BINDING C11 IMPLEMENTATION REFERENCE  
**Canonical product base:** `3fda88061df35ad14755d22881e5d3a9216d1ff5`  
**C11 implementation branch:** `wave14/c11-canonical-eee-demo`  
**Product Owner direction:** the new EliteSCADA EEE DEMO must use clean canonical names while preserving the supplied real Modbus addressing as the primary reference for a later PLC-connected variant.

## 1. Source material supplied by Product Owner

C11 implementation is grounded in the real EEE material supplied by the Product Owner during Wave 14 coordination:

- FvDesigner TAG export `tags(1).csv`;
- FvDesigner alarm configuration export `alarm.csv`;
- 14 real HMI captures covering:
  - Lay-out EEE;
  - Ajuste de níveis;
  - Alarmes;
  - Medidas elétricas;
  - Medidas dos instrumentos;
  - Entradas digitais;
  - Saídas digitais;
  - Entradas/Saídas analógicas;
  - Menu;
  - Gerador;
  - Horímetros/Totalizadores;
  - Escalas;
  - Ajuste de pressão;
  - Controle/Status das duas bombas.

The historical HMI is an engineering/process reference, **not** a pixel-for-pixel visual specification. The new DEMO must preserve the useful industrial semantics while presenting them through current EliteSCADA Engineering, Runtime, Dynamos, Popups, Trend, Alarm Browser, Event Browser, scripts, bindings and Commands.

## 2. Naming authority

Legacy FvDesigner names such as `B1_LIG`, `B1_DEF_INV`, `NV_M_ALTO` and `INV1_CORR` are **not** the canonical EliteSCADA application namespace.

The EliteSCADA DEMO uses readable semantic paths such as:

- `EEE.Well.Level` / `EEE.Well.LevelPercent`;
- `EEE.Pump01.Running`;
- `EEE.Pump01.Fault`;
- `EEE.Pump01.CommunicationFault`;
- `EEE.Pump01.Current`;
- `EEE.Pump01.Frequency`;
- `EEE.Pump02.*`;
- `EEE.Discharge.Pressure`;
- `EEE.Discharge.Flow`.

The real Modbus source name/address remains reconciliation metadata and the binding authority for the PLC variant.

## 3. Two-variant architecture

### 3.1 Canonical DEMO Simulation

The first accepted application is built only from ordinary generic product capabilities:

`Server Memory -> Server Script -> TAG/quality -> Alarm/Event/Historian -> HMI bindings/Dynamos/Popups/Trend/Browsers/Commands`

No EEE-specific Driver, service, backend route, hidden runtime state or frontend-generated process truth is permitted.

### 3.2 DEMO PLC / Modbus

After Simulation acceptance, create a second project/package using the real PLC addressing supplied by the Product Owner.

The design target is:

- same conceptual HMI;
- same Screen/Popup/Dynamo structure;
- same semantic HMI-facing TAG paths wherever practical;
- real Modbus Source/address definitions replacing simulation ownership;
- explicit normalisation only where the raw PLC bit/register semantics differ from the semantic HMI state;
- no duplicate hand-authored HMI.

Recommended PLC structure:

`real Modbus TAG -> optional generic normalization -> canonical semantic TAG -> same HMI`

When direct mapping is semantically exact, no normalization layer should be added merely for ceremony.

## 4. Do not guess scaling

The supplied exports prove register addresses and raw data types, but do not establish every engineering conversion/scaling rule.

Therefore:

- preserve the real address and raw type;
- mark unknown scaling as `TO VALIDATE WITH REAL PLC`;
- never invent current/frequency/voltage/pressure/level scale factors to make a screen look plausible;
- validate those conversions against the real PLC/application when the PLC variant is exercised.

The Simulation variant may use realistic engineering-unit values independently, because its purpose is deterministic product demonstration rather than byte-for-byte PLC emulation.

## 5. Alarm vs Operational Event authority

The historical `alarm.csv` mixes abnormal conditions with normal operating state transitions. EliteSCADA must separate them.

Rule:

- **Alarm** = abnormal condition requiring operator attention, acknowledgement/shelving semantics where applicable.
- **Operational Event** = process/operator/equipment state transition worth recording but not an abnormal condition by itself.
- **Audit** = security/accountability record and remains separate from both.

A running pump is not an alarm. A selector changing to Local is not intrinsically an alarm. Treating every state change as an alarm trains operators to ignore the alarm system, defeating the point of having one.

## 6. Historical alarm.csv disposition

| # | Legacy source | Real address | Legacy condition | Legacy message | EliteSCADA disposition |
|---:|---|---|---|---|---|
| 1 | `NV_BAIXO` | `@0:4x2005.0` | `=0` | NÍVEL BAIXO | **Alarm** — low level; preserve active-low polarity |
| 2 | `NV_ALTO` | `@0:4x2005.1` | `=1` | NÍVEL ALTO | **Alarm** — high level |
| 3 | `NV_M_ALTO` | `@0:4x2005.2` | `=1` | NÍVEL MUITO ALTO | **Alarm** — high-high level |
| 4 | `NV_EXTRAVASAO` | `@0:4x2005.6` | `=1` | EXTRAVASÃO | **Alarm** — critical overflow |
| 5 | `NV_M_BAIXO` | `@0:4x2005.7` | `=0` | NÍVEL MUITO BAIXO | **Alarm** — very-low level; preserve active-low polarity |
| 6 | `EMG_ACI` | `@0:4x2011.2` | `=1` | EMERGÊNCIA ACIONADA | **Alarm** — emergency |
| 7 | `UPS_ALARME` | `@0:4x2011.12` | `=0` | UPS EM ALARME | **Alarm** — preserve active-low polarity |
| 8 | `ERRO_EA01` | `@0:4x2009.0` | `=1` | ERRO NO CANAL ANALÓGICO EA 01 | **Alarm** |
| 9 | `ERRO_EA02` | `@0:4x2009.1` | `=1` | ERRO NO CANAL ANALÓGICO EA 02 | **Alarm** |
| 10 | `ERRO_EA03` | `@0:4x2009.2` | `=1` | ERRO NO CANAL ANALÓGICO EA 03 | **Alarm** |
| 11 | `ERRO_EA04` | `@0:4x2009.3` | `=1` | ERRO NO CANAL ANALÓGICO EA 04 | **Alarm** |
| 12 | `B1_DEF_INV` | `@0:4x2006.4` | `=0` | BOMBA 1 DEFEITO | **Alarm** — drive fault; preserve active-low/raw polarity |
| 13 | `SEL_AUT_B1` | `@0:4x2006.6` | `=0` | SELETORA EM LOCAL | **Operational Event** — Pump 1 mode changed to Local; Runtime status badge, not alarm |
| 14 | `B1_LIG` | `@0:4x2006.8` | `=1` | BOMBA 1 LIGADA | **Operational Event** — Pump 1 started; also emit symmetric Pump 1 stopped event |
| 15 | `ERRO_EA05` | `@0:4x2009.4` | `=1` | ERRO NO CANAL ANALÓGICO EA 05 | **Alarm** |
| 16 | `B2_DEF_INV` | `@0:4x2007.4` | `=0` | BOMBA 2 DEFEITO | **Alarm** — drive fault; preserve active-low/raw polarity |
| 17 | `B2_LIG` | `@0:4x2007.8` | `=1` | BOMBA 2 LIGADA | **Operational Event** — Pump 2 started; also emit symmetric Pump 2 stopped event |
| 18 | `NV_CESTO_ALTO` | `@0:4x2005.8` | `=1` | NÍVEL ALTO DO CESTO | **Alarm** |
| 19 | `PRESSAO_ALTA_GBM_01` | `@0:4x2005.9` | `=1` | PRESSÃO ALTA GMB 01 | **Alarm** |
| 20 | `PRESSAO_ALTA_GBM_02` | `@0:4x2005.10` | `=1` | PRESSÃO ALTA GMB 02 | **Alarm** |
| 21 | `PRESSAO_BAIXA_GBM_01` | `@0:4x2005.11` | `=1` | PRESSÃO BAIXA GMB 01 | **Alarm** |
| 22 | `PRESSAO_BAIXA_GBM_02` | `@0:4x2005.12` | `=1` | PRESSÃO BAIXA GMB 02 | **Alarm** |
| 23 | direct raw bit | `@0:4x2011.14` | `=0` | FALTA DE FASE GMB 01 | **Alarm** — preserve active-low polarity |
| 24 | direct raw bit | `@0:4x2011.15` | `=0` | FALTA DE FASE GMB 02 | **Alarm** — preserve active-low polarity |
| 25 | direct raw bit | `@0:4x2008.0` | `=1` | FALHA DE COMUNICAÇÃO INV 01 | **Alarm** — Pump 1 communication |
| 26 | direct raw bit | `@0:4x2008.1` | `=1` | FALHA DE COMUNICAÇÃO INV 02 | **Alarm** — Pump 2 communication |
| 27 | direct raw bit | `@0:4x2008.2` | `=1` | FALHA DE COMUNICAÇÃO GERADOR | **Alarm** |
| 28 | direct raw bit | `@0:4x2008.3` | `=1` | FALHA DE COMUNICAÇÃO MME | **Alarm** |

The real export also contains `SEL_AUT_B2` at `@0:4x2007.6`. Even though the historical alarm table does not create the corresponding record, the new application should treat Pump 2 Local/Automatic transitions symmetrically as Operational Events/status.

Likewise, `GERADOR_OP`, `CH_REDE` and `CH_GERADOR` represent operating/source state and should be modeled as status/Operational Events rather than normal alarms unless a distinct abnormal condition exists.

## 7. Core PLC mapping for the second variant

The following are the minimum real-world signals around which the HMI architecture must remain compatible.

### 7.1 Wet well / process

| Semantic purpose | Legacy source | Address | Raw type | PLC-variant note |
|---|---|---|---|---|
| Wet well level raw | `NIVEL_INT` | `@0:4x2031` | 16Bit-INT | Required. Engineering scale **TO VALIDATE WITH REAL PLC** |
| Low level switch/state | `NV_BAIXO` | `@0:4x2005.0` | Bit | Historical alarm active at 0 |
| High level | `NV_ALTO` | `@0:4x2005.1` | Bit | Active high |
| High-high level | `NV_M_ALTO` | `@0:4x2005.2` | Bit | Active high |
| Overflow | `NV_EXTRAVASAO` | `@0:4x2005.6` | Bit | Active high |
| Very-low level | `NV_M_BAIXO` | `@0:4x2005.7` | Bit | Historical alarm active at 0 |
| Basket high level | `NV_CESTO_ALTO` | `@0:4x2005.8` | Bit | Active high |
| Very-low setpoint | `P_NIVEL_M_BAIXO` | `@0:4xD2106` | 32Bit-FLOAT | Engineering value supplied by PLC |
| Low setpoint | `P_NIVEL_BAIXO` | `@0:4xD2108` | 32Bit-FLOAT | Engineering value supplied by PLC |
| High setpoint | `P_NIVEL_ALTO` | `@0:4xD2110` | 32Bit-FLOAT | Engineering value supplied by PLC |
| High-high setpoint | `P_NIVEL_M_ALTO` | `@0:4xD2112` | 32Bit-FLOAT | Engineering value supplied by PLC |
| Overflow setpoint | `P_NIVEL_EXTRAVASAO` | `@0:4xD2114` | 32Bit-FLOAT | Engineering value supplied by PLC |

### 7.2 Pump 1

| Semantic purpose | Legacy source | Address | Raw type | PLC-variant note |
|---|---|---|---|---|
| Running | `B1_LIG` | `@0:4x2006.8` | Bit | Direct positive state; start/stop become Operational Events |
| Local/Automatic | `SEL_AUT_B1` | `@0:4x2006.6` | Bit | 1 = Automatic per source comment; 0 = Local |
| Motor protection/PTC | `B1_PTC` | `@0:4x2006.2` | Bit | Abnormal/protection signal; exact reset semantics to validate |
| Drive defect raw | `B1_DEF_INV` | `@0:4x2006.4` | Bit | Historical alarm active at 0; normalize before exposing semantic `Fault` |
| Drive communication failure | raw status word bit | `@0:4x2008.0` | Bit | Active high |
| Current raw | `INV1_CORR` | `@0:4x2041` | 16Bit-INT | Scale **TO VALIDATE WITH REAL PLC** |
| Frequency raw | `INV1_FREQ` | `@0:4x2042` | 16Bit-INT | Scale **TO VALIDATE WITH REAL PLC** |
| Voltage raw | `INV1_TENS` | `@0:4x2043` | 16Bit-INT | Scale **TO VALIDATE WITH REAL PLC** |
| Operating hours | `SUP_HOR_B1` | `@0:4x2040` | 16Bit-INT | Unit/rollover **TO VALIDATE WITH REAL PLC** |
| Fault hours | `SUP_HORDEF_B1` | `@0:4x2046` | 16Bit-INT | Unit/rollover **TO VALIDATE WITH REAL PLC** |
| Starts/count | `SUP_QTD_B1` | `@0:4x2047` | 16Bit-INT | Counter semantics to validate |
| Pressure high | `PRESSAO_ALTA_GBM_01` | `@0:4x2005.9` | Bit | Alarm |
| Pressure low | `PRESSAO_BAIXA_GBM_01` | `@0:4x2005.11` | Bit | Alarm |

### 7.3 Pump 2

| Semantic purpose | Legacy source | Address | Raw type | PLC-variant note |
|---|---|---|---|---|
| Running | `B2_LIG` | `@0:4x2007.8` | Bit | Direct positive state; start/stop become Operational Events |
| Local/Automatic | `SEL_AUT_B2` | `@0:4x2007.6` | Bit | 1 = Automatic per source comment; 0 = Local |
| Motor protection/PTC | `B2_PTC` | `@0:4x2007.2` | Bit | Abnormal/protection signal |
| Drive defect raw | `B2_DEF_INV` | `@0:4x2007.4` | Bit | Historical alarm active at 0; normalize before semantic `Fault` |
| Drive communication failure | raw status word bit | `@0:4x2008.1` | Bit | Active high |
| Current raw | `INV2_CORR` | `@0:4x2051` | 16Bit-INT | Scale **TO VALIDATE WITH REAL PLC** |
| Frequency raw | `INV2_FREQ` | `@0:4x2052` | 16Bit-INT | Scale **TO VALIDATE WITH REAL PLC** |
| Voltage raw | `INV2_TENS` | `@0:4x2053` | 16Bit-INT | Scale **TO VALIDATE WITH REAL PLC** |
| Operating hours | `SUP_HOR_B2` | `@0:4x2050` | 16Bit-INT | Unit/rollover **TO VALIDATE WITH REAL PLC** |
| Fault hours | `SUP_HORDEF_B2` | `@0:4x2056` | 16Bit-INT | Unit/rollover **TO VALIDATE WITH REAL PLC** |
| Starts/count | `SUP_QTD_B2` | `@0:4x2057` | 16Bit-INT | Counter semantics to validate |
| Pressure high | `PRESSAO_ALTA_GBM_02` | `@0:4x2005.10` | Bit | Alarm |
| Pressure low | `PRESSAO_BAIXA_GBM_02` | `@0:4x2005.12` | Bit | Alarm |

### 7.4 Electrical metering and generator

The supplied export contains real electrical-meter registers `@0:4x2019` through `@0:4x2030` (`SUP_STS_ME01_*`) for phase currents, voltages, power, power factor and frequency. Exact scaling is not established by the CSV and remains a PLC-validation item.

Generator references include:

- `GERADOR_OP` `@0:4x2010.2`;
- `CH_REDE` `@0:4x2010.0`;
- `CH_GERADOR` `@0:4x2010.1`;
- generator current registers `@0:4x2084`, `2086`, `2088`;
- generator voltage registers `@0:4x2090`, `2092`, `2094`;
- generator level/speed/battery registers `@0:4x2080`, `2081`, `2082`;
- generator communication failure `@0:4x2008.2`.

The canonical Simulation DEMO may represent the generator/support system only to the level useful for the product demonstration. The PLC variant mapping must preserve these real references if that screen is included.

## 8. PLC command safety boundary

The supplied TAG export contains command-like addresses such as `CMD_HAB_B1`, reset/hourmeter and pressure-protection controls. Their exact write semantics, pulse/latch behavior, permissives and safety expectations are **not proven by the source material alone**.

Therefore C11 must not infer or enable real PLC writes from names alone.

For the PLC variant:

1. first prove read-only observation of the real station state;
2. validate command semantics against the PLC logic/address contract;
3. only then map selected EliteSCADA Commands through the normal authenticated/authorized Command path;
4. never bypass PLC interlocks or write directly from a visual object.

## 9. Visual information architecture derived from the real HMI

### 9.1 Main screen — `EEE Principal`

The historical `LAY-OUT EEE` supplies the process topology, but the new EliteSCADA screen should be substantially cleaner and more operator-oriented.

Main visual hierarchy:

1. large wet-well/process graphic as the primary visual anchor;
2. animated liquid level using canonical `AnalogFillEngineeringDto` / ordinary property binding;
3. Pump 1 and Pump 2 as **two instances of the same reusable Dynamo**;
4. discharge piping/header with flow and pressure KPIs;
5. pump current/frequency near each pump rather than hidden on a separate page;
6. explicit Auto/Local, Running/Stopped, Fault/Trip and Quality text/symbol state, not color alone;
7. compact active-alarm/operational-event summary and navigation to full Browsers;
8. contextual Pump Popups opened from each Dynamo.

### 9.2 Pump Popups

The historical `CONTROLE/STATUS` content becomes contextual Pump Popups rather than forcing operators onto a separate duplicate page.

Each Pump Popup should expose:

- Running/Stopped;
- Available/Unavailable;
- Auto/Local where available;
- Fault/Trip/communication quality;
- current;
- frequency;
- pressure/flow where appropriate;
- runtime/starter counters where useful;
- permitted Commands;
- command feedback/state.

The two Popup instances/definitions must follow the same functional contract as the two Pump Dynamos.

### 9.3 Secondary screens

Recommended canonical Runtime structure:

- **EEE Principal** — process overview;
- **Instrumentação** — level, pressure, flow, setpoints and instrument health;
- **Sistema Elétrico** — electrical meter, pump electrical data, generator/support status;
- **Operação** — operating modes, counters, selected controls/setpoints appropriate to an operator;
- **Tendências** — Multi-Pen real historian Trend;
- **Alarmes e Eventos** — separate Alarm Browser and Operational Event Browser.

The historical raw Digital Input/Output and Analog I/O pages are primarily evidence for Engineering/Diagnostics and PLC mapping. They should not automatically become permanent operator screens merely because the old HMI had them.

## 10. Visual language

The canonical DEMO should look like a current product demonstration, not a replica of the grey FvDesigner interface.

Required direction:

- strong process hierarchy and generous spacing;
- dark industrial canvas is acceptable, with clear contrast and restrained accent use;
- authored HMI content must remain legible independent of application shell Dark/Light theme;
- states must use icon/text/shape as well as color;
- fault/quality precedence must be obvious;
- no ornamental animation that obscures process state;
- PNG/project assets may be used for polished static artwork/backgrounds, but live state must remain driven by normal bindings/Analog Fill/Dynamo properties, never image swaps or DOM hacks that bypass Engineering.

## 11. C11 acceptance consequences

C11 is not complete until the DEMO proves, in the integrated application:

- visibly changing wet-well Analog Fill;
- two independent instances of the same Pump Dynamo with independent TAG parameters;
- process values changing over time;
- Pump start/stop as Operational Events, not alarms;
- actual alarm transitions for faults/levels/quality;
- bad/non-Good quality visible on affected equipment;
- Trend from real Historian data;
- separate Alarm Browser and Event Browser;
- contextual Pump Popups and canonical Commands;
- fixed logical Runtime scaling/navigation without HMI reflow;
- a package exportable/reimportable through the ordinary product lifecycle;
- an architecture that can be remapped to the real Modbus addresses above without recreating the HMI.

Fresh Codespace Product Owner homologation remains intentionally **after** the canonical EEE DEMO is created. That later proof will validate the assembled experience rather than isolated pre-DEMO parts.