# Parte I — Fundamentos de EliteSCADA

## 1. Sobre EliteSCADA

EliteSCADA separa claramente la autoría de la aplicación, la ejecución industrial y las autoridades administrativas. El proyecto se prepara en Engineering; Runtime ejecuta la revisión Active; Authority controla la identidad y la autorización; Licensing controla la habilitación comercial; Historian conserva los datos históricos conforme a sus propias reglas.

Esta separación es importante en la operación: editar una aplicación no cambia automáticamente lo que está en ejecución, una licencia no concede autorización de seguridad y un backup de Authority no reemplaza un paquete de aplicación ni un backup de base de datos/histórico.

**Ayuda relacionada:** `/help?topic=engineering.overview`, `/help?topic=runtime.overview`.

## 2. Conceptos fundamentales

- **Working**: estado editable de Engineering.
- **Revision**: versión identificable guardada a partir de Working.
- **Published**: revisión publicada y elegible para activación conforme a la validación.
- **Active**: revisión que constituye la autoridad de aplicación para Runtime.
- **TAG**: identidad estable para datos de proceso/ingeniería consumidos por el TAG Engine.
- **Data Source**: origen de datos configurado. Los Drivers de comunicación y los proveedores internos de fuentes son categorías distintas.
- **Authority**: identidad, autenticación, roles y autorización efectiva.
- **Engineering Lock**: protección opcional del contenido de Engineering de la aplicación; es independiente de la contraseña del usuario y de la contraseña del backup de Authority.
- **License**: habilitación comercial vinculada a la máquina; no reemplaza la autenticación ni la autorización.

## 3. Arquitectura del producto para el usuario

La ruta normal de operación mantiene la autoridad en el backend. Las interfaces usan capabilities para orientar la UX, pero la validación y la autorización no dependen solamente de los controles visibles en el navegador.

Para los datos industriales, los consumidores trabajan a través de los contratos públicos del TAG Engine/cache/eventos. Una pantalla, un script o un informe no debe abrir una conexión privada del Driver para omitir el Data Source, Authority o el lifecycle.

## 4. Engineering y Runtime

Engineering y Runtime tienen responsabilidades distintas.

En Engineering, el usuario crea y valida la aplicación. Los cambios permanecen en Working hasta seguir el lifecycle. En Runtime, la aplicación ejecutada es la revisión Active registrada por el backend.

### Procedimiento: cambiar la aplicación sin afectar Runtime inmediatamente

**Objetivo:** preparar un cambio de Engineering mientras la revisión Active sigue en ejecución hasta una activación explícita.

**Requisitos previos:** sesión con capabilities de Engineering compatibles y acceso a la aplicación.

**Pasos:**
1. Abra el área de Engineering.
2. Edite el recurso deseado en Working.
3. Valide el cambio antes de persistirlo/aplicarlo cuando la superficie ofrezca Preview.
4. Guarde Working conforme al flujo del producto.
5. Cree/seleccione la Revision correspondiente.
6. Publique la revisión cuando esté lista.
7. Actívela solamente cuando el cambio esté autorizado para entrar en operación.

**Resultado esperado:** Runtime sigue asociado a la revisión Active anterior hasta el paso explícito de Activate.

**Diagnóstico:** si una edición parece haber cambiado Runtime sin Activate, confirme primero qué revisión informa el backend como Active; no confíe solamente en el estado visual local.

**Ayuda relacionada:** `/help?topic=engineering.lifecycle`.

## 5. Application / Authority / License / Historian

Estos dominios no son intercambiables:

| Dominio | Responsabilidad |
|---|---|
| Application | Contenido de Engineering y lifecycle de la aplicación |
| Authority | Identidades, roles y autorización |
| License | Habilitación comercial, capacidad y condiciones de Runtime |
| Historian | Persistencia y consulta de datos históricos |

Un `.escadapkg` no es un backup de Authority ni una base de datos de Historian. Licensing es una preocupación separada de instalación y puede tratarse por separado durante la recuperación.

## 6. Lifecycle del proyecto

El flujo fundamental es:

`Working -> Save -> Revision -> Publish -> Activate`

Save no es Publish. Publish no es Activate. Apply de un Preview de importación tampoco equivale a Activate.

### Resultado operacional

- Working sigue siendo el área de autoría.
- Revision proporciona identidad duradera al estado guardado.
- Published indica la revisión publicada.
- Active define lo que ejecuta Runtime.

### Errores comunes

- Suponer que guardar cambia Runtime.
- Tratar Preview como una mutación.
- Tratar Apply de importación como activación.
- Usar el estado del frontend como autoridad del lifecycle.

**Ayuda relacionada:** `/help?topic=engineering.lifecycle`, `/help?topic=packages.escadapkg`.
