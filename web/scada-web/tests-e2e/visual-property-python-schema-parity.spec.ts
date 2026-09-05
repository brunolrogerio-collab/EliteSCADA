import { expect, test } from '@playwright/test';
import { dispatchClientVisualPythonCapability } from '../src/python-runtime/clientVisualPythonCapabilities';
import {
  RuntimeVisualInstance,
  createVisualPythonPropertyCapabilityProvider,
  listBuiltinVisualObjectSchemas,
  projectVisualEngineeringDefinition,
  type VisualPropertyDefinition,
  type VisualPropertyValue
} from '../src/visual-runtime';

test('canonical visual schemas stay compatible with generic Python read/write/clear capabilities', async () => {
  for (const [schemaIndex, schema] of listBuiltinVisualObjectSchemas().entries()) {
    const objectId = `c05-python-object-${schemaIndex}`;
    const objectKey = `c05PythonObject${schemaIndex}`;
    const definition = projectVisualEngineeringDefinition({
      objectId,
      key: objectKey,
      objectType: schema.objectTypeKey,
      baseProperties: schema.createDefaultValues()
    }, schema);
    const instance = new RuntimeVisualInstance({
      runtimeInstanceId: `c05-python-runtime-${schemaIndex}`,
      visualContextInstanceId: `c05-screen-${schemaIndex}`,
      definition,
      schema
    });
    const provider = createVisualPythonPropertyCapabilityProvider(instance);
    const context = {
      scriptId: `c05-script-${schemaIndex}`,
      runtimeInstanceId: `c05-script-runtime-${schemaIndex}`,
      visualRuntimeInstanceId: instance.runtimeInstanceId,
      executionId: `c05-execution-${schemaIndex}`
    };

    for (const property of schema.definitions()) {
      if (property.runtimeReadable) {
        const read = await dispatchClientVisualPythonCapability(
          provider,
          'visualProperty.read',
          'read',
          { targetReference: objectKey, propertyKey: property.key },
          context
        );
        expect(read, `${schema.objectTypeKey}.${property.key} read`).toMatchObject({ source: 'engineering' });
      } else {
        await expect(dispatchClientVisualPythonCapability(
          provider,
          'visualProperty.read',
          'read',
          { targetReference: objectKey, propertyKey: property.key },
          context
        ), `${schema.objectTypeKey}.${property.key} read policy`).rejects.toThrow(/not runtime-readable/);
      }

      const scriptValue = alternateValue(property);
      if (property.runtimeWritable) {
        await expect(dispatchClientVisualPythonCapability(
          provider,
          'visualProperty.write',
          'write',
          { targetReference: objectKey, propertyKey: property.key, value: scriptValue },
          context
        ), `${schema.objectTypeKey}.${property.key} write`).resolves.toMatchObject({
          accepted: true,
          propertyKey: property.key,
          visualRuntimeInstanceId: instance.runtimeInstanceId
        });

        const after = await dispatchClientVisualPythonCapability(
          provider,
          'visualProperty.read',
          'read',
          { targetReference: objectKey, propertyKey: property.key },
          context
        );
        expect(after, `${schema.objectTypeKey}.${property.key} script precedence`).toEqual({
          value: scriptValue,
          source: 'script'
        });

        await expect(dispatchClientVisualPythonCapability(
          provider,
          'visualProperty.write',
          'clear',
          { targetReference: objectKey, propertyKey: property.key },
          context
        ), `${schema.objectTypeKey}.${property.key} clear`).resolves.toMatchObject({
          accepted: true,
          propertyKey: property.key,
          visualRuntimeInstanceId: instance.runtimeInstanceId
        });

        const cleared = await dispatchClientVisualPythonCapability(
          provider,
          'visualProperty.read',
          'read',
          { targetReference: objectKey, propertyKey: property.key },
          context
        );
        expect(cleared, `${schema.objectTypeKey}.${property.key} clear precedence`).toEqual({
          value: property.defaultValue,
          source: 'engineering'
        });
      } else {
        await expect(dispatchClientVisualPythonCapability(
          provider,
          'visualProperty.write',
          'write',
          { targetReference: objectKey, propertyKey: property.key, value: scriptValue },
          context
        ), `${schema.objectTypeKey}.${property.key} write policy`).rejects.toThrow(/not runtime-writable/);

        await expect(dispatchClientVisualPythonCapability(
          provider,
          'visualProperty.write',
          'clear',
          { targetReference: objectKey, propertyKey: property.key },
          context
        ), `${schema.objectTypeKey}.${property.key} clear policy`).rejects.toThrow(/not runtime-writable/);
      }
    }

    instance.dispose();
  }
});

function alternateValue(definition: VisualPropertyDefinition): VisualPropertyValue {
  switch (definition.type) {
    case 'boolean':
      return !definition.defaultValue;
    case 'string':
      return definition.defaultValue === 'c05-script-value' ? 'c05-script-value-2' : 'c05-script-value';
    case 'color':
      return definition.defaultValue.toUpperCase() === '#12345678' ? '#87654321' : '#12345678';
    case 'enum':
      return definition.allowedValues.find(value => value !== definition.defaultValue) ?? definition.defaultValue;
    case 'assetRef':
      return { assetId: 'asset:c05-python-parity' };
    case 'number':
      return alternateNumber(definition.defaultValue, definition.minimum, definition.maximum, definition.integer === true);
  }
}

function alternateNumber(
  defaultValue: number,
  minimum: number | undefined,
  maximum: number | undefined,
  integer: boolean
): number {
  let candidate: number;
  if (minimum !== undefined && maximum !== undefined) {
    candidate = (minimum + maximum) / 2;
  } else if (maximum !== undefined) {
    candidate = Math.min(maximum, defaultValue - 1);
  } else if (minimum !== undefined) {
    candidate = Math.max(minimum, defaultValue + 1);
  } else {
    candidate = defaultValue + 1;
  }

  if (integer) candidate = Math.round(candidate);
  if (candidate === defaultValue) {
    const increment = integer ? 1 : 0.5;
    const increased = defaultValue + increment;
    const decreased = defaultValue - increment;
    if (maximum === undefined || increased <= maximum) candidate = increased;
    else if (minimum === undefined || decreased >= minimum) candidate = decreased;
  }
  return candidate;
}
