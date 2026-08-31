import signal
import struct
import threading

from snap7.server import Server
from snap7.type import SrvArea


stop_event = threading.Event()


def handle_signal(signum, frame):
    del signum, frame
    stop_event.set()


def build_db1() -> bytearray:
    data = bytearray(256)
    struct.pack_into(">h", data, 0, 1234)       # DB1.DBW0 INT
    struct.pack_into(">i", data, 4, 12345678)   # DB1.DBD4 DINT
    struct.pack_into(">f", data, 8, 21.5)       # DB1.DBD8 REAL
    data[12] = 0b00000001                         # DB1.DBX12.0 BOOL
    data[14] = 0xAB                               # DB1.DBB14 BYTE
    struct.pack_into(">H", data, 16, 0x1234)    # DB1.DBW16 WORD

    text = b"EliteSCADA Lab"
    data[20] = 32                                 # STRING max length
    data[21] = len(text)                          # STRING current length
    data[22 : 22 + len(text)] = text
    return data


def main() -> None:
    signal.signal(signal.SIGINT, handle_signal)
    signal.signal(signal.SIGTERM, handle_signal)

    server = Server(log=True)
    server.register_area(SrvArea.DB, 1, build_db1())
    server.register_area(SrvArea.MK, 0, bytearray(256))
    server.register_area(SrvArea.PE, 0, bytearray(256))
    server.register_area(SrvArea.PA, 0, bytearray(256))
    server.start(tcp_port=102)

    print("EliteSCADA S7 lab peer listening on 0.0.0.0:102; DB1 registered", flush=True)

    try:
        while not stop_event.wait(0.25):
            pass
    finally:
        server.stop()
        server.destroy()


if __name__ == "__main__":
    main()
