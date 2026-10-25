def calculate_ratio():
    flow = read_tag("Plant.Pump.Flow")
    reference = read_tag("Plant.Pump.ReferenceFlow")
    if flow is not None:
        if reference is not None:
            if reference != 0:
                ratio = flow / reference
                write_server_memory("State.Pump.FlowRatio", ratio)
