import asyncio

from dnp3.core.flags import AnalogQuality, BinaryQuality, CounterQuality
from dnp3.database import AnalogInputConfig, BinaryInputConfig, CounterConfig, Database, EventClass
from dnp3.outstation import Outstation, OutstationConfig, OutstationTcpRunner


def build_outstation() -> Outstation:
    database = Database()
    database.add_binary_input(
        0,
        BinaryInputConfig(event_class=EventClass.CLASS_1),
        value=True,
        quality=BinaryQuality.ONLINE,
    )
    database.add_analog_input(
        0,
        AnalogInputConfig(event_class=EventClass.CLASS_2),
        value=4242.0,
        quality=AnalogQuality.ONLINE,
    )
    database.add_counter(
        0,
        CounterConfig(event_class=EventClass.CLASS_3),
        value=123456,
        quality=CounterQuality.ONLINE,
    )

    return Outstation(
        config=OutstationConfig(address=1024, master_address=1),
        database=database,
    )


async def main() -> None:
    runner = OutstationTcpRunner(
        outstation=build_outstation(),
        host="0.0.0.0",
        port=20000,
    )
    await runner.run()


if __name__ == "__main__":
    asyncio.run(main())
