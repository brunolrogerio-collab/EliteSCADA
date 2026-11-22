def update_pressure_state():
    pressure = read_tag("Plant.Pump.DischargePressure")
    if pressure is not None:
        if pressure >= 8:
            write_server_memory("State.Pump.HighPressure", True)
        elif pressure <= 7:
            write_server_memory("State.Pump.HighPressure", False)
