import { EventEmitter } from 'node:events';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { runSidecar } from '../../src/sidecar.mjs';

function recordAudit(update) {
  const auditPath = process.env.ZIGBEE_TEST_AUDIT_PATH;
  const current = existsSync(auditPath) ? JSON.parse(readFileSync(auditPath, 'utf8')) : {};
  writeFileSync(auditPath, JSON.stringify({ ...current, ...update }));
}

class FakeCoordinator extends EventEmitter {
  constructor(options) {
    super();
    this.database = {};
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
