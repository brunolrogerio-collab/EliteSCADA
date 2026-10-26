# Primer proyecto: de la entrada al Runtime

## Primer proyecto: de la entrada a la primera lectura

Siga un ensayo de ingeniería hasta comprobar una muestra conocida en Runtime sin conectar equipos.

### Prepare el entorno

Use instalación de prueba dedicada y cuenta autorizada. Compruebe dirección/proyecto; no aprenda activación en una planta operativa. El ensayo lee memoria interna, no sensor real. Si hay aplicación, no haga detach para forzar inicio: use proyecto de prueba autorizado o pida preparar el entorno.

### Entrar y crear proyecto

1. Abra dirección correcta. En instalación nueva elegible complete Administrador inicial; para recuperar datos use Restaurar backup. No suponga credenciales predeterminadas.
2. Entre con cuenta autorizada.
3. Si se ofrece Crear nuevo proyecto, introduzca Clave y Nombre del proyecto y Crear proyecto y abrir Engineering.
4. Confirme contexto. Si no aparece, revise si existe proyecto y siga su flujo. Crear no equivale a nueva aplicación Active. Si otro usuario creó mientras estaba abierto, recargue/compruebe en vez de repetir a ciegas.

### Campos del ensayo

Los nombres son ejemplos de prueba. Use referencias legibles; los flujos del producto seleccionan/generan IDs internos.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Clave del proyecto | Identidad estable distinta del título. | planta-piloto | 1–200 caracteres tras recortar extremos; no es clave de licencia ni ID para adivinar. |
| Nombre del proyecto | Nombre legible de Ingeniería. | Planta piloto | 1–300 caracteres tras recortar extremos; confirme destino. |
| Fuente del TAG | Seleccione/cree Server Memory del catálogo, sin host/puerto. | Memoria del Servidor | Provider interno, no driver de equipo ni memoria del navegador. |
| Path del TAG | Referencia usada por Script y pantalla. | Demo.Level | Asocie a fuente preparada; path solo no produce muestra. |
| Tipo | Tipo numérico compatible con la muestra. | Double | No use String para ocultar incompatibilidad. |
| Scope del Script | Server publica la muestra del servidor. | Server | Client Visual ejecuta en cliente y no sustituye el publicador. |
| Gatillo y handler | Entrada Initialize con nombre exacto de función. | Initialize / initialize_demo | El nombre solo no configura gatillo; habilite Script/entrada según editor. |
| Referencia declarada | Declare Demo.Level como ServerMemoryTag en referencias del Script. | Demo.Level | La API exige memoria declarada; texto de código no sustituye configuración. |
| Vínculo de visualización de valor | Seleccione Demo.Level en selector del objeto. | Valor Demo.Level | No adivine GUID ni deje 42 fijo aparentando muestra. |
| Pantalla | Cree título/visualización y configure apertura/navegación disponibles. | Pantalla de ensayo | Crear no garantiza apertura automática en Runtime. |

### Producir muestra conocida

1. Prepare Server Memory y TAG Double Demo.Level en Fuentes/TAGs y aplique cambios válidos a Working.
2. Cree Server Script Python habilitado y declare Demo.Level como ServerMemoryTag.
3. Añada Initialize con handler initialize_demo.
4. Use código y valide. Publica 42.0/Good por ser valor conocido del ensayo; no mide equipo. No es continuo: Initialize publica al ejecutarse la entrada al iniciar revisión. Para actualizaciones periódicas estudie Timer/cadencia en Scripts, sin bucle infinito.

```python
def initialize_demo():
    publish_server_memory_sample("Demo.Level", 42.0, "Good")
```

### Montar primera pantalla

1. Abra Pantallas y cree una para el dispositivo.
2. Añada Texto con título Ensayo de nivel.
3. Añada Visualización de valor, ajuste posición/tamaño/decimales en Inspector.
4. Elija Demo.Level en vínculo. Unidad solo si corresponde al significado; aquí puede quedar vacía.
5. Previsualice, lea diagnósticos y aplique edición.
6. Configure presentación/navegación soportadas para abrirla. No añada escritura para probar lectura.

### Guardar, publicar, activar y comprobar

Abra lifecycle/revisiones y compruebe cambios/validación. Guardar crea revisión persistida; seleccione correcta y Publique. Publicar prepara sin hacer Active automáticamente. Active la publicada cuando deba ejecutarse en prueba. Compruebe resultado/revisión Active. En TAG Monitor elija Demo.Level y espere 42/Good, revisando horas y origen de memoria interna. Abra pantalla en Runtime y compare. Texto fijo 42 sin muestra del TAG no pasa el ensayo. Aplicar al Working no demuestra ejecución.

### Si no aparece

Compruebe proyecto/Active antes de cambiar dibujo. Sin muestra: fuente/TAG, referencia declarada, Script habilitado, Initialize/handler y diagnóstico. TAG válido/pantalla vacía: pantalla abierta, vínculo, objeto/formato. Visualización basta para observar; acciones interactivas exigen clase/permiso. Si Guardar/Publicar/Activar falla, lea mensaje/estado antes de repetir; una etapa no implica siguientes. Para equipo físico use guía de su driver y verifique valor/calidad independientemente. No es prueba de campo.

[Abrir esta guía en Ayuda instalada](/help?topic=getting-started.first-project)
