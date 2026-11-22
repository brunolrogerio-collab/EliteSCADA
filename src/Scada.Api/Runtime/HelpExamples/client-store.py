from elite_scada import tag_read, client_memory_write

async def on_click(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample.quality == "Good":
        await client_memory_write("UI.LastPressure", sample.value)
