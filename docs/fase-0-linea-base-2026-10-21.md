# Fase 0: preparación y línea base

Fecha de la revisión local: 21 de octubre de 2026.

## Estado del repositorio

- Rama de trabajo: `mejoras/envios-checkout-ui`, creada desde `main` en el commit `0270fbe`.
- Se agregó el `AGENTS.md` de la raíz con arquitectura, reglas de seguridad, datos, pruebas, Git y comandos reproducibles.
- No se instalaron paquetes, skills ni servidores MCP.

## Herramientas disponibles y utilizadas

- `codex features list` confirmó funciones de navegador, automatización de interfaz, revisión de código, plugins y agentes.
- La solución usa `dotnet`, SQL Server y pruebas aisladas. Para la revisión de navegador ya estaban disponibles Playwright, axe-core y Lighthouse; se usaron mediante `tests/visual/browser-audit.cjs`.
- Se usaron comandos locales de Git, PowerShell, `dotnet`, Node y Playwright. Tres agentes revisaron código en modo de solo lectura para envíos/pagos, navegación del panel y páginas públicas/seguridad.
- La automatización de navegador de Codex no tenía una pestaña local del proyecto disponible. El navegador Playwright de la auditoría existente cubrió las páginas reales desde un Kestrel local temporal.
- No se encontró un servidor MCP de documentación oficial de .NET/EF Core/Meta configurado. No se instaló uno.
- El entorno de esta sesión declara acceso a archivos sin sandbox y no ofrece un control para convertirlo al sandbox restringido que pide el documento de alcance; no se cambió la configuración del host.

## Verificaciones

- `dotnet build Bisuteria.slnx -c Release`: correcto, 0 advertencias y 0 errores.
- `dotnet test Bisuteria.slnx -c Release --no-build`: 233 aprobadas, 0 fallidas y 1 omitida por ser la auditoría de navegador opcional.
- `dotnet list Bisuteria.slnx package --vulnerable --include-transitive --no-restore`: no informó vulnerabilidades en Domain, Application, Infrastructure, Web ni Tests. El comando consultó la fuente NuGet configurada; no se instaló ningún paquete.
- Auditoría de navegador con la base temporal aislada `BisuteriaAudit_<GUID>`: 111 capturas entre 360, 768 y 1366 px; seis flujos completos; cero fallos estructurales, fallos de flujo, errores de consola o imágenes rotas. axe encontró 84 instancias de contraste insuficiente. Lighthouse móvil: rendimiento 62, accesibilidad 94, buenas prácticas 100 y SEO 100.
- Las capturas e informes quedan en `audit/browser/before/`; esa carpeta es un artefacto local y no contiene la base de datos temporal.
- No se usó la base de datos con datos reales del negocio.

## Hallazgos priorizados

- `.checkout-thumb` se usa en el checkout, la confirmación y el detalle del pedido, pero no tenía estilos; la auditoría base no detectó overflow global. La Fase 1 agrega una comprobación específica para el contenido de esas filas.
- El checkout no implementa zonas/sectores de envío de Manta y Jaramijó, y mantiene código postal en sus modelos y pantallas. La tarifa gratuita tiene fuentes incompatibles; no se inventarán sectores, tarifas ni tiempos.
- El panel aplica permisos a enlaces, pero la navegación es plana y el botón móvil no abre el menú.
- La tienda pública conserva pendientes de contraste y SEO descritos en los informes previos; se registrarán los resultados nuevos sin afirmar conformidad legal o WCAG completa.
- La cola de correo ya existe. En Desarrollo, si `Smtp:Enabled` es falso, los mensajes se guardan localmente en `src/Bisuteria.Web/App_Data/MailDrop` y no se envían. La configuración local actual tiene SMTP habilitado; el proceso web debe reiniciarse para cargar cambios en User Secrets. Los archivos MailDrop no demuestran entrega real.

## Límites

La auditoría cubre el código y una base temporal aislada en este equipo. No valida el dominio, proxy, hosting, entrega real de correo ni textos legales aprobados por la propietaria. Esas decisiones se mantienen abiertas para la entrega final.
