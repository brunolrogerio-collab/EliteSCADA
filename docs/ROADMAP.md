# Roadmap baseline

The approved development north is preserved. The immediate execution slice is:

1. Foundation/repository/architecture.
2. Tag Engine + quality + current-value cache + internal Event Bus.
3. Simulation Driver.
4. Driver SDK contract.
5. PostgreSQL/TimescaleDB persistence.
6. REST + WebSocket.
7. Modbus TCP + MQTT.
8. First runtime screen and equipment modal.

Only after the 0.1 runtime is stable should the SVG editor become the main development focus.

## Cross-cutting requirement — Engineering Import/Export

This requirement applies to all roadmap phases. Every engineering entity introduced from this point forward must define a stable serialization contract and participate in the common validation/preview/apply pipeline.

Current implementation: Tags and Alarms in JSON/CSV.
Next expansions: XLSX, data sources, historian configuration persistence, screens, dynamos, templates, popups, SQL mappings, plugins and complete project packages.
