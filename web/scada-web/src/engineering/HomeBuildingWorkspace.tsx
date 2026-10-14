import React, { useEffect, useMemo, useState } from "react";
import {
  applyEngineeringPackage,
  loadEngineeringSnapshot,
  loadEngineeringWorkspace,
  previewEngineeringPackage,
} from "./api";
import {
  DataSourceCatalogEditor,
  loadDataSourceTypeCatalog,
} from "./DataSourceCatalogEditor";
import type { DataSourceTypeDefinition } from "./DataSourceCatalogEditor.logic";
import {
  discoverEngineeringDataSource,
  type DriverDiscoveryCandidateView,
} from "./driverEngineeringApi";
import type { EngineeringLocale } from "./i18n";
import type {
  DataSourceEngineering,
  EquipmentEngineering,
  EquipmentSourceBindingEngineering,
  EngineeringPackageView,
  EngineeringSnapshot,
  ImportPreviewItemView,
  LocationEngineering,
} from "./types";
import {
  buildLocationPackage,
  buildMaterializationPackage,
} from "./homeBuildingWorkspace.logic";
import "./home-building-workspace.css";

type Props = {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  projectKey: string;
  onApplied: () => Promise<void>;
};

type WorkspaceTab = "integrations" | "locations" | "devices";

export function HomeBuildingWorkspace({
  snapshot,
  locale,
  projectKey,
  onApplied,
}: Props) {
  const text = useMemo(() => copy(locale), [locale]);
  const [tab, setTab] = useState<WorkspaceTab>("integrations");
  const [catalog, setCatalog] = useState<DataSourceTypeDefinition[]>([]);
  const [catalogError, setCatalogError] = useState("");

  useEffect(() => {
    let active = true;
    void loadDataSourceTypeCatalog()
      .then((types) => {
        if (active) setCatalog(types);
      })
      .catch((reason) => {
        if (active) setCatalogError(asMessage(reason));
      });
    return () => {
      active = false;
    };
  }, []);

  const tabs: Array<{ id: WorkspaceTab; label: string; count: number }> = [
    {
      id: "integrations",
      label: text.integrations,
      count: snapshot.package.dataSources?.length ?? 0,
    },
    {
      id: "locations",
      label: text.locations,
      count: snapshot.package.locations?.length ?? 0,
    },
    {
      id: "devices",
      label: text.devices,
      count: snapshot.package.equipment?.length ?? 0,
    },
  ];

  return (
    <section
      className="eng-section home-building"
      data-testid="home-building-workspace"
    >
      <header className="eng-section-header">
        <div>
          <span className="eng-eyebrow">{text.eyebrow}</span>
          <h1>{text.title}</h1>
          <p>{text.description}</p>
        </div>
        <div className="eng-section-meta">
          <strong>
            {snapshot.package.dataSources?.length ?? 0}{" "}
            {text.integrations.toLocaleLowerCase()}
          </strong>
        </div>
      </header>
      <div
        className="home-building__tabs"
        role="tablist"
        aria-label={text.workspaceTabs}
      >
        {tabs.map((item) => (
          <button
            key={item.id}
            type="button"
            role="tab"
            aria-selected={tab === item.id}
            aria-controls={`home-building-panel-${item.id}`}
            data-testid={`home-building-tab-${item.id}`}
            className={tab === item.id ? "is-active" : ""}
            onClick={() => setTab(item.id)}
          >
            {item.label}
            <span>{item.count}</span>
          </button>
        ))}
      </div>
      <div
        id={`home-building-panel-${tab}`}
        role="tabpanel"
        className="home-building__panel"
      >
        {tab === "integrations" ? (
          <IntegrationsPanel
            model={snapshot.package}
            locale={locale}
            projectKey={projectKey}
            catalog={catalog}
            catalogError={catalogError}
            text={text}
          />
        ) : null}
        {tab === "locations" ? (
          <LocationsPanel
            snapshot={snapshot}
            onApplied={onApplied}
            text={text}
          />
        ) : null}
        {tab === "devices" ? (
          <DevicesPanel
            snapshot={snapshot}
            catalog={catalog}
            onApplied={onApplied}
            text={text}
          />
        ) : null}
      </div>
    </section>
  );
}

function IntegrationsPanel({
  model,
  locale,
  projectKey,
  catalog,
  catalogError,
  text,
}: {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
  projectKey: string;
  catalog: DataSourceTypeDefinition[];
  catalogError: string;
  text: ReturnType<typeof copy>;
}) {
  const homeTypes = catalog.filter((type) =>
    (type.integrationDomains ?? []).some((domain) =>
      ["building", "residential", "iot"].includes(domain.toLocaleLowerCase()),
    ),
  );
  const existingSources = model.dataSources ?? [];
  return (
    <div
      className="home-building__integrations"
      data-testid="home-building-integrations"
    >
      <div className="home-building__intro">
        <div>
          <h2>{text.catalogTitle}</h2>
          <p>{text.catalogHint}</p>
        </div>
        <span>
          {homeTypes.length} {text.catalogDrivers}
        </span>
      </div>
      {catalogError ? (
        <p className="home-building__error" role="alert">
          {catalogError}
        </p>
      ) : null}
      <div className="home-building__driver-grid">
        {homeTypes.map((type) => {
          const connected = existingSources.filter(
            (source) =>
              source.driver.toLocaleLowerCase() ===
              type.typeKey.toLocaleLowerCase(),
          ).length;
          const domains = (type.integrationDomains ?? []).filter((domain) =>
            ["building", "residential", "iot"].includes(
              domain.toLocaleLowerCase(),
            ),
          );
          return (
            <article
              key={type.typeKey}
              className="home-building__driver-card"
              data-testid="home-building-driver-card"
            >
              <div className="home-building__driver-card-heading">
                <strong>{type.displayName}</strong>
                <code>{type.typeKey}</code>
              </div>
              <p>{type.description || text.noDescription}</p>
              <div className="home-building__badges">
                {domains.map((domain) => (
                  <span key={domain}>{domainLabel(domain, locale)}</span>
                ))}
                {type.connectionModel ? (
                  <span>{connectionLabel(type.connectionModel, locale)}</span>
                ) : null}
                {type.capabilities?.supportsDiscovery ? (
                  <span>{text.discoveryBadge}</span>
                ) : null}
              </div>
              {(type.externalDependencies ?? []).length > 0 ? (
                <ul className="home-building__dependencies">
                  {type.externalDependencies?.map((dependency, index) => (
                    <li key={`${dependency.kind}-${index}`}>
                      <strong>
                        {dependencyLabel(dependency.kind, locale)}
                      </strong>
                      {dependency.requirement ? (
                        <span>{dependency.requirement}</span>
                      ) : null}
                      {dependency.description ? (
                        <small>{dependency.description}</small>
                      ) : null}
                    </li>
                  ))}
                </ul>
              ) : null}
              <footer>
                <span>
                  {text.configured}: {connected}
                </span>
                <span>
                  {type.capabilities?.supportsDiscovery
                    ? text.discoveryAvailable
                    : text.noDiscovery}
                </span>
              </footer>
            </article>
          );
        })}
        {catalog.length > 0 && homeTypes.length === 0 ? (
          <div className="eng-empty">
            <strong>{text.noCatalogDrivers}</strong>
          </div>
        ) : null}
      </div>
      <details className="home-building__source-editor" open>
        <summary>{text.configureIntegrations}</summary>
        <p>{text.configureHint}</p>
        <DataSourceCatalogEditor
          model={model}
          locale={locale}
          projectKey={projectKey}
        />
      </details>
    </div>
  );
}

