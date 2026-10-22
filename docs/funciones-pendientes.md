# Funciones pendientes y alcance real

Revisión histórica del 5 de octubre de 2026. Esta lista distingue código faltante de configuración externa y de verificaciones para esa fecha; revisa el informe actual antes de usarla como estado vigente. No se considera implementada una función solo porque la suite general pasa.

## Requisitos de la misión

| Función | Estado real | Qué falta |
|---|---|---|
| Registro, login/logout, perfil, recuperación y restablecimiento | Flujos implementados; Identity y reset comprobados con un doble SMTP | Habilitar/verificar la entrega real del correo con el proveedor. El código SMTP existe; esto no requiere construir un nuevo módulo. |
| Catálogo público y protección Admin | Implementados; lectura pública filtrada y autorización en servidor | No se identificó una función obligatoria ausente en estos flujos revisados. |
| Carrito, cantidades, variantes, personalización | Implementados; los precios se recalculan en servidor | No se identificó una función obligatoria ausente. La prueba de cantidad comprueba actualización válida y rechazo de exceso. |
| Pedidos, cancelación, inventario y movimientos | Implementados; transacciones, concurrencia y reposición idempotente | Se corrigió durante el cierre la omisión de dirección complementaria/código postal en los detalles y confirmación. No se deja esa omisión pendiente. |
| Métodos de pago habilitados | Solo se ofrecen CashOnDelivery/BankTransfer si se configuran; sin configuración se bloquea el checkout | Confirmar y habilitar los métodos reales del negocio. Registrar el estado de un pago no equivale a cobrarlo automáticamente. |
| Contacto | Guarda mensajes, permite al administrador leerlos/eliminarlos y encola un aviso a la tienda | El aviso usa `ContactEmail` de `SiteSetting` y depende de que SMTP entregue el mensaje. La entrega al proveedor no está comprobada. |
| Newsletter | Retirada según la misión | No falta implementar suscripción: se pidió eliminar la función ficticia. |

## Funciones parciales existentes, fuera del mínimo explícito de la misión

| Función | Evidencia de implementación parcial | Trabajo que requeriría completarla |
|---|---|---|
| Guía de envío del pedido | `Order.TrackingNumber`, `OrderDto.TrackingNumber` y lectura en `Views/Order/Details.cshtml`; no hay acción de guardado ni formulario Admin para ese campo | Campo administrativo validado, servicio de actualización y prueba de autorización/persistencia. No se confundió con las notas internas, que sí funcionan. |
| Portada del blog | `BlogPost.CoverImage`, DTO de lectura y visualización; `BlogPostInputDto`, `BlogService.SaveAsync` y la vista Edit no la gestionan | Entrada de portada, validación, persistencia y reutilización segura del servicio de cargas. El CRUD de texto/publicación sí existe. |
| Contenido enriquecido del blog | El contenido se presenta como texto codificado, preservando saltos de línea | Un editor HTML real requeriría sanitización y política de etiquetas. Se corrigió el texto del formulario que prometía HTML para que describa la función real. No se añadió renderizado inseguro. |

Estas funciones parciales se documentan como posibles ampliaciones, no como funcionalidades completas ni como trabajo obligatorio entregado por las pruebas de CRUD.

## Ampliaciones nuevas opcionales

**Pasarela de tarjeta y cobro automático:** no implementados. Requieren proveedor, credenciales, webhooks, idempotencia y conciliación. La misión exigía no anunciar métodos inexistentes, no integrar una pasarela.

**Confirmación de correo o cambio de contraseña desde el perfil:** no hay un flujo específico para estas opciones; existe recuperación/restablecimiento. La lista explícita de la misión no exigía esas ampliaciones. No se presentan como parte del perfil actual.

**Operación en varias instancias:** necesita sesión/caché y claves compartidas. Es trabajo de despliegue, no una pantalla comercial faltante.

No se modificaron datos reales ni se activaron métodos de pago, cuentas bancarias, SMTP o servicios externos sin información confirmada del negocio.
