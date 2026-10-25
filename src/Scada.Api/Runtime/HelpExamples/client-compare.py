from elite_scada import tag_read, visual_property_write

async def on_click(event):
    current = await tag_read("Plant.Pump.Current")
    reference = await tag_read("Plant.Pump.ReferenceCurrent")
    color = "#808080"
    if current.quality == "Good" and reference.quality == "Good":
        if current.value is not None and reference.value is not None:
            if current.value > reference.value:
                color = "#FFB300"
            else:
                color = "#2384C6"
    await visual_property_write(event.visualObjectId, "fillColor", color)
