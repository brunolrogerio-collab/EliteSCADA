import asyncio

from dnp3.core.enums import ControlCode
from dnp3.core.flags import AnalogQuality, BinaryQuality, CounterQuality
from dnp3.database import (
    AnalogInputConfig,
    BinaryInputConfig,
    BinaryOutputConfig,
    CounterConfig,
    Database,
    EventClass,
)
from dnp3.outstation import (
    CommandResult,
    DefaultCommandHandler,
    Outstation,
    OutstationConfig,
    OutstationTcpRunner,
)


class L3CommandHandler(DefaultCommandHandler):
    def __init__(self, database: Database) -> None:
        self._database = database

    @staticmethod
    def _command_value(index: int, code: ControlCode) -> bool | None:
        if index != 3:
            return None
        if code == ControlCode.LATCH_ON:
            return True
        if code == ControlCode.LATCH_OFF:
            return False
        return None

    def select_binary_output(
        self,
        index: int,
        code: ControlCode,
        count: int,
        on_time: int,
        off_time: int,
    ) -> CommandResult:
        del count, on_time, off_time
        if self._command_value(index, code) is None:
            return CommandResult.not_supported(f"Binary output {index} / {code} not supported")
        return CommandResult.success("L3 binary output selection accepted")

    def operate_binary_output(
        self,
        index: int,
        code: ControlCode,
        count: int,
        on_time: int,
        off_time: int,
        select_sequence: int,
    ) -> CommandResult:
        del count, on_time, off_time, select_sequence
        value = self._command_value(index, code)
        if value is None:
            return CommandResult.not_supported(f"Binary output {index} / {code} not supported")
        self._database.update_binary_output(index, value=value, quality=BinaryQuality.ONLINE)
        return CommandResult.success("L3 binary output operated")

    def direct_operate_binary_output(
        self,
        index: int,
        code: ControlCode,
        count: int,
        on_time: int,
        off_time: int,
    ) -> CommandResult:
        del count, on_time, off_time
        value = self._command_value(index, code)
        if value is None:
            return CommandResult.not_supported(f"Binary output {index} / {code} not supported")
        self._database.update_binary_output(index, value=value, quality=BinaryQuality.ONLINE)
        return CommandResult.success("L3 binary output directly operated")

    def select_analog_output(self, index: int, value: float) -> CommandResult:
        del value
        if index != 5:
            return CommandResult.not_supported(f"Analog output {index} not supported")
        return CommandResult.success("L3 analog output selection accepted")

    def operate_analog_output(
        self,
        index: int,
        value: float,
        select_sequence: int,
    ) -> CommandResult:
        del value, select_sequence
        if index != 5:
            return CommandResult.not_supported(f"Analog output {index} not supported")
        return CommandResult.success("L3 analog output operated")

    def direct_operate_analog_output(self, index: int, value: float) -> CommandResult:
        del value
        if index != 5:
            return CommandResult.not_supported(f"Analog output {index} not supported")
        return CommandResult.success("L3 analog output directly operated")


def build_outstation() -> Outstation:
    database = Database()
    database.add_binary_input(
        0,
        BinaryInputConfig(event_class=EventClass.CLASS_1),
        value=True,
        quality=BinaryQuality.ONLINE,
    )
    database.add_binary_output(
        3,
        BinaryOutputConfig(event_class=EventClass.CLASS_1),
        value=False,
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
        handler=L3CommandHandler(database),
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
