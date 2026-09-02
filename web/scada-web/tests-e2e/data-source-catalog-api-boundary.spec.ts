import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function repoFile(path: string): Promise<string> {
  return readFile(new URL(`../../../${path}`, import.meta.url), 'utf8');
}

test('Driver catalog DI and endpoint remain in the Engineering API boundary', async () => {
  const engineeringApi = await repoFile('src/Scada.Api/Engineering/EngineeringDriverCatalogApi.cs');
  const persistenceApi = await repoFile('src/Scada.Api/Persistence/EngineeringPersistenceApi.cs');
  const program = await repoFile('src/Scada.Api/Program.cs');
  const licensingApi = await repoFile('src/Scada.Api/Licensing/ProductLicensingApi.cs');
  const licensingRuntime = await repoFile('src/Scada.Api/Licensing/ProductLicensedRuntimeCoordinator.cs');

  expect(engineeringApi).toContain('AddEngineeringDriverCatalog');
  expect(engineeringApi).toContain('CommunicationDriverRuntimeComposition.BuildForCurrentSchema()');
  expect(engineeringApi).toContain('IDataSourceConfigurationValidator');
  expect(engineeringApi).toContain('/api/engineering/data-source-types');
  expect(engineeringApi).toContain('.RequireWorkspaceEngineeringRead()');

  expect(persistenceApi).toContain('builder.AddEngineeringDriverCatalog();');
  expect(persistenceApi).toContain('app.MapEngineeringDriverCatalogEndpoints();');
  expect(program).toContain('builder.AddOptionalEngineeringPersistence();');
  expect(program).toContain('app.MapEngineeringPersistenceEndpoints();');

  expect(licensingApi).not.toContain('EngineeringDataSourceTypeCatalog');
  expect(licensingApi).not.toContain('/api/engineering/data-source-types');
  expect(licensingRuntime).not.toContain('EngineeringDataSourceTypeCatalog');
  expect(licensingRuntime).not.toContain('IDataSourceConfigurationValidator');
});
