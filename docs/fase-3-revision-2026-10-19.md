# Fase 3 — altas y autenticación de cuentas

Esta revisión aplica la solicitud más reciente: contraseñas nuevas de al menos 12 caracteres y TOTP obligatorio para Developer y Owner. Sustituye los ajustes anteriores que permitían ocho caracteres y desactivaban TOTP automáticamente al iniciar sesión. Las contraseñas existentes no se imprimen, cambian ni consultan.

## Comportamiento

- El primer Developer continúa dependiendo de secretos externos y del cambio de contraseña inicial. La fase 1 prueba el rechazo del arranque productivo sin esos secretos cuando no hay Developer.
- El login conserva TOTP; conocer únicamente la contraseña no desactiva la protección. El filtro de acceso exige enrolamiento a Developer/Owner incluso si una cuenta antigua perdió la bandera de configuración.
- El alta por invitación mantiene el hash del token, caducidad de 24 horas y consumo único dentro de transacción. La persona invitada escoge su contraseña. Se añadió el botón Reenviar: genera otro enlace e invalida el anterior.
- Developer/Owner no pueden desactivar TOTP con la contraseña desde su perfil. Los clientes mantienen la opción voluntaria de activarlo o desactivarlo con contraseña. El enrolamiento obligatorio invalida las sesiones y exige autenticarse de nuevo.
- Se conserva el recuerdo de un dispositivo durante 30 días con cookie Secure/HttpOnly, sin caducidad deslizante. Las sesiones privilegiadas duran 30 minutos y las acciones sensibles requieren autenticación reciente de 15 minutos.
- La contraseña y los fallos TOTP comparten la progresión de bloqueos. El nivel se reinicia únicamente al completar el acceso.
- Los tests ahora completan TOTP con claves efímeras; no se usa una bandera de producción para omitirlo.

No se aplicó una migración nueva: los campos `MustChangePassword`, protección de la contraseña inicial, `RequireTwoFactorSetup`, invitaciones y outbox ya tienen migraciones aditivas en el repositorio. No se convirtieron usuarios Admin en Developer ni se enviaron invitaciones reales.

## Comprobaciones

Se comprobaron el flujo inicial de cambio de contraseña/enrolamiento, rechazo de reutilización de la clave inicial, rechazo del acceso con solo contraseña, imposibilidad de desactivar TOTP obligatorio, enrolamiento exigido a cuentas antiguas, recuerdo del dispositivo, caducidad y reenvío de invitaciones, y reautenticación de acciones sensibles.

Las primeras ejecuciones encontraron pruebas que esperaban la política anterior y un error de presentación del rechazo de desactivación. Se corrigieron y se volvió a ejecutar la validación; no se presentan esas ejecuciones fallidas como aprobadas.

Resultados finales: `dotnet build Bisuteria.slnx -c Release` sin advertencias ni errores; `dotnet test Bisuteria.slnx -c Release --no-build` con **185 aprobadas, 0 fallidas**. Todas las pruebas usan `BisuteriaAudit_<GUID>` y el sustituto de correo. El TRX queda en `audit/test-results/fase3-validada-20261019.trx` (ruta ignorada por Git).
