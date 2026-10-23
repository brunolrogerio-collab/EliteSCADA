# Part I — EliteSCADA Fundamentals

## 1. About EliteSCADA

EliteSCADA clearly separates application authoring, industrial execution, and administrative authorities. The project is prepared in Engineering; Runtime executes the Active revision; Authority controls identity and authorization; Licensing controls commercial entitlement; Historian preserves historical data according to its own rules.

This separation matters operationally: editing an application does not automatically change what is running, a license does not grant security authorization, and an Authority backup does not replace an application package or a database/history backup.

**Related Help:** `/help?topic=engineering.overview`, `/help?topic=runtime.overview`.

## 2. Fundamental concepts

- **Working**: editable Engineering state.
- **Revision**: an identifiable version saved from Working.
- **Published**: a published revision eligible for activation according to validation.
- **Active**: the revision that constitutes the application's authority for Runtime.
- **TAG**: stable identity for process/engineering data consumed by the TAG Engine.
- **Data Source**: configured data origin. Communication Drivers and internal source providers are distinct categories.
- **Authority**: identity, authentication, roles, and effective authorization.
- **Engineering Lock**: optional protection for the application's Engineering content; it is independent of the user's password and the Authority backup password.
- **License**: machine-bound commercial entitlement; it does not replace authentication or authorization.

## 3. Product architecture from the user's perspective

The normal operating path keeps authority in the backend. Interfaces use capabilities to guide UX, but validation and authorization do not depend only on visible browser controls.

For industrial data, consumers work through public TAG Engine/cache/event contracts. A screen, script, or report must not open a private Driver connection to bypass the Data Source, Authority, or lifecycle.

## 4. Engineering vs. Runtime

Engineering and Runtime have different responsibilities.

In Engineering, the user creates and validates the application. Changes remain in Working until they follow the lifecycle. In Runtime, the executed application is the Active revision registered by the backend.

### Procedure: change the application without immediately affecting Runtime

**Goal:** prepare an Engineering change while the Active revision continues running until an explicit activation.

**Prerequisites:** a session with compatible Engineering capabilities and access to the application.

**Steps:**
1. Open the Engineering area.
2. Edit the desired resource in Working.
3. Validate the change before persisting/applying it when the surface offers Preview.
4. Save Working according to the product workflow.
5. Create/select the corresponding Revision.
6. Publish the revision when it is ready.
7. Activate it only when the change is authorized to go into operation.

**Expected result:** Runtime remains associated with the previous Active revision until the explicit Activate step.

**Troubleshooting:** if an edit appears to have changed Runtime without Activate, first confirm which revision the backend reports as Active; do not rely only on local visual state.

**Related Help:** `/help?topic=engineering.lifecycle`.

## 5. Application / Authority / License / Historian

These domains are not interchangeable:

| Domain | Responsibility |
|---|---|
| Application | Engineering content and application lifecycle |
| Authority | Identities, roles, and authorization |
| License | Commercial entitlement, capacity, and Runtime conditions |
| Historian | Persistence and query of historical data |

A `.escadapkg` is not an Authority backup or a Historian database. Licensing is a separate installation concern and may be handled separately during recovery.

## 6. Project lifecycle

The fundamental flow is:

`Working -> Save -> Revision -> Publish -> Activate`

Save is not Publish. Publish is not Activate. Applying an import Preview is not equivalent to Activate either.

### Operational outcome

- Working remains the authoring area.
- Revision gives the saved state a durable identity.
- Published indicates the published revision.
- Active defines what Runtime executes.

### Common mistakes

- Assuming that saving changes Runtime.
- Treating Preview as a mutation.
- Treating import Apply as activation.
- Using frontend state as the authority for lifecycle.

**Related Help:** `/help?topic=engineering.lifecycle`, `/help?topic=packages.escadapkg`.
