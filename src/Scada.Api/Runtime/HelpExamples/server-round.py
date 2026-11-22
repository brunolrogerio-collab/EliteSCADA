def on_timer():
    current = read_tag("Plant.Pump.Current")
    if current is not None:
        rounded = round(current, 2)
        write_server_memory("State.Pump.LastCurrent", rounded)
