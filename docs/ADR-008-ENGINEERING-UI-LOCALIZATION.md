# ADR-008 — Engineering UI localization

## Status
Accepted and active.

## Context

EliteSCADA is intended for industrial engineering use across Portuguese-, English- and Spanish-speaking environments. The developer/engineering user must be able to work with the same project and the same public Engineering model while choosing the language of the Engineering/development interface.

Localization must not corrupt or fork the Engineering contract. A translated menu, property label or validation message is presentation. Stable identifiers, TAG paths, addresses, schema keys and runtime semantics are engineering data and must remain language-neutral/stable.

## Decision

### Supported Engineering interface languages

The Engineering/development interface must support user selection among:

- Portuguese (Brazil) — `pt-BR`;
- English — `en`;
- Spanish — `es`.

The choice applies consistently across all developer-facing Engineering surfaces, including:

- Data Sources and driver configuration;
- TAG engineering;
- database/historian configuration and diagnostics;
- alarm engineering;
- Equipment Templates, Equipment and Dynamos;
- screens and popups;
- trends;
- project/revision/save/publish/activate workflows;
- users, roles and security administration;
- driver-module administration and diagnostics;
- menus, dialogs, property editors, validation messages and built-in engineering help text.

### Stable Engineering model

Changing the UI language must not change:

- internal IDs;
- TAG paths;
- communication addresses;
- enum/storage values;
- public JSON/CSV/XLSX schema keys;
- project revision identity;
- runtime behavior or authorization semantics.

Product code should use localization/resource keys for product-owned text rather than persisting translated UI labels as authoritative configuration values.

### User preference

The selected language is a user-interface preference. When the user/profile lifecycle exists, the preference should be persistable per user/profile so the Engineering environment opens in that user's selected language.

Automatic detection, default/fallback order and missing-resource behavior are implementation details to be finalized during the localization slice.

### Runtime HMI distinction

This ADR applies to the **Engineering/development interface** itself.

Multilingual text inside the runtime HMI/application being engineered is a separate capability. Localizing the editor does not automatically make process screens multilingual, and future runtime-language engineering must be modeled explicitly if/when introduced.

## Consequences

- Engineering screens must share one localization infrastructure rather than each feature inventing its own translation mechanism.
- New developer-facing features must provide resource keys/translations for the supported languages.
- Automated tests should eventually validate language switching and verify that changing language does not mutate Engineering data.
- Plugin/driver modules that contribute Engineering UI should integrate with the same localization mechanism for product-consistent Portuguese/English/Spanish presentation while retaining language-neutral configuration schemas.

## Deferred implementation details

The following are intentionally deferred:

- localization library/framework choice in React;
- translation-file/package format;
- exact fallback locale;
- whether module translations are bundled or separately loaded;
- runtime HMI multilingual engineering model.

The locked decision is that the EliteSCADA Engineering/development UI supports **Portuguese, English and Spanish selectable by the developer user**, without changing the underlying Engineering semantics.