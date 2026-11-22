from elite_scada import tag_read

async def on_click(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample.quality == "Good":
        pressure = sample.value
