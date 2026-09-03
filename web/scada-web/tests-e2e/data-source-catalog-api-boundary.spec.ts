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
  expect(engineeringApi).toContain('CommunicationDriverRuntimeComposition.BuildForCurrentSchema(');
  expect(engineeringApi).toContain('hostProtectedMaterialResolver:');
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

test('Driver Engineering tooling keeps persisted GUID scope and adds read-authorized transient draft tooling', async () => {
  const engineeringApi = await repoFile('src/Scada.Api/Engineering/EngineeringDriverCatalogApi.cs');
  const tooling = await repoFile('src/Scada.Api/Engineering/EngineeringDriverTooling.cs');

  expect(engineeringApi).toContain('/api/engineering/data-sources/{id:guid}/driver-tools/connection-test');
  expect(engineeringApi).toContain('/api/engineering/data-sources/{id:guid}/driver-tools/discover');
  expect(engineeringApi).toContain('/api/engineering/data-sources/{id:guid}/driver-tools/browse');
  expect(engineeringApi).toContain('/api/engineering/driver-tools/connection-test');
  expect(engineeringApi).toContain('/api/engineering/driver-tools/discover');
  expect(engineeringApi.match(/\.RequireWorkspaceEngineeringRead\(\);/g)?.length ?? 0).toBeGreaterThanOrEqual(7);

  // Persisted operations still resolve the canonical Source by GUID.
  expect(engineeringApi).toContain('dataSources.Find(id)');

  // Draft tooling is transient and owns/disposes its provider instead of entering
  // the stable-ID browse continuation cache.
  expect(tooling).toContain('if (!dataSource.Id.HasValue || dataSource.Id.Value == Guid.Empty)');
  expect(tooling).toContain('new EngineeringDriverToolProviderLease(transient.Registration, transient.Provider)');
  expect(tooling).toContain('_ownedProvider?.DisposeAsync()');
  expect(tooling).toContain('ICommunicationDriverProtectedMaterialResolver');

  const failureBoundary = engineeringApi.slice(
    engineeringApi.indexOf('private static IResult DriverToolFailure'),
    engineeringApi.indexOf('private static string NormalizeEnumToken'));
  expect(failureBoundary).not.toContain('exception.Message');
  expect(failureBoundary).toContain('Working and Runtime state were not changed');
});
