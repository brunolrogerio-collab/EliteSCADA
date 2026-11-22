def convert_flow():
    liters_per_second = read_tag("Plant.Pump.FlowLitersPerSecond")
    if liters_per_second is not None:
        cubic_meters_per_hour = liters_per_second * 3.6
        write_server_memory("State.Pump.FlowCubicMetersPerHour", cubic_meters_per_hour)
