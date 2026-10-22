# Fase 1: ajuste de artículos del pedido

## Cambio

Se agregaron estilos de fila y miniatura que faltaban para el resumen del checkout, la confirmación y el detalle de pedidos. La imagen tiene dimensiones controladas y `object-fit: cover`; el texto y la personalización pueden partir líneas largas y el precio queda dentro de la tarjeta. Se conserva la paleta y las variables de tema claro/oscuro existentes.

La auditoría de navegador ahora exige que el checkout tenga al menos un producto y comprueba que imagen, texto y precio no se salgan de cada fila. Comprueba también la ruta real, el ancho del documento y el tema oscuro en checkout, confirmación y detalle. El producto con nombre largo solo se establece en la base temporal del test de navegador.

## Validación

- `dotnet build Bisuteria.slnx -c Release`: correcto, 0 advertencias y 0 errores.
- `dotnet test Bisuteria.slnx -c Release --no-build`: 233 aprobadas, 0 fallidas, 1 omitida (auditoría de navegador opcional).
- `node --check tests/visual/browser-audit.cjs` y `git diff --check`: correctos.
- Auditoría rápida en Kestrel contra `BisuteriaAudit_<GUID>`: el flujo de compra pasó; 18 combinaciones página/ancho registradas entre 360, 768 y 1366 px, sin desbordes horizontales ni filas fuera de su contenedor. También se tomaron capturas oscuras para las tres vistas.
- axe informó 18 hallazgos en el subconjunto auditado. La línea base completa ya mostraba problemas de contraste; esta fase no modifica colores y no declara conformidad WCAG.
- `codex review --uncommitted`: no encontró errores concretos. No reemplaza la ejecución de build, tests ni navegador.

Las capturas e informes antes/después están localmente bajo `audit/browser/before/` y `audit/browser/after/`; no se versionan como imágenes binarias. Lighthouse móvil se volverá a medir en la fase final de interfaz.
