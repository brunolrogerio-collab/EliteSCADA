def publish_current():
    current = read_tag("Plant.Pump.Current")
    if current is not None:
        publish_server_memory_sample("State.Pump.LastCurrent", current, "Uncertain")
