# Fase 5: administrador inicial y correo persistente

## Implementación

InitialDeveloper:Email e InitialDeveloper:Password se leen de configuración privada. El inicializador comprueba los resultados de creación, rol y actualización. La cuenta Developer tiene correo confirmado, debe cambiar la clave inicial y registrar TOTP antes de usar el panel. Se guarda el hash de la clave inicial para impedir reutilizarla tanto al cambiar como al restablecer la contraseña. Los fallos de inicialización detienen producción. No se convierte una cuenta existente del antiguo rol Admin automáticamente.

EmailOutbox se guarda en SQL. Las notificaciones de pedidos, pago, contacto, bienvenida, cambio de contraseña y stock se agregan al mismo SaveChangesAsync que el evento de negocio. Los eventos de pedido incluyen invitados y el correo de la compra. Se conserva la arquitectura: captura de persistencia en Infrastructure, transporte SMTP y worker en Web. No se añadieron dependencias.

El worker reserva mensajes de forma atómica durante cinco minutos, procesa fuera de la solicitud HTTP y reintenta ocho veces con espera exponencial de 30 segundos hasta una hora. Guarda resultado genérico, sin registrar direcciones, cuerpo, token ni credenciales. Después de ocho fallos el registro queda pendiente de intervención operativa. Es entrega al menos una vez: un cierre entre el envío y el guardado puede duplicar un correo. No se promete entrega exactamente una vez ni durabilidad del servidor SMTP externo.

Recuperación y confirmación guardan solicitudes durables, no tokens en claro. El token se genera al procesar el trabajo. Las plantillas incluyen HTML con marca, texto plano y contenido escapado. No incluyen direcciones físicas, contraseñas ni notas internas. Stock bajo se notifica una vez hasta reponer por encima del mínimo. El destino interno es ContactEmail en Configuración; vacío significa que no hay destino interno. No hay promociones ni suscripciones de marketing implementadas.

## Configuración privada

Configurar mediante user-secrets en desarrollo o variables de entorno en el hosting:

| Clave | Uso |
|---|---|
| InitialDeveloper:Email | Correo técnico elegido por el responsable |
| InitialDeveloper:Password | Clave inicial fuerte y privada; retirar después del alta |
| Smtp:Host | smtp.gmail.com si se utiliza Gmail |
| Smtp:Port | 587 |
| Smtp:UseSsl | true; SmtpClient negocia STARTTLS |
| Smtp:User | Cuenta emisora; Smtp:Username se conserva como alias |
| Smtp:Password | Contraseña de aplicación del proveedor, nunca la contraseña normal en Git |
| Smtp:From | Remitente autorizado por el proveedor |
| PublicBaseUrl | Origen HTTPS real para enlaces y logo |
| EmailOutbox:AutoProcess | true por defecto; false permite mantenimiento de la cola |

En variables de entorno sustituir `:` por `__`. Gmail requiere verificación en dos pasos y disponibilidad de contraseñas de aplicaciones según la cuenta. No se solicitaron ni almacenaron credenciales reales durante esta fase. La cuenta oficial del usuario aún no se creó en la base real: está prohibido aplicar estas migraciones sobre ella sin una etapa explícita de respaldo y revisión.

## Migraciones y operación

DurableEmailAndInitialPassword añade EmailOutbox, Products.LowStockNotified y AspNetUsers.MustChangePassword. InitialPasswordReuseGuard añade InitialPasswordHash. Up solo agrega objetos/columnas; no elimina datos. Se probaron únicamente en BisuteriaAudit_GUID. Deben aplicarse antes de ejecutar esta versión contra otra base. No reiniciar la web sobre la base real con Database:Initialize=true hasta revisar y autorizar esa aplicación.

Respaldar la cola con SQL y limitar su acceso porque contiene direcciones de correo y resúmenes de pedidos. Planificar retención/purga de mensajes ya enviados y una herramienta administrativa para reintentar fallos agotados; no se implementó esa pantalla. Monitorizar LastError y Attempts. No activar confirmación obligatoria sin comprobar entrega externa. Un hosting gratuito que prohíba SMTP no puede cumplir el envío real.

## Pruebas

OutboxTests verifica notificaciones por pedido/estado/pago, stock una vez hasta reposición, contacto, inicialización Admin, bloqueo por cambio obligatorio, prohibición de reutilizar contraseña inicial y reintento después de un fallo SMTP simulado. MailCapture sustituye siempre al emisor en pruebas. Las pruebas existentes verifican recuperación y confirmación. No se enviaron mensajes reales ni se certificó Gmail.

Comandos: generación local de ambas migraciones con `dotnet ef migrations add ... --configuration Release`; `dotnet build Bisuteria.slnx -c Release`; `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=ampliacion-fase5.trx' --results-directory audit/test-results`.

## Defensa

La cola transaccional separa guardar la compra de entregar el correo. Si SMTP falla, el pedido permanece y el worker reintenta. La contraseña inicial solo sirve para acceder al cambio obligatorio; se conserva su hash, nunca su texto, para impedir volver a utilizarla.

Resultado final: 147 pruebas aprobadas, 0 fallidas, 0 omitidas; 39 segundos. Build Release: 0 errores y 0 advertencias. Referencia SMTP: https://support.google.com/mail/answer/7104828 .

Archivos modificados en esta fase:
src/Bisuteria.Domain/Entities/Product.cs
src/Bisuteria.Infrastructure/Data/AppDbContext.cs
src/Bisuteria.Infrastructure/Identity/ApplicationUser.cs
src/Bisuteria.Infrastructure/Migrations/AppDbContextModelSnapshot.cs
src/Bisuteria.Infrastructure/Seed/DbInitializer.cs
src/Bisuteria.Web/Controllers/AccountController.cs
src/Bisuteria.Web/Program.cs
src/Bisuteria.Web/Services/EmailSender.cs
src/Bisuteria.Web/Services/PasswordRecoveryQueue.cs
tests/Bisuteria.Tests/AuditFactory.cs
docs/ampliacion-fase5-correo.md
src/Bisuteria.Domain/Entities/EmailOutbox.cs
src/Bisuteria.Infrastructure/Data/OutboxEvents.cs
src/Bisuteria.Infrastructure/Migrations/20261013023821_DurableEmailAndInitialPassword.Designer.cs
src/Bisuteria.Infrastructure/Migrations/20261013023821_DurableEmailAndInitialPassword.cs
src/Bisuteria.Infrastructure/Migrations/20261013024135_InitialPasswordReuseGuard.Designer.cs
src/Bisuteria.Infrastructure/Migrations/20261013024135_InitialPasswordReuseGuard.cs
src/Bisuteria.Web/Security/InitialPasswordFilter.cs
tests/Bisuteria.Tests/OutboxTests.cs
