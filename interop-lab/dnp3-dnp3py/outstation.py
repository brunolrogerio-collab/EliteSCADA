import asyncio
import json
import threading

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
    def __init__(self, database: Database, commands: "L3CommandCapture") -> None:
        self._database = database
        self._commands = commands

    @staticmethod
    def _command_value(index: int, code: ControlCode) -> bool | None:
        if index != 3:
            return None
        if code in (ControlCode.LATCH_ON, ControlCode.PULSE_ON):
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
        if self._command_value(index, code) is None:
            return CommandResult.not_supported(f"Binary output {index} / {code} not supported")
        self._commands.append("select", index, code, count, on_time, off_time, None)
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
        value = self._command_value(index, code)
        if value is None:
            return CommandResult.not_supported(f"Binary output {index} / {code} not supported")
        self._commands.append("operate", index, code, count, on_time, off_time, select_sequence)
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
        value = self._command_value(index, code)
        if value is None:
            return CommandResult.not_supported(f"Binary output {index} / {code} not supported")
        self._commands.append("directOperate", index, code, count, on_time, off_time, None)
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


class L3CommandCapture:
    def __init__(self, database: Database) -> None:
        self._lock = threading.Lock()
        self._items: list[dict[str, int | str | None]] = []
        self._database = database

    def append(
        self,
        phase: str,
        index: int,
        code: ControlCode,
        count: int,
        on_time: int,
        off_time: int,
        select_sequence: int | None,
    ) -> None:
        item = {
            "phase": phase,
            "index": index,
            "operation": code.name,
            "count": count,
            "onTimeMilliseconds": on_time,
            "offTimeMilliseconds": off_time,
            "selectSequence": select_sequence,
        }
        with self._lock:
            self._items.append(item)

    def snapshot(self) -> list[dict[str, int | str | None]]:
        with self._lock:
            return list(self._items)

    def clear(self) -> None:
        with self._lock:
            self._items.clear()
        self._database.update_binary_output(3, value=False, quality=BinaryQuality.ONLINE)


def build_outstation(commands: L3CommandCapture, database: Database) -> Outstation:
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
        handler=L3CommandHandler(database, commands),
    )


async def main() -> None:
    database = Database()
    commands = L3CommandCapture(database)
    runner = OutstationTcpRunner(
        outstation=build_outstation(commands, database),
        host="0.0.0.0",
        port=20000,
    )

    async def handle_capture_request(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        try:
            request_line = await reader.readline()
            while await reader.readline() not in (b"\r\n", b"\n", b""):
                pass
            if request_line.startswith(b"GET /commands "):
                status = b"200 OK"
                payload = json.dumps(commands.snapshot(), separators=(",", ":")).encode("utf-8")
            elif request_line.startswith(b"POST /commands/reset "):
                commands.clear()
                status = b"200 OK"
                payload = b'{"cleared":true}'
            else:
                status = b"404 Not Found"
                payload = b"{}"
            writer.write(
                b"HTTP/1.1 " + status + b"\r\n"
                + b"Content-Type: application/json\r\n"
                + b"Cache-Control: no-store\r\n"
                + b"Connection: close\r\n"
                + b"Content-Length: " + str(len(payload)).encode("ascii") + b"\r\n\r\n"
                + payload
            )
            await writer.drain()
        finally:
            writer.close()
            await writer.wait_closed()

    capture_server = await asyncio.start_server(handle_capture_request, "0.0.0.0", 20001)
    async with capture_server:
        await runner.run()


if __name__ == "__main__":
    asyncio.run(main())
