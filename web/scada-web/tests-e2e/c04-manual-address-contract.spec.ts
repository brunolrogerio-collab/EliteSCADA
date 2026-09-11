import { expect, test } from '@playwright/test';
import {
  updateManualTagAddress,
  type TagSourceAwareEngineering
} from '../src/engineering/TagSourceSelector.logic';

function tag(overrides: Partial<TagSourceAwareEngineering> = {}): TagSourceAwareEngineering {
  return {
    name: 'Temperature',
    path: 'Plant.Temperature',
    dataType: 'double',
    readOnly: true,
    source: 'opc-main',
    dataSourceId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    ...overrides
  };
}

test('manual address edit keeps legacy Address and canonical CommunicationBinding portable address synchronized', () => {
  const original = tag({
    address: 'node=ns%3D2%3Bs%3DOld',
    communicationBinding: {
      contractVersion: 1,
      schemaId: 'elitescada.driver.opc-ua',
      schemaVersion: 2,
      portableAddress: 'node=ns%3D2%3Bs%3DOld',
      settings: { queueSize: '10' }
    }
  });

  const updated = updateManualTagAddress(original, 'node=ns%3D2%3Bs%3DNew');

  expect(updated.address).toBe('node=ns%3D2%3Bs%3DNew');
  expect(updated.communicationBinding?.portableAddress).toBe(updated.address);
  expect(updated.communicationBinding?.settings?.queueSize).toBe('10');
});

test('clearing manual address removes canonical binding instead of leaving an invalid envelope', () => {
  const original = tag({
    address: 'ca=1;ioa=10',
    communicationBinding: {
      contractVersion: 1,
      schemaId: 'elite.iec60870.5.104.point',
      schemaVersion: 1,
      portableAddress: 'ca=1;ioa=10',
      settings: { 'iec104.typeId': 'MMeNc1' }
    }
  });

  const cleared = updateManualTagAddress(original, '   ');

  expect(cleared.address).toBeNull();
  expect(cleared.communicationBinding).toBeNull();
});

test('legacy raw OPC UA NodeId remains available as an explicit manual migration fallback', () => {
  const updated = updateManualTagAddress(tag(), 'ns=2;s=Temperature');

  expect(updated.address).toBe('ns=2;s=Temperature');
  expect(updated.communicationBinding).toBeUndefined();
});
