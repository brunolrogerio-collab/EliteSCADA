import json
import os
import signal
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from bacpypes.app import BIPSimpleApplication
from bacpypes.core import run, stop
from bacpypes.local.device import LocalDeviceObject
from bacpypes.object import AnalogValueObject, BinaryValueObject
from bacpypes.service.cov import ChangeOfValueServices
from bacpypes.service.object import ReadWritePropertyMultipleServices


class LabApplication(BIPSimpleApplication, ReadWritePropertyMultipleServices, ChangeOfValueServices):
    pass


class HealthHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path != "/health":
            self.send_response(404)
            self.end_headers()
            return
        body = json.dumps({"status": "ok", "protocol": "bacnet-ip"}).encode("utf-8")
        self.send_response(200)
        self.send_header("content-type", "application/json")
        self.send_header("content-length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        del format, args


def start_health_server(port: int) -> ThreadingHTTPServer:
    server = ThreadingHTTPServer(("127.0.0.1", port), HealthHandler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server


def main() -> None:
    address = os.getenv("BACNET_ADDRESS", "127.0.0.1/8:47808")
    device_instance = int(os.getenv("BACNET_DEVICE_INSTANCE", "599001"))
    health_port = int(os.getenv("BACNET_HEALTH_PORT", "18080"))

    # Match the BACpypes mini_device server pattern deliberately.  The Device
    # Object owns protocol-services metadata; the application service mixins
    # advertise/handle the services they implement without a second manual
    # protocolServicesSupported authority.
    device = LocalDeviceObject(
        objectName="EliteSCADA Lab BACnet Peer",
        objectIdentifier=("device", device_instance),
        maxApduLengthAccepted=1024,
        segmentationSupported="noSegmentation",
        vendorIdentifier=999,
    )

    application = LabApplication(device, address)

    analog = AnalogValueObject(
        objectIdentifier=("analogValue", 1),
        objectName="Lab.AnalogValue1",
        presentValue=21.5,
        statusFlags=[0, 0, 0, 0],
        covIncrement=0.5,
        units="degreesCelsius",
    )
    binary = BinaryValueObject(
        objectIdentifier=("binaryValue", 1),
        objectName="Lab.BinaryValue1",
        presentValue="active",
        statusFlags=[0, 0, 0, 0],
    )
    application.add_object(analog)
    application.add_object(binary)

    health_server = start_health_server(health_port)

    def shutdown(signum, frame):
        del signum, frame
        health_server.shutdown()
        stop()

    signal.signal(signal.SIGINT, shutdown)
    signal.signal(signal.SIGTERM, shutdown)

    print(
        f"EliteSCADA BACnet/IP lab peer device={device_instance} address={address} "
        "objects=analogValue:1,binaryValue:1",
        flush=True,
    )

    try:
        run()
    finally:
        health_server.server_close()


if __name__ == "__main__":
    main()
