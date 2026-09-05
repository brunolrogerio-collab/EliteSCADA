import {
  buildEeeFoundationPackage as buildFoundationEntities,
  EEE_IDS,
  EEE_PATHS,
  EEE_PROJECT_KEY,
  EEE_PROJECT_NAME
} from './c11-eee-demo-foundation';

export { EEE_IDS, EEE_PATHS, EEE_PROJECT_KEY, EEE_PROJECT_NAME };

/**
 * Canonical C11 foundation composition.
 *
 * The Engineering entities remain authored by c11-eee-demo-foundation.ts. This
 * composition replaces only the Server Script source so each 1 s handler keeps
 * its canonical TAG/event replay comfortably inside the product's normal 250 ms
 * execution budget. Discrete process truth is committed first; derived analog
 * telemetry converges in bounded groups on subsequent deterministic ticks.
 */
export function buildEeeFoundationPackage(base: any): any {
  const packageData = buildFoundationEntities(base);
  packageData.scripts = (packageData.scripts ?? []).map((script: any) =>
    script.id === EEE_IDS.script
      ? {
          ...script,
          source: buildCanonicalServerScriptSource(),
          metadata: {
            ...(script.metadata ?? {}),
            writeStrategy: 'bounded-on-change',
            derivedTelemetry: 'deterministic-convergence'
          }
        }
      : script);
  return packageData;
}

