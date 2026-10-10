import { EventEmitter } from 'node:events';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { runSidecar } from '../../src/sidecar.mjs';

function recordAudit(update) {
  const auditPath = process.env.ZIGBEE_TEST_AUDIT_PATH;
  const current = existsSync(auditPath) ? JSON.parse(readFileSync(auditPath, 'utf8')) : {};
  writeFileSync(auditPath, JSON.stringify({ ...current, ...update }));
}

function appendAudit(name, value) {
  const auditPath = process.env.ZIGBEE_TEST_AUDIT_PATH;
  const current = existsSync(auditPath) ? JSON.parse(readFileSync(auditPath, 'utf8')) : {};
  writeFileSync(auditPath, JSON.stringify({ ...current, [name]: [...(current[name] ?? []), value] }));
}

class FakeCoordinator extends EventEmitter {
  constructor(options) {
    super();
    this.database = {};
    this.onOff = false;
    const device = {
      ieeeAddr: '0x00124b0001abcdef',
      modelID: '01MINIZB',
      manufacturerName: 'SONOFF',
      type: 'Router',
      getEndpoint: (id) => id === 1 ? endpoint : undefined,
    };
    const endpoint = {
      read: async (cluster, attributes, readOptions) => {
        appendAudit('reads', { cluster, attributes, options: readOptions, value: this.onOff });
        return { onOff: this.onOff };
      },
      command: async (cluster, command, payload, commandOptions) => {
        appendAudit('commands', { cluster, command, payload, options: commandOptions });
        this.onOff = command === 'on';
        this.emit('message', {
          type: 'attributeReport',
          cluster,
          device,
          endpoint: { ID: 1 },
          data: { onOff: this.onOff },
        });
      },
    };
    this.device = device;
    this.adapter = {
      removeAllListeners: () => recordAudit({ adapterListenersRemoved: true }),
      stop: async () => recordAudit({ adapterStopped: true }),
    };
    recordAudit({
      networkKeyLength: options.network.networkKey.length,
      panId: options.network.panID,
      extendedPanId: Buffer.from(options.network.extendedPanID).toString('hex'),
      channelList: options.network.channelList,
      networkKeyDistribute: options.network.networkKeyDistribute,
      serialAdapter: options.serialPort.adapter,
      serialPath: options.serialPort.path,
      baudRate: options.serialPort.baudRate,
      rtscts: options.serialPort.rtscts,
      databasePath: options.databasePath,
    });
  }

  async start() {
    recordAudit({ started: true });
  }

  getDeviceByIeeeAddr(ieeeAddr) {
    return ieeeAddr.toLowerCase() === this.device.ieeeAddr.toLowerCase() ? this.device : undefined;
  }

  databaseSave() {
    recordAudit({ databaseSaved: true });
  }
}

class FakeZnpAdapterManager {
  async determineStrategy() { return 'startup'; }
  async addToGroup() {}
}

await runSidecar(process.env, {
  Controller: FakeCoordinator,
  ZnpAdapterManager: FakeZnpAdapterManager,
  setLogger() {},
});