function LocationsPanel({
  snapshot,
  onApplied,
  text,
}: {
  snapshot: EngineeringSnapshot;
  onApplied: () => Promise<void>;
  text: ReturnType<typeof copy>;
}) {
  const locations = snapshot.package.locations ?? [];
  const [selectedId, setSelectedId] = useState("");
  const [name, setName] = useState("");
  const [kind, setKind] = useState("Site");
  const [parentId, setParentId] = useState("");
  const [candidate, setCandidate] = useState<EngineeringPackageView | null>(
    null,
  );
  const [validatedVersion, setValidatedVersion] = useState<number | null>(null);
  const [preview, setPreview] = useState<Awaited<
    ReturnType<typeof previewEngineeringPackage>
  > | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  const selected = locations.find((item) => item.id === selectedId) ?? null;
  const parentOptions = useMemo(
    () =>
      [...locations].sort((a, b) =>
        locationPath(a, locations).localeCompare(locationPath(b, locations)),
      ),
    [locations],
  );
  const invalidate = () => {
    setCandidate(null);
    setValidatedVersion(null);
    setPreview(null);
  };
  const reset = () => {
    setSelectedId("");
    setName("");
    setKind("Site");
    setParentId("");
    setMessage("");
    invalidate();
  };
  const edit = (location: LocationEngineering) => {
    setSelectedId(location.id ?? "");
    setName(location.name);
    setKind(location.kind || "AreaRoom");
    setParentId(location.parentLocationId ?? "");
    setMessage("");
    invalidate();
  };

  const validate = async () => {
    setBusy(true);
    setMessage("");
    try {
      const before = await loadEngineeringWorkspace();
      const next = buildLocationPackage(snapshot.package, {
        id: selected?.id,
        name,
        kind,
        parentLocationId: parentId || null,
      });
      const nextPreview = await previewEngineeringPackage(next);
      const after = await loadEngineeringWorkspace();
      if (
        before.changeVersion !== after.changeVersion ||
        after.changeVersion !== snapshot.workspace.changeVersion
      )
        throw new Error(text.workspaceChanged);
      setCandidate(next);
      setValidatedVersion(after.changeVersion);
      setPreview(nextPreview);
      if (!nextPreview.canApply)
        setMessage(firstPreviewError(nextPreview) ?? text.validationFailed);
    } catch (reason) {
      setCandidate(null);
      setPreview(null);
      setValidatedVersion(null);
      setMessage(asMessage(reason));
    } finally {
      setBusy(false);
    }
  };

  const apply = async () => {
    if (!candidate || !preview?.canApply || validatedVersion === null) return;
    setBusy(true);
    setMessage("");
    try {
      const applied = candidate.locations?.find(
        (item) =>
          item.id &&
          item.id === (selected?.id ?? candidate.locations?.at(-1)?.id),
      );
      await applyEngineeringPackage(candidate, validatedVersion);
      await onApplied();
      if (applied?.id) setSelectedId(applied.id);
      setMessage(text.locationSaved);
      setCandidate(null);
      setPreview(null);
      setValidatedVersion(null);
    } catch (reason) {
      setMessage(asMessage(reason));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      className="home-building__locations"
      data-testid="home-building-locations"
    >
      <div className="home-building__intro">
        <div>
          <h2>{text.locationTitle}</h2>
          <p>{text.locationHint}</p>
        </div>
        <span>
          {locations.length} {text.locationCount}
        </span>
      </div>
      <div className="home-building__two-column">
        <section className="eng-panel home-building__form">
          <h3>{selected ? text.editLocation : text.newLocation}</h3>
          <label>
            <span>{text.locationName}</span>
            <input
              data-testid="home-building-location-name"
              value={name}
              onChange={(event) => {
                setName(event.currentTarget.value);
                invalidate();
              }}
            />
          </label>
          <label>
            <span>{text.locationKind}</span>
            <select
              data-testid="home-building-location-kind"
              value={kind}
              onChange={(event) => {
                setKind(event.currentTarget.value);
                invalidate();
              }}
            >
              <option value="Site">{text.kinds.Site}</option>
              <option value="Building">{text.kinds.Building}</option>
              <option value="Floor">{text.kinds.Floor}</option>
              <option value="AreaRoom">{text.kinds.AreaRoom}</option>
              <option value="Zone">{text.kinds.Zone}</option>
            </select>
          </label>
          <label>
            <span>{text.parentLocation}</span>
            <select
              data-testid="home-building-location-parent"
              value={parentId}
              onChange={(event) => {
                setParentId(event.currentTarget.value);
                invalidate();
              }}
            >
              <option value="">{text.noParent}</option>
              {parentOptions.map((item) => (
                <option
                  key={item.id ?? item.name}
                  value={item.id ?? ""}
                  disabled={!item.id}
                >
                  {locationPath(item, locations)}
                </option>
              ))}
            </select>
            <small>{text.parentHint}</small>
          </label>
          <div className="home-building__actions">
            <button
              type="button"
              className="secondary"
              disabled={busy || !name.trim()}
              onClick={() => void validate()}
              data-testid="home-building-location-preview"
            >
              {busy ? text.validating : text.preview}
            </button>
            <button
              type="button"
              className="primary"
              disabled={
                busy ||
                !candidate ||
                !preview?.canApply ||
                validatedVersion === null
              }
              onClick={() => void apply()}
              data-testid="home-building-location-apply"
            >
              {text.apply}
            </button>
            {selected ? (
              <button
                type="button"
                className="secondary"
                disabled={busy}
                onClick={reset}
              >
                {text.newLocation}
              </button>
            ) : null}
          </div>
          {preview ? (
            <PreviewSummary
              preview={preview}
              candidatePackage={candidate}
              text={text}
            />
          ) : null}
          {message ? (
            <p
              className={
                preview?.canApply
                  ? "home-building__success"
                  : "home-building__error"
              }
              role="status"
            >
              {message}
            </p>
          ) : null}
        </section>
        <section className="eng-panel home-building__list">
          <h3>{text.locationHierarchy}</h3>
          {locations.length === 0 ? (
            <div className="eng-empty">
              <strong>{text.noLocations}</strong>
              <span>{text.emptyLocationHint}</span>
            </div>
          ) : (
            <div className="home-building__rows" role="list">
              {parentOptions.map((item) => (
                <button
                  type="button"
                  role="listitem"
                  key={item.id ?? item.name}
                  className={`home-building__row${selectedId === item.id ? " is-selected" : ""}`}
                  onClick={() => edit(item)}
                >
                  <span className="home-building__row-icon" aria-hidden="true">
                    ⌖
                  </span>
                  <span>
                    <strong>{item.name}</strong>
                    <code>{locationPath(item, locations)}</code>
                  </span>
                  <small>{kindLabel(item.kind, text)}</small>
                </button>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

function DevicesPanel({
  snapshot,
  catalog,
  onApplied,
  text,
}: {
  snapshot: EngineeringSnapshot;
  catalog: DataSourceTypeDefinition[];
  onApplied: () => Promise<void>;
  text: ReturnType<typeof copy>;
}) {
  const sources = snapshot.package.dataSources ?? [];
  const discoverySources = sources.filter(
    (source) =>
      source.id &&
      catalog.find(
        (type) =>
          type.typeKey.toLocaleLowerCase() ===
            source.driver.toLocaleLowerCase() &&
          type.capabilities?.supportsDiscovery,
      ),
  );
  const [sourceId, setSourceId] = useState("");
  const [integrationFilterId, setIntegrationFilterId] = useState("*");
  const [locationFilterId, setLocationFilterId] = useState("*");
  const [discovered, setDiscovered] = useState<DriverDiscoveryCandidateView[]>(
    [],
  );
  const [selectedCandidateId, setSelectedCandidateId] = useState("");
  const [deviceName, setDeviceName] = useState("");
  const [devicePath, setDevicePath] = useState("");
  const [locationId, setLocationId] = useState("");
  const [packageCandidate, setPackageCandidate] =
    useState<EngineeringPackageView | null>(null);
  const [validatedVersion, setValidatedVersion] = useState<number | null>(null);
  const [preview, setPreview] = useState<Awaited<
    ReturnType<typeof previewEngineeringPackage>
  > | null>(null);
  const [busy, setBusy] = useState<"discover" | "preview" | "apply" | null>(
    null,
  );
  const [message, setMessage] = useState("");

  useEffect(() => {
    if (discoverySources.some((source) => source.id === sourceId)) return;
    setSourceId(discoverySources[0]?.id ?? "");
  }, [discoverySources, sourceId]);

  const source = discoverySources.find((item) => item.id === sourceId) ?? null;
  const definition = source
    ? (catalog.find(
        (type) =>
          type.typeKey.toLocaleLowerCase() ===
          source.driver.toLocaleLowerCase(),
      ) ?? null)
    : null;
  const selected =
    discovered.find(
      (candidate) => candidate.candidateId === selectedCandidateId,
    ) ?? null;
  const materialization = selected?.materialization ?? null;
  const locations = snapshot.package.locations ?? [];
  const equipment = snapshot.package.equipment ?? [];
  const integrationOptions = buildIntegrationFilterOptions(
    sources,
    equipment,
    text,
  );
  const locationOptions = buildLocationFilterOptions(
    locations,
    equipment,
    text,
  );
  const registeredGroups = groupRegisteredEquipment(
    equipment,
    locations,
    sources,
    integrationFilterId,
    locationFilterId,
    text,
  );
  const invalidate = () => {
    setPackageCandidate(null);
    setValidatedVersion(null);
    setPreview(null);
  };

  const chooseCandidate = (item: DriverDiscoveryCandidateView) => {
    setSelectedCandidateId(item.candidateId);
    setDeviceName(item.materialization?.equipment.name ?? item.displayName);
    setDevicePath(item.materialization?.equipment.path ?? "");
    setLocationId("");
    setMessage("");
    invalidate();
  };

  const discover = async () => {
    if (!source?.id) return;
    setBusy("discover");
    setMessage("");
    setDiscovered([]);
    setSelectedCandidateId("");
    invalidate();
    try {
      const results = await discoverEngineeringDataSource(source.id, {
        maximumResults: 100,
      });
      setDiscovered(results);
      if (results.length === 0) setMessage(text.noCandidates);
    } catch (reason) {
      setMessage(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const validate = async () => {
    if (!source || !definition || !selected?.materialization) return;
    setBusy("preview");
    setMessage("");
    try {
      const before = await loadEngineeringSnapshot();
      const latestSource = before.package.dataSources?.find(
        (item) => item.id === source.id,
      );
      if (!latestSource) throw new Error(text.sourceChanged);
      const next = buildMaterializationPackage(
        before.package,
        latestSource,
        definition,
        selected,
        {
          locationId: locationId || null,
          equipmentName: deviceName,
          equipmentPath: devicePath,
        },
      );
      const nextPreview = await previewEngineeringPackage(next);
      const after = await loadEngineeringWorkspace();
      if (
        before.workspace.changeVersion !== after.changeVersion ||
        after.changeVersion !== snapshot.workspace.changeVersion
      )
        throw new Error(text.workspaceChanged);
      setPackageCandidate(next);
      setValidatedVersion(after.changeVersion);
      setPreview(nextPreview);
      if (!nextPreview.canApply)
        setMessage(firstPreviewError(nextPreview) ?? text.validationFailed);
    } catch (reason) {
      setPackageCandidate(null);
      setPreview(null);
      setValidatedVersion(null);
      setMessage(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const apply = async () => {
    if (!packageCandidate || !preview?.canApply || validatedVersion === null)
      return;
    setBusy("apply");
    setMessage("");
    try {
      await applyEngineeringPackage(packageCandidate, validatedVersion);
      await onApplied();
      setMessage(text.deviceApplied);
      setPreview(null);
      setPackageCandidate(null);
      setValidatedVersion(null);
    } catch (reason) {
      setMessage(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="home-building__devices" data-testid="home-building-devices">
      <div className="home-building__intro">
        <div>
          <h2>{text.deviceTitle}</h2>
          <p>{text.deviceHint}</p>
        </div>
        <span>
          {equipment.length} {text.devices.toLocaleLowerCase()}
        </span>
      </div>
      <div className="home-building__device-toolbar">
        <label>
          <span>{text.integration}</span>
          <select
            data-testid="home-building-device-source"
            value={sourceId}
            onChange={(event) => {
              setSourceId(event.currentTarget.value);
              setDiscovered([]);
              setSelectedCandidateId("");
              setMessage("");
              invalidate();
            }}
          >
            {discoverySources.length === 0 ? (
              <option value="">{text.noDiscoverySource}</option>
            ) : null}
            {discoverySources.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name} · {item.driver}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          className="primary"
          disabled={!source || busy !== null}
          onClick={() => void discover()}
          data-testid="home-building-discover"
        >
          {busy === "discover" ? text.discovering : text.discover}
        </button>
      </div>
      {discoverySources.length === 0 ? (
        <div className="home-building__callout">
          <strong>{text.noDiscoverySource}</strong>
          <span>{text.configureFirst}</span>
        </div>
      ) : null}
      {discoverySources.length > 0 && discovered.length > 0 ? (
        <div className="home-building__candidate-layout">
          <section className="eng-panel home-building__candidate-list">
            <h3>
              {text.discoveryResults} <span>{discovered.length}</span>
            </h3>
            <div className="home-building__rows" role="list">
              {discovered.map((item) => {
                const device = item.materialization?.equipment;
                const issueCount = item.issues?.length ?? 0;
                return (
                  <div
                    role="listitem"
                    className="home-building__candidate-entry"
                    key={item.candidateId}
                  >
                    <button
                      type="button"
                      className={`home-building__candidate${selectedCandidateId === item.candidateId ? " is-selected" : ""}`}
                      onClick={() => chooseCandidate(item)}
                      data-testid={`home-building-candidate-${item.candidateId}`}
                    >
                      <span>
                        <strong>{device?.name ?? item.displayName}</strong>
                      </span>
                      {source ? (
                        <small>
                          {text.integration}: {source.name || source.key} · {source.driver}
                        </small>
                      ) : null}
                      <small>
                        {device
                          ? `${(item.materialization?.tags ?? []).length} ${text.tags.toLocaleLowerCase()}`
                          : text.endpointOnly}
                      </small>
                      {device?.locationPlaceholder ? (
                        <small>
                          {text.locationHintLabel}: {device.locationPlaceholder}
                        </small>
                      ) : null}
                      {issueCount > 0 ? (
                        <small className="home-building__warning">
                          {issueCount} {text.driverIssues}
                        </small>
                      ) : null}
                    </button>
                    <details
                      className="home-building__diagnostics"
                      data-testid={`home-building-candidate-diagnostics-${item.candidateId}`}
                    >
                      <summary>{text.advancedDetails}</summary>
                      <code data-testid="home-building-candidate-identity">
                        {device?.stableDeviceIdentity ?? item.stableIdentity}
                      </code>
                      {device?.path ? (
                        <code>{text.devicePath}: {device.path}</code>
                      ) : null}
                    </details>
                  </div>
                );
              })}
            </div>
          </section>
          <section className="eng-panel home-building__candidate-detail">
            <h3>{text.deviceMaterialization}</h3>
            {!selected ? (
              <div className="eng-empty">
                <span>{text.selectCandidate}</span>
              </div>
            ) : (
              <>
                {selected.issues?.map((issue, index) => (
                  <p
                    className={
                      isErrorSeverity(issue.severity)
                        ? "home-building__error"
                        : "home-building__warning"
                    }
                    key={`${issue.code}-${index}`}
                  >
                    {issue.message}
                  </p>
                ))}
                {materialization ? (
                  <>
                    <div className="home-building__candidate-summary">
                      <strong>{materialization.equipment.name}</strong>
                      <span>
                        {materialization.tags?.length ?? 0} {text.tags} ·{" "}
                        {materialization.commands?.length ?? 0} {text.commands}{" "}
                        · {materialization.equipment.capabilities?.length ?? 0}{" "}
                        {text.capabilities}
                      </span>
                    </div>
                    <details
                      className="home-building__diagnostics"
                      data-testid="home-building-selected-device-diagnostics"
                    >
                      <summary>{text.advancedDetails}</summary>
                      <code data-testid="home-building-selected-device-identity">
                        {materialization.equipment.stableDeviceIdentity}
                      </code>
                    </details>
                    <label>
                      <span>{text.deviceName}</span>
                      <input
                        data-testid="home-building-device-name"
                        value={deviceName}
                        onChange={(event) => {
                          setDeviceName(event.currentTarget.value);
                          invalidate();
                        }}
                      />
                    </label>
                    <label>
                      <span>{text.devicePath}</span>
                      <input
                        className="mono"
                        data-testid="home-building-device-path"
                        value={devicePath}
                        onChange={(event) => {
                          setDevicePath(event.currentTarget.value);
                          invalidate();
                        }}
                      />
                    </label>
                    <label>
                      <span>{text.assignLocation}</span>
                      <select
                        data-testid="home-building-device-location"
                        value={locationId}
                        onChange={(event) => {
                          setLocationId(event.currentTarget.value);
                          invalidate();
                        }}
                      >
                        <option value="">{text.unassigned}</option>
                        {locations.map((item) => (
                          <option
                            key={item.id ?? item.name}
                            value={item.id ?? ""}
                            disabled={!item.id}
                          >
                            {locationPath(item, locations)}
                          </option>
                        ))}
                      </select>
                      <small>{text.locationAssignmentHint}</small>
                    </label>
                    <div className="home-building__actions">
                      <button
                        type="button"
                        className="secondary"
                        disabled={
                          busy !== null ||
                          !deviceName.trim() ||
                          !devicePath.trim()
                        }
                        onClick={() => void validate()}
                        data-testid="home-building-device-preview"
                      >
                        {busy === "preview" ? text.validating : text.preview}
                      </button>
                      <button
                        type="button"
                        className="primary"
                        disabled={
                          busy !== null ||
                          !packageCandidate ||
                          !preview?.canApply ||
                          validatedVersion === null
                        }
                        onClick={() => void apply()}
                        data-testid="home-building-device-apply"
                      >
                        {busy === "apply" ? text.applying : text.apply}
                      </button>
                    </div>
                    {preview ? (
                      <PreviewSummary
                        preview={preview}
                        candidatePackage={packageCandidate}
                        text={text}
                      />
                    ) : null}
                  </>
                ) : (
                  <div className="home-building__callout">
                    <strong>{text.endpointOnly}</strong>
                    <span>{text.endpointOnlyHint}</span>
                  </div>
                )}
              </>
            )}
            {message ? (
              <p
                className={
                  preview?.canApply
                    ? "home-building__success"
                    : "home-building__error"
                }
                role="status"
              >
                {message}
              </p>
            ) : null}
          </section>
        </div>
      ) : null}
      {discovered.length === 0 && !message && source ? (
        <div className="home-building__callout">
          <strong>{text.discoveryReady}</strong>
          <span>{text.discoveryReadyHint}</span>
        </div>
      ) : null}
      <section className="eng-panel home-building__existing-devices">
        <h3>
          {text.registeredDevices} <span>{equipment.length}</span>
        </h3>
        <div className="home-building__device-filters">
          <label>
            <span>{text.filterIntegration}</span>
            <select
              data-testid="home-building-device-integration-filter"
              value={integrationFilterId}
              onChange={(event) =>
                setIntegrationFilterId(event.currentTarget.value)
              }
            >
              <option value="*">{text.allIntegrations}</option>
              {integrationOptions.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span>{text.filterLocation}</span>
            <select
              data-testid="home-building-device-location-filter"
              value={locationFilterId}
              onChange={(event) =>
                setLocationFilterId(event.currentTarget.value)
              }
            >
              <option value="*">{text.allLocations}</option>
              {locationOptions.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
        </div>
        {equipment.length === 0 ? (
          <p>{text.noDevices}</p>
        ) : registeredGroups.length === 0 ? (
          <p>{text.noFilteredDevices}</p>
        ) : (
          <div className="home-building__registered-groups">
            {registeredGroups.map((locationGroup) => (
              <section
                className="home-building__registered-location-group"
                key={locationGroup.id}
                data-testid="home-building-location-group"
              >
                <h4>{locationGroup.label}</h4>
                {locationGroup.integrations.map((integrationGroup) => (
                  <section
                    className="home-building__registered-integration-group"
                    key={`${locationGroup.id}:${integrationGroup.id}`}
                    data-testid="home-building-integration-group"
                  >
                    <h5>{integrationGroup.label}</h5>
                    <div className="home-building__registered-device-list">
                      {integrationGroup.devices.map(({ item, bindings }) => (
                        <div
                          className="home-building__registered-device"
                          key={`${item.id ?? item.path}:${integrationGroup.id}`}
                          data-testid="home-building-registered-device"
                        >
                          <span>
                            <strong>{item.name}</strong>
                          </span>
                          <details
                            className="home-building__diagnostics"
                            data-testid="home-building-registered-device-diagnostics"
                          >
                            <summary>{text.advancedDetails}</summary>
                            {bindings.length === 0 ? (
                              <code>{text.devicePath}: {item.path}</code>
                            ) : (
                              bindings.map((binding, index) => (
                                <code key={`${binding.dataSourceId}:${index}`}>
                                  {binding.stableDeviceIdentity}
                                  {binding.role
                                    ? ` · ${text.bindingRole}: ${binding.role}`
                                    : ""}
                                </code>
                              ))
                            )}
                          </details>
                        </div>
                      ))}
                    </div>
                  </section>
                ))}
              </section>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

type RegisteredDeviceRecord = Readonly<{
  item: EquipmentEngineering;
  bindings: readonly EquipmentSourceBindingEngineering[];
}>;

type RegisteredIntegrationGroup = Readonly<{
  id: string;
  label: string;
  devices: readonly RegisteredDeviceRecord[];
}>;

type RegisteredLocationGroup = Readonly<{
  id: string;
  label: string;
  integrations: readonly RegisteredIntegrationGroup[];
}>;

type FilterOption = Readonly<{ id: string; label: string }>;

const UNASSIGNED_FILTER = "__unassigned__";
const UNBOUND_FILTER = "__unbound__";

function buildIntegrationFilterOptions(
  sources: readonly DataSourceEngineering[],
  equipment: readonly EquipmentEngineering[],
  text: ReturnType<typeof copy>,
): FilterOption[] {
  const sourceIds = new Set(
    sources.flatMap((source) => (source.id ? [source.id.toLowerCase()] : [])),
  );
  const options = sources.flatMap((source) =>
    source.id
      ? [
          {
            id: source.id,
            label: integrationLabel(source.id, sources, text),
          },
        ]
      : [],
  );
  const unknownIds = new Set(
    equipment.flatMap((item) =>
      (item.sourceBindings ?? [])
        .map((binding) => binding.dataSourceId)
        .filter((id) => id && !sourceIds.has(id.toLowerCase())),
    ),
  );
  options.push(
    ...[...unknownIds].map((id) => ({
      id,
      label: integrationLabel(id, sources, text),
    })),
  );
  if (equipment.some((item) => (item.sourceBindings ?? []).length === 0)) {
    options.push({ id: UNBOUND_FILTER, label: text.noIntegration });
  }
  return options.sort((left, right) => left.label.localeCompare(right.label));
}

function buildLocationFilterOptions(
  locations: readonly LocationEngineering[],
  equipment: readonly EquipmentEngineering[],
  text: ReturnType<typeof copy>,
): FilterOption[] {
  const knownIds = new Set(
    locations.flatMap((location) =>
      location.id ? [location.id.toLowerCase()] : [],
    ),
  );
  const options = locations.flatMap((location) =>
    location.id
      ? [
          {
            id: location.id,
            label: locationPath(location, locations),
          },
        ]
      : [],
  );
  const unknownIds = new Set(
    equipment
      .map((item) => item.locationId)
      .filter((id): id is string => Boolean(id) && !knownIds.has(id!.toLowerCase())),
  );
  options.push(
    ...[...unknownIds].map((id) => ({
      id,
      label: `${text.unknownLocation} · ${id}`,
    })),
  );
  if (equipment.some((item) => !item.locationId)) {
    options.push({ id: UNASSIGNED_FILTER, label: text.unassigned });
  }
  return options.sort((left, right) => left.label.localeCompare(right.label));
}

function groupRegisteredEquipment(
  equipment: readonly EquipmentEngineering[],
  locations: readonly LocationEngineering[],
  sources: readonly DataSourceEngineering[],
  integrationFilterId: string,
  locationFilterId: string,
  text: ReturnType<typeof copy>,
): RegisteredLocationGroup[] {
  const locationGroups = new Map<
    string,
    { label: string; integrations: Map<string, { label: string; devices: RegisteredDeviceRecord[] }> }
  >();
  const knownLocationIds = new Set(
    locations.flatMap((location) =>
      location.id ? [location.id.toLowerCase()] : [],
    ),
  );
  const getLocation = (locationId: string | null | undefined) =>
    locations.find(
      (location) =>
        location.id &&
        locationId &&
        location.id.toLowerCase() === locationId.toLowerCase(),
    );

  for (const item of equipment) {
    const locationId = item.locationId ?? "";
    if (
      locationFilterId !== "*" &&
      (locationId
        ? locationFilterId.toLowerCase() !== locationId.toLowerCase()
        : locationFilterId !== UNASSIGNED_FILTER)
    ) {
      continue;
    }

    const location = getLocation(locationId);
    const locationKey = locationId
      ? knownLocationIds.has(locationId.toLowerCase())
        ? locationId.toLowerCase()
        : `unknown:${locationId.toLowerCase()}`
      : UNASSIGNED_FILTER;
    const locationLabel = location
      ? locationPath(location, locations)
      : locationId
        ? `${text.unknownLocation} · ${locationId}`
        : text.unassigned;
    let locationGroup = locationGroups.get(locationKey);
    if (!locationGroup) {
      locationGroup = { label: locationLabel, integrations: new Map() };
      locationGroups.set(locationKey, locationGroup);
    }

    const bySource = new Map<string, EquipmentSourceBindingEngineering[]>();
    for (const binding of item.sourceBindings ?? []) {
      const key = binding.dataSourceId.toLowerCase();
      const existing = bySource.get(key) ?? [];
      existing.push(binding);
      bySource.set(key, existing);
    }
    if (bySource.size === 0) bySource.set(UNBOUND_FILTER, []);

    for (const [sourceKey, bindings] of bySource) {
      const filterKey = sourceKey === UNBOUND_FILTER ? UNBOUND_FILTER : sourceKey;
      if (
        integrationFilterId !== "*" &&
        integrationFilterId.toLowerCase() !== filterKey.toLowerCase()
      ) {
        continue;
      }
      const sourceId = sourceKey === UNBOUND_FILTER ? null : sourceKey;
      const integrationId = sourceId ?? UNBOUND_FILTER;
      let integrationGroup = locationGroup.integrations.get(integrationId);
      if (!integrationGroup) {
        integrationGroup = {
          label: sourceId
            ? integrationLabel(sourceId, sources, text)
            : text.noIntegration,
          devices: [],
        };
        locationGroup.integrations.set(integrationId, integrationGroup);
      }
      integrationGroup.devices.push({ item, bindings });
    }
  }

  return [...locationGroups.entries()]
    .map(([id, group]) => ({
      id,
      label: group.label,
      integrations: [...group.integrations.entries()]
        .map(([integrationId, integration]) => ({
          id: integrationId,
          label: integration.label,
          devices: integration.devices.sort((left, right) =>
            left.item.name.localeCompare(right.item.name),
          ),
        }))
        .sort((left, right) => left.label.localeCompare(right.label)),
    }))
    .sort((left, right) => left.label.localeCompare(right.label));
}

function integrationLabel(
  dataSourceId: string,
  sources: readonly DataSourceEngineering[],
  text: ReturnType<typeof copy>,
): string {
  const source = sources.find(
    (item) => item.id?.toLowerCase() === dataSourceId.toLowerCase(),
  );
  return source
    ? `${source.name || source.key} · ${source.driver}`
    : `${text.unknownIntegration} · ${dataSourceId}`;
}

function PreviewSummary({
  preview,
  candidatePackage,
  text,
}: {
  preview: Awaited<ReturnType<typeof previewEngineeringPackage>>;
  candidatePackage: EngineeringPackageView | null;
  text: ReturnType<typeof copy>;
}) {
  return (
    <div
      className={
        preview.canApply
          ? "home-building__preview is-valid"
          : "home-building__preview is-invalid"
      }
      data-testid="home-building-preview-summary"
    >
      <strong>
        {preview.canApply ? text.previewValid : text.previewInvalid}
      </strong>
      <span>
        {preview.createCount} {text.create} · {preview.updateCount}{" "}
        {text.update} · {preview.errorCount} {text.errors}
      </span>
      <strong>{text.previewChanges}</strong>
      {preview.items.length > 0 ? (
        <ul
          className="home-building__preview-items"
          data-testid="home-building-preview-items"
        >
          {preview.items.map((item, index) => (
            <li key={`${item.entityKind}:${item.entityKey}:${index}`}>
              <span className="home-building__preview-operation">
                {previewOperationLabel(item.operation, text)}
              </span>
              <strong>
                {item.entityKind} · {item.entityKey}
              </strong>
              {previewItemDetails(item, candidatePackage, text) ? (
                <small>
                  {previewItemDetails(item, candidatePackage, text)}
                </small>
              ) : null}
              {(item.issues ?? []).map((issue, issueIndex) => (
                <small
                  className={
                    issue.isError
                      ? "home-building__error-text"
                      : "home-building__warning"
                  }
                  key={`${issue.code}-${issueIndex}`}
                >
                  {issue.code}: {issue.message}
                </small>
              ))}
            </li>
          ))}
        </ul>
      ) : (
        <small>{text.noPreviewItems}</small>
      )}
    </div>
  );
}

function previewOperationLabel(
  operation: string,
  text: ReturnType<typeof copy>,
): string {
  switch (operation.toLocaleLowerCase()) {
    case "create":
      return text.create;
    case "update":
      return text.update;
    case "skip":
      return text.skip;
    case "error":
      return text.operationError;
    default:
      return operation;
  }
}

function previewItemDetails(
  item: ImportPreviewItemView,
  candidatePackage: EngineeringPackageView | null,
  text: ReturnType<typeof copy>,
): string | null {
  if (!candidatePackage) return null;
  switch (item.entityKind.toLocaleLowerCase()) {
    case "equipment": {
      const equipment = candidatePackage.equipment?.find(
        (candidate) => candidate.path === item.entityKey,
      );
      if (!equipment) return null;
      const location = candidatePackage.locations?.find(
        (candidate) => candidate.id === equipment.locationId,
      );
      const capabilities = (equipment.capabilities ?? []).map(
        (capability) => `${capability.kind} (${capability.id})`,
      );
      return [
        equipment.name,
        location ? `${text.location}: ${locationPath(location, candidatePackage.locations ?? [])}` : text.unassigned,
        capabilities.length ? `${text.capabilities}: ${capabilities.join(", ")}` : "",
      ].filter(Boolean).join(" · ");
    }
    case "tag": {
      const tag = candidatePackage.tags.find(
        (candidate) => candidate.path === item.entityKey,
      );
      return tag
        ? [tag.name, tag.dataType, tag.address || tag.communicationBinding?.portableAddress]
            .filter(Boolean)
            .join(" · ")
        : null;
    }
    case "command": {
      const command = candidatePackage.commands?.find(
        (candidate) => candidate.key === item.entityKey,
      );
      return command
        ? [command.name, command.equipmentPath, command.targetTagPath]
            .filter(Boolean)
            .join(" · ")
        : null;
    }
    case "location": {
      const location = candidatePackage.locations?.find(
        (candidate) => candidate.id === item.entityKey || candidate.name === item.entityKey,
      );
      if (!location) return null;
      const parent = candidatePackage.locations?.find(
        (candidate) => candidate.id === location.parentLocationId,
      );
      return [
        kindLabel(location.kind, text),
        parent ? `${text.parentLocation}: ${locationPath(parent, candidatePackage.locations ?? [])}` : "",
      ].filter(Boolean).join(" · ");
    }
    default:
      return null;
  }
}

function locationPath(
  location: LocationEngineering,
  all: readonly LocationEngineering[],
  seen = new Set<string>(),
): string {
  if (!location.id || seen.has(location.id)) return location.name;
  seen.add(location.id);
  const parent = all.find((item) => item.id === location.parentLocationId);
  return parent
    ? `${locationPath(parent, all, seen)} / ${location.name}`
    : location.name;
}

function kindLabel(
  kind: string | null | undefined,
  text: ReturnType<typeof copy>,
): string {
  return kind && kind in text.kinds
    ? text.kinds[kind as keyof typeof text.kinds]
    : kind || text.location;
}

function domainLabel(domain: string, locale: EngineeringLocale): string {
  const labels: Record<string, Record<EngineeringLocale, string>> = {
    building: { "pt-BR": "Predial", en: "Building", es: "Edificios" },
    residential: {
      "pt-BR": "Residencial",
      en: "Residential",
      es: "Residencial",
    },
    iot: { "pt-BR": "IoT", en: "IoT", es: "IoT" },
  };
  return labels[domain.toLocaleLowerCase()]?.[locale] ?? domain;
}

function connectionLabel(model: string, locale: EngineeringLocale): string {
  const labels: Record<string, Record<EngineeringLocale, string>> = {
    directnetwork: {
      "pt-BR": "Rede direta",
      en: "Direct network",
      es: "Red directa",
    },
    hostserial: {
      "pt-BR": "Serial no host",
      en: "Host serial",
      es: "Serial del host",
    },
    hostradio: {
      "pt-BR": "Rádio no host",
      en: "Host radio",
      es: "Radio del host",
    },
    localbridge: {
      "pt-BR": "Bridge local",
      en: "Local bridge",
      es: "Bridge local",
    },
    cloud: { "pt-BR": "Nuvem", en: "Cloud", es: "Nube" },
  };
  return labels[model.toLocaleLowerCase()]?.[locale] ?? model;
}

function dependencyLabel(kind: string, locale: EngineeringLocale): string {
  const labels: Record<string, Record<EngineeringLocale, string>> = {
    builtin: { "pt-BR": "Incluído", en: "Built in", es: "Incluido" },
    optionalsidecar: {
      "pt-BR": "Serviço opcional",
      en: "Optional service",
      es: "Servicio opcional",
    },
    thirdpartyaccount: {
      "pt-BR": "Conta externa",
      en: "External account",
      es: "Cuenta externa",
    },
    thirdpartysubscription: {
      "pt-BR": "Assinatura externa",
      en: "External subscription",
      es: "Suscripción externa",
    },
    externalgateway: {
      "pt-BR": "Gateway externo",
      en: "External gateway",
      es: "Gateway externo",
    },
    certifiedhardware: {
      "pt-BR": "Hardware certificado",
      en: "Certified hardware",
      es: "Hardware certificado",
    },
    hostresource: {
      "pt-BR": "Recurso do servidor",
      en: "Server resource",
      es: "Recurso del servidor",
    },
  };
  return labels[kind.toLocaleLowerCase()]?.[locale] ?? kind;
}

function firstPreviewError(
  preview: Awaited<ReturnType<typeof previewEngineeringPackage>>,
): string | null {
  return (
    preview.items
      .flatMap((item) => item.issues ?? [])
      .find((issue) => issue.isError)?.message ?? null
  );
}

function isErrorSeverity(value: string | number): boolean {
  return value === 2 || String(value).toLocaleLowerCase() === "error";
}

function asMessage(reason: unknown): string {
  return reason instanceof Error ? reason.message : String(reason);
}

function copy(locale: EngineeringLocale) {
  const kinds =
    locale === "en"
      ? {
          Site: "Site",
          Building: "Building",
          Floor: "Floor",
          AreaRoom: "Area / room",
          Zone: "Zone",
        }
      : locale === "es"
        ? {
            Site: "Sitio",
            Building: "Edificio",
            Floor: "Piso",
            AreaRoom: "Área / habitación",
            Zone: "Zona",
          }
        : {
            Site: "Unidade",
            Building: "Edifício",
            Floor: "Andar",
            AreaRoom: "Área / sala",
            Zone: "Zona",
          };
  if (locale === "en")
    return {
      eyebrow: "Home / Building",
      title: "Integrations, locations and devices",
      description:
        "Discover physical devices and organize them in EliteSCADA while keeping the canonical Engineering and Runtime model.",
      workspaceTabs: "Home and building workspaces",
      integrations: "Integrations",
      locations: "Locations",
      devices: "Devices",
      catalogTitle: "Available integration catalog",
      catalogHint:
        "Driver metadata comes from the current EliteSCADA catalog; dependencies and discovery support are shown before configuration.",
      catalogDrivers: "drivers",
      noDescription: "No description provided.",
      discoveryBadge: "Discovery",
      configured: "Configured",
      discoveryAvailable: "Discovery available",
      noDiscovery: "No device discovery",
      noCatalogDrivers:
        "No building or residential drivers are available in this build.",
      configureIntegrations: "Configure integrations",
      configureHint:
        "Use the normal Data Source editor. Saving applies through Engineering Preview/Apply; Runtime activation and publication remain separate.",
      locationTitle: "Physical location hierarchy",
      locationHint:
        "Create stable Site → Building → Floor → Area/Room → Zone identities. Equipment is assigned to a location here, not inside the protocol.",
      locationCount: "locations",
      editLocation: "Edit location",
      newLocation: "New location",
      locationName: "Location name",
      locationKind: "Location level",
      parentLocation: "Parent location",
      noParent: "No parent (root)",
      parentHint:
        "The parent link is validated by the canonical Engineering Preview.",
      locationHierarchy: "Locations",
      noLocations: "No locations yet",
      emptyLocationHint: "Add a site or building to start the hierarchy.",
      workspaceChanged:
        "Engineering changed while the candidate was being checked. Reload and try again.",
      validationFailed: "The candidate did not pass Engineering validation.",
      locationSaved: "Location saved to the Engineering workspace.",
      validating: "Validating…",
      preview: "Preview",
      apply: "Apply to Engineering",
      create: "Create",
      update: "Update",
      errors: "errors",
      previewValid: "Preview is valid",
      previewInvalid: "Preview has errors; nothing was applied.",
      previewChanges: "Canonical changes included in this Preview",
      noPreviewItems: "The server returned no entity-level changes.",
      skip: "Skip",
      operationError: "Blocked",
      deviceTitle: "Discover and register devices",
      deviceHint:
        "Discovery is read-only. Select a candidate, set its Engineering name and location, then preview canonical Equipment, TAGs and Commands before applying.",
      integration: "Integration",
      noDiscoverySource:
        "No saved integration with device discovery is available.",
      configureFirst:
        "Configure and apply an integration in the Integrations tab first. Browse-only drivers remain available through the existing TAG browser.",
      discover: "Discover devices",
      discovering: "Discovering…",
      discoveryResults: "Candidates",
      noCandidates: "No devices were returned by this discovery.",
      endpointOnly: "Endpoint result only",
      endpointOnlyHint:
        "This result does not include a composite device proposal. No Engineering changes were made; use the driver’s existing browse flow to select TAGs.",
      locationHintLabel: "Suggested location (not assigned)",
      driverIssues: "driver notes",
      selectCandidate: "Select a candidate to review it before importing.",
      deviceMaterialization: "Candidate and Preview/Apply",
      tags: "TAGs",
      commands: "Commands",
      capabilities: "capabilities",
      deviceName: "Equipment name",
      devicePath: "Stable Engineering path",
      assignLocation: "EliteSCADA location",
      unassigned: "Unassigned for now",
      locationAssignmentHint:
        "A driver-provided location is only a hint. Choose a canonical location here or explicitly leave the device unassigned.",
      sourceChanged:
        "The selected integration changed. Reload the workspace and discover again.",
      deviceApplied:
        "Device proposal applied to the Engineering workspace. Save or publish separately when ready.",
      applying: "Applying…",
      discoveryReady: "Ready to discover devices",
      discoveryReadyHint:
        "Choose Discover devices to query this integration. Discovery does not change Engineering.",
      registeredDevices: "Registered equipment",
      noDevices: "No devices have been materialized yet.",
      filterIntegration: "Filter by integration",
      filterLocation: "Filter by location",
      allIntegrations: "All integrations",
      allLocations: "All locations",
      noIntegration: "No integration bound",
      unknownIntegration: "Unknown integration",
      unknownLocation: "Unknown location",
      noFilteredDevices: "No equipment matches these filters.",
      bindingRole: "Binding role",
      advancedDetails: "Advanced details / diagnostics",
      location: "Location",
      kinds,
      workspaceStatus: "workspace",
      createLabel: "Create",
      updateLabel: "Update",
      previewHint: "Preview never changes the workspace.",
    };
  if (locale === "es")
    return {
      eyebrow: "Hogar / Edificios",
      title: "Integraciones, ubicaciones y dispositivos",
      description:
        "Descubra dispositivos físicos y organícelos en EliteSCADA conservando el modelo canónico de Engineering y Runtime.",
      workspaceTabs: "Espacios de hogar y edificios",
      integrations: "Integraciones",
      locations: "Ubicaciones",
      devices: "Dispositivos",
      catalogTitle: "Catálogo de integraciones disponibles",
      catalogHint:
        "Los metadatos provienen del catálogo actual de EliteSCADA; se muestran dependencias y descubrimiento antes de configurar.",
      catalogDrivers: "drivers",
      noDescription: "Sin descripción.",
      discoveryBadge: "Descubrimiento",
      configured: "Configuradas",
      discoveryAvailable: "Descubrimiento disponible",
      noDiscovery: "Sin descubrimiento",
      noCatalogDrivers:
        "No hay drivers de edificios o residencial en esta compilación.",
      configureIntegrations: "Configurar integraciones",
      configureHint:
        "Use el editor normal de Fuentes de datos. Guardar usa Preview/Apply de Engineering; activar Runtime y publicar siguen siendo acciones separadas.",
      locationTitle: "Jerarquía física de ubicaciones",
      locationHint:
        "Cree identidades estables Sitio → Edificio → Piso → Área/Habitación → Zona. El equipo se asigna aquí, no dentro del protocolo.",
      locationCount: "ubicaciones",
      editLocation: "Editar ubicación",
      newLocation: "Nueva ubicación",
      locationName: "Nombre",
      locationKind: "Nivel",
      parentLocation: "Ubicación superior",
      noParent: "Sin superior (raíz)",
      parentHint:
        "El vínculo se valida con el Preview canónico de Engineering.",
      locationHierarchy: "Ubicaciones",
      noLocations: "Aún no hay ubicaciones",
      emptyLocationHint:
        "Agregue un sitio o edificio para iniciar la jerarquía.",
      workspaceChanged:
        "Engineering cambió durante la validación. Recargue e intente de nuevo.",
      validationFailed: "El candidato no pasó la validación de Engineering.",
      locationSaved: "Ubicación guardada en el espacio de Engineering.",
      validating: "Validando…",
      preview: "Vista previa",
      apply: "Aplicar a Engineering",
      create: "Crear",
      update: "Actualizar",
      errors: "errores",
      previewValid: "Vista previa válida",
      previewInvalid: "Hay errores; no se aplicó nada.",
      previewChanges: "Cambios canónicos incluidos en esta vista previa",
      noPreviewItems: "El servidor no devolvió cambios por entidad.",
      skip: "Omitir",
      operationError: "Bloqueado",
      deviceTitle: "Descubrir y registrar dispositivos",
      deviceHint:
        "El descubrimiento es de solo lectura. Seleccione un candidato, defina su nombre y ubicación de Engineering, y previsualice Equipo, TAGs y Comandos antes de aplicar.",
      integration: "Integración",
      noDiscoverySource:
        "No hay integración guardada con descubrimiento de dispositivos.",
      configureFirst:
        "Configure y aplique una integración primero. Los drivers solo de navegación siguen en el navegador de TAG existente.",
      discover: "Descubrir dispositivos",
      discovering: "Descubriendo…",
      discoveryResults: "Candidatos",
      noCandidates: "No se encontraron dispositivos.",
      endpointOnly: "Solo resultado de endpoint",
      endpointOnlyHint:
        "Este resultado no incluye una propuesta compuesta. No se cambió Engineering; use el navegador del driver para seleccionar TAGs.",
      locationHintLabel: "Ubicación sugerida (sin asignar)",
      driverIssues: "avisos",
      selectCandidate: "Seleccione un candidato para revisarlo.",
      deviceMaterialization: "Candidato y Preview/Apply",
      tags: "TAGs",
      commands: "Comandos",
      capabilities: "capacidades",
      deviceName: "Nombre del equipo",
      devicePath: "Ruta estable de Engineering",
      assignLocation: "Ubicación EliteSCADA",
      unassigned: "Sin asignar por ahora",
      locationAssignmentHint:
        "La ubicación del driver es solo una sugerencia. Elija una ubicación canónica o déjelo sin asignar.",
      sourceChanged: "La integración cambió. Recargue y vuelva a descubrir.",
      deviceApplied:
        "La propuesta se aplicó al espacio de Engineering. Guardar o publicar sigue siendo independiente.",
      applying: "Aplicando…",
      discoveryReady: "Listo para descubrir dispositivos",
      discoveryReadyHint:
        "Elija Descubrir dispositivos para consultar esta integración. El descubrimiento no cambia Engineering.",
      registeredDevices: "Equipos registrados",
      noDevices: "Aún no se materializaron dispositivos.",
      filterIntegration: "Filtrar por integración",
      filterLocation: "Filtrar por ubicación",
      allIntegrations: "Todas las integraciones",
      allLocations: "Todas las ubicaciones",
      noIntegration: "Sin integración vinculada",
      unknownIntegration: "Integración desconocida",
      unknownLocation: "Ubicación desconocida",
      noFilteredDevices: "Ningún equipo coincide con estos filtros.",
      bindingRole: "Rol de vínculo",
      advancedDetails: "Detalles avanzados / diagnóstico",
      location: "Ubicación",
      kinds,
      workspaceStatus: "espacio",
      createLabel: "Crear",
      updateLabel: "Actualizar",
      previewHint: "La vista previa no cambia el espacio.",
    };
  return {
    eyebrow: "HOME / BUILDING",
    title: "Integrações, locais e dispositivos",
    description:
      "Descubra dispositivos físicos e organize-os no EliteSCADA preservando o modelo canônico de Engenharia e Runtime.",
    workspaceTabs: "Áreas de Home e Building",
    integrations: "Integrações",
    locations: "Locais",
    devices: "Dispositivos",
    catalogTitle: "Catálogo de integrações disponíveis",
    catalogHint:
      "Os metadados vêm do catálogo atual do EliteSCADA; dependências e suporte a descoberta aparecem antes da configuração.",
    catalogDrivers: "drivers",
    noDescription: "Sem descrição disponível.",
    discoveryBadge: "Descoberta",
    configured: "Configuradas",
    discoveryAvailable: "Descoberta disponível",
    noDiscovery: "Sem descoberta",
    noCatalogDrivers:
      "Nenhum driver predial ou residencial está disponível nesta compilação.",
    configureIntegrations: "Configurar integrações",
    configureHint:
      "Use o editor normal de Fontes de dados. Aplicar usa o Preview/Apply de Engenharia; ativação de Runtime e publicação continuam separadas.",
    locationTitle: "Hierarquia física de locais",
    locationHint:
      "Crie identidades estáveis Unidade → Edifício → Andar → Área/Sala → Zona. O equipamento é associado a um local aqui, não dentro do protocolo.",
    locationCount: "locais",
    editLocation: "Editar local",
    newLocation: "Novo local",
    locationName: "Nome do local",
    locationKind: "Nível do local",
    parentLocation: "Local superior",
    noParent: "Sem superior (raiz)",
    parentHint: "O vínculo será validado pelo Preview canônico de Engenharia.",
    locationHierarchy: "Locais",
    noLocations: "Ainda não há locais",
    emptyLocationHint:
      "Adicione uma unidade ou edifício para começar a hierarquia.",
    workspaceChanged:
      "A Engenharia mudou durante a validação. Recarregue e tente novamente.",
    validationFailed: "O candidato não passou na validação da Engenharia.",
    locationSaved: "Local aplicado ao workspace de Engenharia.",
    validating: "Validando…",
    preview: "Pré-visualizar",
    apply: "Aplicar à Engenharia",
    create: "Criar",
    update: "Atualizar",
    errors: "erros",
    previewValid: "Prévia válida",
    previewInvalid: "Há erros; nada foi aplicado.",
    previewChanges: "Alterações canônicas incluídas nesta prévia",
    noPreviewItems: "O servidor não retornou alterações por entidade.",
    skip: "Ignorar",
    operationError: "Bloqueado",
    deviceTitle: "Descobrir e registrar dispositivos",
    deviceHint:
      "A descoberta não altera o projeto. Selecione um candidato, defina nome e local no EliteSCADA e confira o Preview de Equipamento, TAGs e Comandos antes de aplicar.",
    integration: "Integração",
    noDiscoverySource:
      "Nenhuma integração salva com descoberta de dispositivos.",
    configureFirst:
      "Configure e aplique uma integração na aba Integrações. Drivers que só navegam continuam no navegador existente de TAGs.",
    discover: "Descobrir dispositivos",
    discovering: "Descobrindo…",
    discoveryResults: "Candidatos",
    noCandidates: "A descoberta não retornou dispositivos.",
    endpointOnly: "Resultado de endpoint",
    endpointOnlyHint:
      "Este resultado não contém uma proposta de dispositivo composta. Nenhuma alteração de Engenharia foi feita; use o browse existente do driver para selecionar TAGs.",
    locationHintLabel: "Local sugerido (não atribuído)",
    driverIssues: "avisos do driver",
    selectCandidate: "Selecione um candidato para revisar antes da importação.",
    deviceMaterialization: "Candidato e Preview/Apply",
    tags: "TAGs",
    commands: "Comandos",
    capabilities: "capacidades",
    deviceName: "Nome do equipamento",
    devicePath: "Caminho estável na Engenharia",
    assignLocation: "Local no EliteSCADA",
    unassigned: "Sem local por enquanto",
    locationAssignmentHint:
      "A indicação do driver é apenas uma sugestão. Escolha aqui um local canônico ou deixe o equipamento explicitamente sem local.",
    sourceChanged:
      "A integração selecionada mudou. Recarregue o workspace e execute a descoberta novamente.",
    deviceApplied:
      "Proposta aplicada ao workspace de Engenharia. Salvar ou publicar continua sendo uma ação separada.",
    applying: "Aplicando…",
    discoveryReady: "Pronto para descobrir dispositivos",
    discoveryReadyHint:
      "Selecione Descobrir dispositivos para consultar esta integração. A descoberta não altera a Engenharia.",
    registeredDevices: "Equipamentos registrados",
    noDevices: "Nenhum dispositivo foi materializado ainda.",
    filterIntegration: "Filtrar por integração",
    filterLocation: "Filtrar por local",
    allIntegrations: "Todas as integrações",
    allLocations: "Todos os locais",
    noIntegration: "Sem integração vinculada",
    unknownIntegration: "Integração desconhecida",
    unknownLocation: "Local desconhecido",
    noFilteredDevices: "Nenhum equipamento corresponde aos filtros.",
    bindingRole: "Papel do vínculo",
    advancedDetails: "Detalhes avançados / diagnóstico",
    location: "Local",
    kinds,
    workspaceStatus: "workspace",
    createLabel: "Criar",
    updateLabel: "Atualizar",
    previewHint: "A prévia não altera o workspace.",
  };
}
