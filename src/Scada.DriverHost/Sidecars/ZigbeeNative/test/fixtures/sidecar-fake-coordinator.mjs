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

function failWithCode(code) {
  const error = new Error(code);
  error.code = code;
  throw error;
}

function describeError(error) {
  const description = { name: error?.name ?? 'Error', message: error?.message ?? String(error) };
  if (typeof error?.code === 'string') description.code = error.code;
  if (error instanceof AggregateError) description.errors = error.errors.map(describeError);
  if (error?.cause) description.cause = describeError(error.cause);
  return description;
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
      stop: async () => {
        if (process.env.ZIGBEE_TEST_FAIL_ADAPTER_STOP === 'true') {
          recordAudit({ adapterStopAttempted: true });
          failWithCode('ADAPTER_STOP_FAILED');
        }
        recordAudit({ adapterStopped: true });
      },
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
    if (process.env.ZIGBEE_TEST_FAIL_START === 'true') failWithCode('COORDINATOR_START_FAILED');
  }

  getDeviceByIeeeAddr(ieeeAddr) {
    return ieeeAddr.toLowerCase() === this.device.ieeeAddr.toLowerCase() ? this.device : undefined;
  }

  databaseSave() {
    if (process.env.ZIGBEE_TEST_FAIL_DATABASE_SAVE === 'true') {
      recordAudit({ databaseSaveAttempted: true });
      failWithCode('DATABASE_SAVE_FAILED');
    }
    recordAudit({ databaseSaved: true });
  }
}

class FakeZnpAdapterManager {
  async determineStrategy() { return 'startup'; }
  async addToGroup() {}
}

try {
  await runSidecar(process.env, {
    Controller: FakeCoordinator,
    ZnpAdapterManager: FakeZnpAdapterManager,
    setLogger() {},
  });
} catch (error) {
  recordAudit({ terminalError: describeError(error) });
  process.exitCode = 1;
}