function buildCanonicalServerScriptSource(): string {
  const t = EEE_IDS.tags;
  const e = EEE_IDS.events;
  return [
    'def on_initialize():',
    `    if read_server_memory("${t.cmdAutoEnable}"):` , `        write_server_memory("${t.cmdAutoEnable}", False)`,
    `    if read_server_memory("${t.cmdAutoDisable}"):` , `        write_server_memory("${t.cmdAutoDisable}", False)`,
    `    if read_server_memory("${t.cmdP01Start}"):` , `        write_server_memory("${t.cmdP01Start}", False)`,
    `    if read_server_memory("${t.cmdP01Stop}"):` , `        write_server_memory("${t.cmdP01Stop}", False)`,
    `    if read_server_memory("${t.cmdP02Start}"):` , `        write_server_memory("${t.cmdP02Start}", False)`,
    `    if read_server_memory("${t.cmdP02Stop}"):` , `        write_server_memory("${t.cmdP02Stop}", False)`,
    `    if read_server_memory("${t.cmdResetFaults}"):` , `        write_server_memory("${t.cmdResetFaults}", False)`,
    `    if read_server_memory("${t.cmdInjectP01Fault}"):` , `        write_server_memory("${t.cmdInjectP01Fault}", False)`,
    `    if read_server_memory("${t.cmdInjectP02Fault}"):` , `        write_server_memory("${t.cmdInjectP02Fault}", False)`,
    `    if read_server_memory("${t.cmdHighDemandEnable}"):` , `        write_server_memory("${t.cmdHighDemandEnable}", False)`,
    `    if read_server_memory("${t.cmdHighDemandDisable}"):` , `        write_server_memory("${t.cmdHighDemandDisable}", False)`,
    `    if read_server_memory("${t.cmdBadQualityEnable}"):` , `        write_server_memory("${t.cmdBadQualityEnable}", False)`,
    `    if read_server_memory("${t.cmdBadQualityDisable}"):` , `        write_server_memory("${t.cmdBadQualityDisable}", False)`,
    `    p01_pressure = read_server_memory("${t.p01PressureBar}")`,
    `    write_server_memory("${t.p01PressureBar}", p01_pressure)`, '',

    'def on_tick():',
    `    level = read_server_memory("${t.levelPct}")`,
    `    old_inflow = read_server_memory("${t.inflowM3h}")`,
    `    old_total_flow = read_server_memory("${t.totalFlowM3h}")`,
    `    old_station_pressure = read_server_memory("${t.dischargePressureBar}")`,
    `    auto_mode = read_server_memory("${t.autoMode}")`,
    `    high_demand = read_server_memory("${t.highDemand}")`,
    `    bad_quality = read_server_memory("${t.badQualityScenario}")`,
    `    duty = read_server_memory("${t.dutyPump}")`,
    `    cycles = read_server_memory("${t.cycleCount}")`,
    `    p01_running = read_server_memory("${t.p01Running}")`,
    `    p02_running = read_server_memory("${t.p02Running}")`,
    `    p01_available = read_server_memory("${t.p01Available}")`,
    `    p02_available = read_server_memory("${t.p02Available}")`,
    `    p01_fault = read_server_memory("${t.p01Fault}")`,
    `    p02_fault = read_server_memory("${t.p02Fault}")`,
    `    p01_trip = read_server_memory("${t.p01Trip}")`,
    `    p02_trip = read_server_memory("${t.p02Trip}")`,
    `    old_p01_frequency = read_server_memory("${t.p01FrequencyHz}")`,
    `    old_p01_current = read_server_memory("${t.p01CurrentA}")`,
    `    old_p01_flow = read_server_memory("${t.p01FlowM3h}")`,
    `    old_p01_pressure = read_server_memory("${t.p01PressureBar}")`,
    `    old_p02_frequency = read_server_memory("${t.p02FrequencyHz}")`,
    `    old_p02_current = read_server_memory("${t.p02CurrentA}")`,
    `    old_p02_flow = read_server_memory("${t.p02FlowM3h}")`,
    `    old_p02_pressure = read_server_memory("${t.p02PressureBar}")`,
    `    cmd_auto_enable = read_server_memory("${t.cmdAutoEnable}")`,
    `    cmd_auto_disable = read_server_memory("${t.cmdAutoDisable}")`,
    `    cmd_p01_start = read_server_memory("${t.cmdP01Start}")`,
    `    cmd_p01_stop = read_server_memory("${t.cmdP01Stop}")`,
    `    cmd_p02_start = read_server_memory("${t.cmdP02Start}")`,
    `    cmd_p02_stop = read_server_memory("${t.cmdP02Stop}")`,
    `    cmd_reset_faults = read_server_memory("${t.cmdResetFaults}")`,
    `    cmd_inject_p01_fault = read_server_memory("${t.cmdInjectP01Fault}")`,
    `    cmd_inject_p02_fault = read_server_memory("${t.cmdInjectP02Fault}")`,
    `    cmd_high_demand_enable = read_server_memory("${t.cmdHighDemandEnable}")`,
    `    cmd_high_demand_disable = read_server_memory("${t.cmdHighDemandDisable}")`,
    `    cmd_bad_quality_enable = read_server_memory("${t.cmdBadQualityEnable}")`,
    `    cmd_bad_quality_disable = read_server_memory("${t.cmdBadQualityDisable}")`,

    '    old_level = level',
    '    old_auto_mode = auto_mode',
    '    old_high_demand = high_demand',
    '    old_bad_quality = bad_quality',
    '    old_duty = duty',
    '    old_cycles = cycles',
    '    old_p01_running = p01_running',
    '    old_p02_running = p02_running',
    '    old_p01_fault = p01_fault',
    '    old_p02_fault = p02_fault',
    '    old_p01_trip = p01_trip',
    '    old_p02_trip = p02_trip',

    '    if cmd_auto_enable:', '        auto_mode = True',
    '    if cmd_auto_disable:', '        auto_mode = False',
    '    if cmd_high_demand_enable:', '        high_demand = True',
    '    if cmd_high_demand_disable:', '        high_demand = False',
    '    if cmd_bad_quality_enable:', '        bad_quality = True',
    '    if cmd_bad_quality_disable:', '        bad_quality = False',
    '    if cmd_inject_p01_fault:', '        p01_fault = True', '        p01_trip = True',
    '    if cmd_inject_p02_fault:', '        p02_fault = True', '        p02_trip = True',
    '    if cmd_reset_faults:', '        p01_fault = False', '        p02_fault = False', '        p01_trip = False', '        p02_trip = False',

    '    if auto_mode:',
    '        if level >= 65.0 and not p01_running and not p02_running:',
    '            if duty == 1:',
    '                if p01_available and not p01_fault and not p01_trip:', '                    p01_running = True',
    '                else:', '                    if p02_available and not p02_fault and not p02_trip:', '                        p02_running = True',
    '            else:',
    '                if p02_available and not p02_fault and not p02_trip:', '                    p02_running = True',
    '                else:', '                    if p01_available and not p01_fault and not p01_trip:', '                        p01_running = True',
    '        if level >= 80.0 or high_demand:',
    '            if p01_running and not p02_running and p02_available and not p02_fault and not p02_trip:', '                p02_running = True',
    '            if p02_running and not p01_running and p01_available and not p01_fault and not p01_trip:', '                p01_running = True',
    '        if level <= 35.0 and (p01_running or p02_running):',
    '            p01_running = False', '            p02_running = False',
    '            cycles = cycles + 1',
    '            if duty == 1:', '                duty = 2', '            else:', '                duty = 1',
    '    else:',
    '        if cmd_p01_start and p01_available and not p01_fault and not p01_trip:', '            p01_running = True',
    '        if cmd_p01_stop:', '            p01_running = False',
    '        if cmd_p02_start and p02_available and not p02_fault and not p02_trip:', '            p02_running = True',
    '        if cmd_p02_stop:', '            p02_running = False',
    '    if p01_fault or p01_trip or not p01_available:', '        p01_running = False',
    '    if p02_fault or p02_trip or not p02_available:', '        p02_running = False',

    '    p01_flow = 0.0', '    p02_flow = 0.0',
    '    p01_frequency = 0.0', '    p02_frequency = 0.0',
    '    p01_current = 0.0', '    p02_current = 0.0',
    '    station_pressure = 0.0',
    '    if p01_running and p02_running:',
    '        p01_flow = 35.0', '        p02_flow = 35.0',
    '        p01_frequency = 45.0', '        p02_frequency = 45.0',
    '        p01_current = 20.0', '        p02_current = 20.0',
    '        station_pressure = 3.0',
    '    else:',
    '        if p01_running:', '            p01_flow = 38.0', '            p01_frequency = 48.0', '            p01_current = 22.0', '            station_pressure = 2.6',
    '        if p02_running:', '            p02_flow = 38.0', '            p02_frequency = 48.0', '            p02_current = 22.0', '            station_pressure = 2.6',
    '    total_flow = p01_flow + p02_flow',
    '    inflow = 20.0',
    '    if high_demand:', '        inflow = 55.0',
    '    level = level + ((inflow - total_flow) * 0.03)',
    '    if level < 0.0:', '        level = 0.0',
    '    if level > 100.0:', '        level = 100.0',
    '    p01_pressure = station_pressure if p01_running else 0.0',
    '    p02_pressure = station_pressure if p02_running else 0.0',

    '    state_changed = old_auto_mode != auto_mode or old_high_demand != high_demand or old_bad_quality != bad_quality',
    '    state_changed = state_changed or old_duty != duty or old_cycles != cycles',
    '    state_changed = state_changed or old_p01_running != p01_running or old_p02_running != p02_running',
    '    state_changed = state_changed or old_p01_fault != p01_fault or old_p02_fault != p02_fault',
    '    state_changed = state_changed or old_p01_trip != p01_trip or old_p02_trip != p02_trip',

    // Commit discrete authority before counters and request acknowledgements. If
    // a host timeout ever occurs, a partially applied stop cannot count twice.
    '    if old_auto_mode != auto_mode:', `        write_server_memory("${t.autoMode}", auto_mode)`,
    '    if old_high_demand != high_demand:', `        write_server_memory("${t.highDemand}", high_demand)`,
    '    if old_bad_quality != bad_quality:', `        write_server_memory("${t.badQualityScenario}", bad_quality)`,
    '    if old_p01_fault != p01_fault:', `        write_server_memory("${t.p01Fault}", p01_fault)`,
    '    if old_p01_trip != p01_trip:', `        write_server_memory("${t.p01Trip}", p01_trip)`,
    '    if old_p02_fault != p02_fault:', `        write_server_memory("${t.p02Fault}", p02_fault)`,
    '    if old_p02_trip != p02_trip:', `        write_server_memory("${t.p02Trip}", p02_trip)`,
    '    if old_p01_running != p01_running:', `        write_server_memory("${t.p01Running}", p01_running)`,
    '    if old_p02_running != p02_running:', `        write_server_memory("${t.p02Running}", p02_running)`,
    '    if old_duty != duty:', `        write_server_memory("${t.dutyPump}", duty)`,
    '    if old_cycles != cycles:', `        write_server_memory("${t.cycleCount}", cycles)`,

    '    if cmd_auto_enable:', `        write_server_memory("${t.cmdAutoEnable}", False)`,
    '    if cmd_auto_disable:', `        write_server_memory("${t.cmdAutoDisable}", False)`,
    '    if cmd_p01_start:', `        write_server_memory("${t.cmdP01Start}", False)`,
    '    if cmd_p01_stop:', `        write_server_memory("${t.cmdP01Stop}", False)`,
    '    if cmd_p02_start:', `        write_server_memory("${t.cmdP02Start}", False)`,
    '    if cmd_p02_stop:', `        write_server_memory("${t.cmdP02Stop}", False)`,
    '    if cmd_reset_faults:', `        write_server_memory("${t.cmdResetFaults}", False)`,
    '    if cmd_inject_p01_fault:', `        write_server_memory("${t.cmdInjectP01Fault}", False)`,
    '    if cmd_inject_p02_fault:', `        write_server_memory("${t.cmdInjectP02Fault}", False)`,
    '    if cmd_high_demand_enable:', `        write_server_memory("${t.cmdHighDemandEnable}", False)`,
    '    if cmd_high_demand_disable:', `        write_server_memory("${t.cmdHighDemandDisable}", False)`,
    '    if cmd_bad_quality_enable:', `        write_server_memory("${t.cmdBadQualityEnable}", False)`,
    '    if cmd_bad_quality_disable:', `        write_server_memory("${t.cmdBadQualityDisable}", False)`,

    // Quality transitions are state semantics, not deferred cosmetic telemetry.
    '    if old_bad_quality != bad_quality:',
    '        if bad_quality:', `            publish_server_memory_sample("${t.p01PressureBar}", p01_pressure, "Unavailable")`,
    '        else:', `            write_server_memory("${t.p01PressureBar}", p01_pressure)`,

    // Derived process telemetry is intentionally bounded to one dirty group per
    // quiet tick so a transition cannot overflow the canonical handler budget.
    '    if not state_changed:',
    '        process_group_dirty = old_inflow != inflow or old_total_flow != total_flow or old_station_pressure != station_pressure',
    '        p01_group_dirty = old_p01_frequency != p01_frequency or old_p01_current != p01_current or old_p01_flow != p01_flow or old_p01_pressure != p01_pressure',
    '        p02_group_dirty = old_p02_frequency != p02_frequency or old_p02_current != p02_current or old_p02_flow != p02_flow or old_p02_pressure != p02_pressure',
    '        if process_group_dirty:',
    '            if old_inflow != inflow:', `                write_server_memory("${t.inflowM3h}", inflow)`,
    '            if old_total_flow != total_flow:', `                write_server_memory("${t.totalFlowM3h}", total_flow)`,
    '            if old_station_pressure != station_pressure:', `                write_server_memory("${t.dischargePressureBar}", station_pressure)`,
    '        else:',
    '            if p01_group_dirty:',
    '                if old_p01_frequency != p01_frequency:', `                    write_server_memory("${t.p01FrequencyHz}", p01_frequency)`,
    '                if old_p01_current != p01_current:', `                    write_server_memory("${t.p01CurrentA}", p01_current)`,
    '                if old_p01_flow != p01_flow:', `                    write_server_memory("${t.p01FlowM3h}", p01_flow)`,
    '                if old_p01_pressure != p01_pressure:',
    '                    if bad_quality:', `                        publish_server_memory_sample("${t.p01PressureBar}", p01_pressure, "Unavailable")`,
    '                    else:', `                        write_server_memory("${t.p01PressureBar}", p01_pressure)`,
    '            else:',
    '                if p02_group_dirty:',
    '                    if old_p02_frequency != p02_frequency:', `                        write_server_memory("${t.p02FrequencyHz}", p02_frequency)`,
    '                    if old_p02_current != p02_current:', `                        write_server_memory("${t.p02CurrentA}", p02_current)`,
    '                    if old_p02_flow != p02_flow:', `                        write_server_memory("${t.p02FlowM3h}", p02_flow)`,
    '                    if old_p02_pressure != p02_pressure:', `                        write_server_memory("${t.p02PressureBar}", p02_pressure)`,

    // Level remains the continuous process variable and historian source.
    '    if old_level != level:', `        write_server_memory("${t.levelPct}", level)`,

    '    if old_p01_running != p01_running:',
    '        if p01_running:', `            emit_operational_event("${e.pumpStarted}", "P01 iniciada", {"pump": "P01"})`,
    '        else:', `            emit_operational_event("${e.pumpStopped}", "P01 parada", {"pump": "P01"})`,
    '    if old_p02_running != p02_running:',
    '        if p02_running:', `            emit_operational_event("${e.pumpStarted}", "P02 iniciada", {"pump": "P02"})`,
    '        else:', `            emit_operational_event("${e.pumpStopped}", "P02 parada", {"pump": "P02"})`,
    '    if not old_p01_fault and p01_fault:', `        emit_operational_event("${e.faultInjected}", "Falha P01 injetada", {"pump": "P01"})`,
    '    if not old_p02_fault and p02_fault:', `        emit_operational_event("${e.faultInjected}", "Falha P02 injetada", {"pump": "P02"})`,
    '    if old_p01_fault and not p01_fault:', `        emit_operational_event("${e.faultReset}", "Falha P01 resetada", {"pump": "P01"})`,
    '    if old_p02_fault and not p02_fault:', `        emit_operational_event("${e.faultReset}", "Falha P02 resetada", {"pump": "P02"})`,
    '    if old_duty != duty:', `        emit_operational_event("${e.dutyChanged}", "Bomba de vez alterada", {"duty": str(duty)})`,
    '    if old_auto_mode != auto_mode:', `        emit_operational_event("${e.modeChanged}", "Modo de operação alterado", {"auto": str(auto_mode)})`,
    '    if old_high_demand != high_demand:', `        emit_operational_event("${e.highDemandChanged}", "Cenário de alta demanda alterado", {"enabled": str(high_demand)})`,
    '    if old_bad_quality != bad_quality:', `        emit_operational_event("${e.qualityScenarioChanged}", "Cenário de qualidade alterado", {"enabled": str(bad_quality)})`, ''
  ].join('\n');
}
