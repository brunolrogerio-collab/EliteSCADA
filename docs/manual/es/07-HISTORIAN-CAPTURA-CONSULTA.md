# Historian: captura y consulta

## Historian: configurar captura y comprobar histórico

Elija qué guardar, en qué condiciones y cómo comprobar almacenamiento.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-HISTORIAN: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=historian.capture-query; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Dónde configurar y consultar

Ingeniería > Historian lista TAGs habilitados con estrategia/deadband/período. Para configurar abra TAGs, seleccione y expanda Histórico/Historian. Monitor en vivo lee actual; Histórico/Informes/Intelligence necesitan muestras almacenadas. Habilitar hoy no recupera pasado.

### Campos de captura TAG

Use los nombres admitidos en estrategia. Texto desconocido/configuración incompleta puede mantener captura legada de todas las observaciones, no la reducción esperada. Compruebe configuración/muestras; rellenar no garantiza ahorro.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Histórico habilitado | Habilita captura TAG. Deshabilitar no elimina histórico ni crea backup. | Habilite TAG presión de prueba |  |
| Estrategia | periodic respeta período; onChange registra cambios; onChangeDeadband exige variación numérica; onChangeDeadbandMaxInterval añade intervalo máximo entre observaciones aceptadas que llegan. | onChangeDeadbandMaxInterval |  |
| Deadband | Cambio absoluto mínimo desde último aceptado. No es porcentaje ni límite de alarma. Use estrategia numérica compatible. | 0.1 para cambio de una décima | Número finito ≥ 0 |
| Período (ms) | Usado por periodic. 1000 ms = 1 segundo. Selecciona observaciones entrantes, no aumenta polling ni inventa muestras si fuente calla. | 1000 | Entero positivo |
| Período máximo (ms) | Usado por onChangeDeadbandMaxInterval para aceptar observación al vencer tiempo pese a cambio pequeño. No garantiza captura durante corte de comunicación. | 60000 para hasta un minuto entre aceptaciones con observaciones llegando | Entero positivo |

### Ejemplo del efecto de deadband

Con calidad constante, deadband 0.1 y último aceptado 1.00, 1.03 puede omitirse; 1.12 supera umbral. Se compara con último aceptado, no cada rechazado. Primera observación/transiciones de calidad tienen tratamiento propio y pueden guardarse sin variación. Modo de intervalo máximo también acepta por tiempo. No deduzca conteo solo por deadband.

### Procedimiento de comprobación

1. Revise TAG en vivo/tipo/escala/calidad.
2. Habilite Histórico y estrategia/campos válidos.
3. Preview/Apply y guarde/publique/active.
4. Produzca cambios conocidos de prueba y anote período.
5. Abra Histórico Runtime, Muestras y filtro TAG.
6. Compare valores/calidad/horas/conteo con estrategia.
7. Repita con valor estable y cambio de calidad. Para Intelligence compruebe origen temporal/alineación.

### Consultar: período, filtros y páginas

Elija dataset correcto: muestras no son transiciones de alarma. Use relativo corto o Desde/Hasta válido; navegador actual limita ventana a 31 días por consulta. Añada campos/operadores del schema con tipo correcto: número/booleano/fecha/texto/enum. Ordenar no filtra. Páginas continúan consulta sin ampliar período. Sin resultados quite filtros uno a uno y confirme captura existente.

### Captura, retención, backup y Playback difieren

Captura decide observaciones admitidas; retención cuánto permanecen; agregación resume; backup conserva copia recuperable. Campos de captura no son cuota global ni archivo. Playback presenta valores históricos en la visualización actual, no restaura proceso ni autoriza comandos; vuelva en vivo antes de operar. Sin muestras no significa cero ni equipo parado.

[Abrir esta guía en Ayuda instalada](/help?topic=historian.capture-query)
