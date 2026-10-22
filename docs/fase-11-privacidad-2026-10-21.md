# Fase 11 — auditoría y privacidad

## Cambios

- La dueña y Developer pueden editar o eliminar cuentas Customer; los cambios de correo actualizan el usuario de acceso, invalidan la confirmación previa y encolan un enlace de confirmación al correo nuevo. La confirmación de eliminación exige volver a escribir el correo y usa antifalsificación.
- La baja elimina direcciones y favoritos asociados, desvincula y anonimiza reseñas y conserva los pedidos, sus artículos, importes y estados. Se anonimizan nombre, correo, teléfono, dirección, ciudad, provincia, código postal, observaciones y campos personalizados de regalo. La acción deja una entrada de auditoría con cantidades, sin incluir esos datos personales.
- Los listados de Developer y Owner enmascaran el contacto. La revelación de clientes por Developer registra el acceso. Operadores ven el detalle comercial del pedido sin datos de contacto/dirección; el acceso de Owner/Developer a un detalle con información identificable queda auditado y se envía con `Cache-Control: no-store`.
- El acceso al comprobante registra actor, rol, pedido objetivo y resultado sin copiar contenido del archivo. Los cambios de estado del pedido y pago guardan estado anterior/nuevo, sin notas privadas.
- El filtro de cuentas comerciales de Owner se aplica en SQL antes de paginar y solo incluye cuentas con rol único Customer u Operator.

## Verificación

`SecurityHardeningTests` cubre edición de perfil/correo, duplicados, límites de roles y confirmación/anonimización de baja. La suite Release del 21 de octubre pasó 234 pruebas, con 0 fallos y 1 prueba de navegador optativa omitida; ver `audit/test-results/fase-integral-final.trx` (artefacto local ignorado por Git).

La anonimización elimina la información de contacto de pedidos existentes. Esto favorece privacidad; el negocio debe confirmar que cumple sus obligaciones de conservación contable antes de borrar cuentas con transacciones reales.
