# Primer proyecto: de la entrada al Runtime

## Primer proyecto: de la entrada a la primera lectura

Cree una variable de prueba, muestre 42 en pantalla y compruebe que la aplicación funciona.

### Qué va a montar

Una pequeña función coloca 42 en Demo.Level y una pantalla lo muestra. Aprende el recorrido sin conectar equipos. Use instalación de prueba y cuenta autorizada. Si hay aplicación operativa, confirme el entorno con su responsable antes de editar.

### Entrar y abrir proyecto de prueba

1. Abra la dirección de prueba proporcionada.
2. En primer acceso siga Administrador inicial; para recuperar aplicación use Restaurar backup. No suponga contraseña predeterminada.
3. Entre con su cuenta. Si aparece Crear nuevo proyecto, introduzca clave planta-piloto y nombre Planta piloto. La clave identifica; el nombre es el título mostrado.
4. Elija Crear proyecto y abrir Engineering y compruebe el nombre. Si existe proyecto o alguien acaba de crearlo, recargue/compruebe; no borre datos para mostrar la opción.

### Poner un valor en la variable

1. En Fuentes prepare Memoria del Servidor: guarda el valor sin dirección de equipo.
2. En TAGs cree Demo.Level tipo Double y asocie la fuente. Double admite decimales. Aplique mediante el editor.
3. En Scripts cree Python habilitado con ejecución Server. Declare Demo.Level como ServerMemoryTag: TAG de memoria del servidor.
4. En entradas elija Initialize y función initialize_demo. Initialize ejecuta al iniciar revisión.
5. Pegue código y valide. Publica 42.0/Good, una muestra conocida válida para este ejercicio. Publica una vez al inicio, no continuamente ni desde sensor.

```python
def initialize_demo():
    publish_server_memory_sample("Demo.Level", 42.0, "Good")
```

### Mostrar el valor en pantalla

1. Cree Pantalla para el dispositivo de prueba.
2. Añada Texto titulado Ensayo de nivel.
3. Añada Visualización de valor y ajuste tamaño/posición/decimales en Inspector.
4. En vínculo de datos elija Demo.Level. El vínculo sigue la variable; texto fijo 42 no prueba lectura.
5. Previsualice, revise avisos y aplique. Prepare navegación/apertura según presentación. No hace falta un comando.

### Ejecutar la versión preparada

La edición es un borrador llamado Working. Guardar crea una copia identificada: revisión. Publicar prepara una revisión para uso. Activar ejecuta la publicada: se convierte en Active. En lifecycle/revisiones Guarde, elija la correcta, Publique y Active en prueba. Espere confirmaciones. En TAG Monitor revise Active, Demo.Level y 42/Good. Abra pantalla Runtime y compare. Texto fijo 42 sin muestra no pasa el ejercicio.

### Consulte campos cuando necesite

Siga el ejercicio y vuelva a la tabla si un campo no está claro. Nombres técnicos identifican campos; los selectores generan/eligen IDs internos.

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

### Si falta algo

Empiece en TAG Monitor. Sin muestra: fuente, Script habilitado, referencia declarada, Initialize y función initialize_demo; lea diagnóstico. TAG correcto/pantalla incorrecta: revise pantalla/vínculo. Si Guardar/Publicar/Activar fue rechazado, lea mensaje/estado antes de repetir. Visualización basta para observar. Después use guía del driver y comprobación independiente para conectar equipo real.

[Abrir esta guía en Ayuda instalada](/help?topic=getting-started.first-project)
