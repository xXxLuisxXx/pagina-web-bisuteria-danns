# Fase 8 — correo transaccional y captura de desarrollo

## Implementación

- En `Development`, el transporte guarda archivos `.eml` en `src/Bisuteria.Web/App_Data/MailDrop` por defecto, aun cuando encuentre credenciales SMTP. Solo `Smtp:Enabled=true` habilita el envío real en ese entorno. `EmailOutbox:PickupDirectory` permite otra carpeta privada y rechaza `wwwroot` y sus descendientes.
- El formato de salida contiene alternativas de texto plano y HTML con el logo existente. El contenido de clientes se escapa; solo los enlaces HTTPS del origen configurado se convierten en enlaces HTML. No se imprime el destinatario, cuerpo, enlace privado ni nombre del archivo en logs.
- Los errores persistidos de reintento son textos controlados de hasta 100 caracteres. No se copian las respuestas del proveedor ni los mensajes de excepciones. Esto evita que un fallo de autenticación provoque otro fallo SQL al guardar su diagnóstico.
- La migración `WidenEmailOutboxRecipient` amplía la columna de destinatario de 200 a 254 caracteres sin modificar datos existentes. La reversión comprueba que no existan valores incompatibles antes de reducirla. No se aplica sobre la base real desde esta revisión.
- Los correos de pedidos incluyen precios unitarios, importes de líneas, total parcial numérico cuando falta cotizar, datos bancarios tomados de `SiteSetting` y acceso seguro a la confirmación. El correo interno incluye destino, contacto, personalización, regalo, observaciones y enlace al pedido; no incluye el token de invitado.
- La carga de comprobantes notifica por separado al cliente que el documento fue recibido y a la tienda que debe revisarlo. Recibir un archivo no marca el pago como aprobado.

## Configuración

El origen público se obtiene de `PublicBaseUrl` y debe ser HTTPS. Los datos bancarios, correo de avisos y demás información del negocio se administran mediante ajustes de la tienda; no tienen valores reales incorporados.

Para Gmail se configuran mediante user-secrets o variables de entorno `Smtp:Host` (`smtp.gmail.com`), `Smtp:Port` (`587`), `Smtp:UseSsl` (`true`, STARTTLS), `Smtp:User`, `Smtp:Password` y `Smtp:From`. La contraseña corresponde a una contraseña de aplicación de Google, no a la contraseña normal de la cuenta. En desarrollo se añade `Smtp:Enabled=true` únicamente cuando se desea enviar a destinatarios reales. Para volver a capturar localmente, se establece `Smtp:Enabled=false` o se elimina esa clave.

Los archivos `.eml` contienen información transaccional y enlaces privados. `App_Data` está excluido de Git, se mantiene fuera del contenido público y debe conservar los permisos del usuario de desarrollo. No se adjuntan estos archivos a la documentación.

La cola SQL y sus eventos conservan la misma arquitectura: la operación de negocio persiste el aviso y el worker intenta entregarlo aparte, con reserva atómica, ocho intentos y retroceso de 30 segundos a una hora. Un fallo de SMTP no revierte la compra o el registro. La pantalla del desarrollador permite revisar el estado y solicitar un nuevo intento con auditoría.

## Verificación

Se añadieron pruebas para captura `.eml` multipart, HTML escapado y enlaces del origen, rechazo de carpetas públicas, opt-in SMTP, límites/redacción del diagnóstico, destinatario de 254 caracteres, resumen completo cliente/tienda, recepción/rechazo de comprobante y eventos de cuenta/envío/reembolso/cancelación. Todas usan datos ficticios, transporte local o la captura del fixture; ninguna debe enviar correos reales.

Build Release: correcto, 0 advertencias y 0 errores. Suite completa Release: 229 correctas, 0 fallidas y 1 omitida (auditoría de navegador optativa). La auditoría dedicada del navegador se ejecuta por separado en la fase 9. La entrega real de Gmail requiere comprobación del proveedor y queda fuera de estas pruebas.
