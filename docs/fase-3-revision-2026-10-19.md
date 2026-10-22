# Fase 3 — altas y autenticación de cuentas

Esta revisión histórica aplicó entonces la solicitud de contraseñas nuevas de al menos 12 caracteres y TOTP obligatorio para Developer y Owner. El cambio posterior del 21 de octubre de 2026 revocó ese requisito: ahora Owner y Developer no usan TOTP y las cuentas existentes limpian esa configuración en el próximo inicio de sesión. Las contraseñas existentes no se imprimen, cambian ni consultan.

## Comportamiento

- El primer Developer continúa dependiendo de secretos externos y del cambio de contraseña inicial. La fase 1 prueba el rechazo del arranque productivo sin esos secretos cuando no hay Developer.
- Estado actual: Developer/Owner inician con contraseña sin desafío TOTP; las cuentas antiguas limpian TOTP y el indicador de enrolamiento en el siguiente inicio de sesión. TOTP continúa opcional para clientes.
- El alta por invitación mantiene el hash del token, caducidad de 24 horas y consumo único dentro de transacción. La persona invitada escoge su contraseña. Se añadió el botón Reenviar: genera otro enlace e invalida el anterior.
- Owner/Developer no pueden activar TOTP desde el perfil. Los clientes mantienen la opción voluntaria de activarlo o desactivarlo con contraseña.
- Se conserva el recuerdo de un dispositivo durante 30 días con cookie Secure/HttpOnly, sin caducidad deslizante. Las sesiones privilegiadas duran 30 minutos y las acciones sensibles requieren autenticación reciente de 15 minutos.
- La contraseña y los fallos TOTP comparten la progresión de bloqueos. El nivel se reinicia únicamente al completar el acceso.
- Los tests ahora completan TOTP con claves efímeras; no se usa una bandera de producción para omitirlo.

No se aplicó una migración nueva: los campos `MustChangePassword`, protección de la contraseña inicial, `RequireTwoFactorSetup`, invitaciones y outbox ya tienen migraciones aditivas en el repositorio. No se convirtieron usuarios Admin en Developer ni se enviaron invitaciones reales.

## Comprobaciones

La comprobación histórica de esta fase cubrió enrolamiento TOTP obligatorio. El cambio del 21 de octubre reemplaza esos casos por acceso de Developer/Owner sin segundo factor y limpieza de MFA heredado, preservando el TOTP opcional de clientes.

Las primeras ejecuciones encontraron pruebas que esperaban la política anterior y un error de presentación del rechazo de desactivación. Se corrigieron y se volvió a ejecutar la validación; no se presentan esas ejecuciones fallidas como aprobadas.

Resultados finales: `dotnet build Bisuteria.slnx -c Release` sin advertencias ni errores; `dotnet test Bisuteria.slnx -c Release --no-build` con **185 aprobadas, 0 fallidas**. Todas las pruebas usan `BisuteriaAudit_<GUID>` y el sustituto de correo. El TRX queda en `audit/test-results/fase3-validada-20261019.trx` (ruta ignorada por Git).
