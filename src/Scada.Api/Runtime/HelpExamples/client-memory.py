from elite_scada import tag_read, client_memory_write

async def remember_pressure(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample.quality == "Good":
        if sample.value is not None:
            await client_memory_write("UI.LastPressure", sample.value)
