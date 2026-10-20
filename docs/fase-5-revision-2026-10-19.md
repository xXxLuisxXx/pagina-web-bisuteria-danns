# Fase 5 — Checkout, transferencia y comprobantes

## Cambios

- El carrito y el checkout ya no cobran un importe fijo de envío antes de conocer la dirección. Domicilio y entrega local aparecen como pendientes de cotización; retiro en tienda no añade envío. Se muestra USD con dos decimales y una etiqueta explícita para el precio unitario.
- La provincia se elige entre las 24 provincias ecuatorianas y se valida también en servidor. Dirección no es obligatoria al retirar en tienda.
- Los pedidos que requieren entrega quedan marcados `ShippingToConfirm`. Owner/Developer con el permiso correspondiente registran el recargo en dólares desde el pedido; el servidor recalcula el total y el pago pendiente, escribe auditoría y encola un aviso al cliente. No se permite marcar el pago como recibido mientras falte confirmar el envío.
- La transferencia se habilita desde ajustes y solo si la tienda completa sus datos bancarios. No se agregaron datos bancarios reales ni se aplicó ninguna migración a la base de negocio.
- Los comprobantes admiten hasta tres archivos de 5 MB (JPG, PNG, WebP o PDF). Se validan firma y contenido, las imágenes se recodifican para quitar metadatos, se usan nombres aleatorios y se guardan fuera de `wwwroot`. La descarga exige autorización y los PDF se entregan como adjuntos con cabeceras restrictivas.
- Owner/Developer revisan el comprobante. La aprobación actualiza pago y pedido respetando transiciones; el rechazo exige motivo. Ambos eventos quedan en auditoría y generan correo en la cola.
- Los invitados acceden a la confirmación y al comprobante con un token aleatorio que se entrega en el enlace y solo se almacena como hash. El botón de WhatsApp usa el teléfono configurable de la tienda y prepara un mensaje que el cliente envía; no adjunta comprobantes ni dirección.

## Migración y datos

Se creó `20261020021142_AddShippingConfirmationAndPaymentProofs`, aditiva: agrega el indicador de cotización, hash de acceso de invitados y tabla de comprobantes. La migración se probó en las bases temporales `BisuteriaAudit_<GUID>` creadas por las pruebas. No se conectó ni migró la base de datos real.

Los datos bancarios y el teléfono de WhatsApp permanecen vacíos hasta que la propietaria los configure desde el panel. La transferencia inicia deshabilitada.

## Pruebas y capturas

- `dotnet build Bisuteria.slnx -c Release`: correcto, 0 advertencias y 0 errores.
- `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=fase5-cierre-retry-20261019.trx' --results-directory audit/test-results`: 199/199 correctas.
- Prueba de renderizado de páginas: 1/1 correcta. Playwright/Edge midió el checkout a 360, 768 y 1366 px, con cero desbordes horizontales, cero errores de página y cero imágenes rotas. Las capturas están en `audit/screenshots/checkout-before-{360,768,1366}.png` y `audit/screenshots/checkout-after-{360,768,1366}.png`; las métricas están en `audit/screenshots/checkout-before-visual.json` y `audit/checkout-phase5-visual.json`.
- La versión anterior se renderizó desde el commit `f4c0b00` en un worktree temporal; el código fuente de esa revisión no se modificó.
- Los comprobantes fueron probados con archivos de prueba, rechazo de contenido activo y cinco cargas concurrentes (límite de tres). No se enviaron correos reales.

Las capturas de esta fase usan datos de prueba y sirven para comparar el resumen y el comportamiento responsive. No son una auditoría visual de todas las páginas; esa inspección corresponde a la fase 9.
