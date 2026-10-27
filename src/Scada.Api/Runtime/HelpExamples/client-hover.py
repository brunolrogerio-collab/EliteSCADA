from elite_scada import visual_property_write, visual_property_clear

async def on_enter(event):
    await visual_property_write(event.visualObjectId, "opacity", 0.7)

async def on_leave(event):
    await visual_property_clear(event.visualObjectId, "opacity")
