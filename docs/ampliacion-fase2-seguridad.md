# Fase 2 ampliada: segundo factor y comprobaciones de seguridad

Fecha: 12 de octubre de 2026. Rama: mejoras/auditoria.

## Cambios

Se corrigió un bypass: el login anterior verificaba la contraseña y emitía la cookie directamente mediante SignInAsync, incluso para usuarios con TwoFactorEnabled. Ahora PasswordSignInAsync inicia el desafío de Identity y solamente se concede la sesión después del TOTP o de un código de recuperación válido. Se mantiene el mensaje genérico, la comprobación de cuenta activa y la redirección exclusivamente local.

El perfil ofrece activación opcional de TOTP, recomendada expresamente para administradores. Se exige la contraseña actual antes de mostrar la clave y un código válido para activar. La clave se introduce manualmente en una aplicación autenticadora: no hay QR externo ni nuevas dependencias. Se generan diez códigos de recuperación de uso único, mostrados una sola vez. Se invalida el sello de seguridad de sesiones anteriores y se renueva la actual. Las respuestas sensibles llevan no-store. GET no genera claves ni modifica la cuenta; POST exige antiforgery. Hay límites por IP y bloqueo de Identity, incluido el conteo explícito de intentos de recuperación fallidos.

Se reutilizan las tablas de Identity: no hay migración ni cambio en la base real. Las credenciales y claves nuevas de pruebas se generan en cada ejecución, sin valores persistentes.

Referencia de API utilizada: [SignInManager de Microsoft](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.signinmanager-1) y [validación TOTP](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.signinmanager-1.twofactorauthenticatorsigninasync?view=aspnetcore-10.0).

## Pruebas y fallos investigados

- PasswordAloneCannotAuthenticateAnAccountWithTwoFactor falló antes del arreglo: redirigía a `/` sin segundo factor. Después pasa, incluyendo rechazo de código inválido, recuperación y prohibición de reutilizar el código.
- AuthenticatorEnrollmentRequiresPasswordAndValidTotp verifica contraseña previa, respuesta no-store, activación y login TOTP completo. Detectó que ResetAuthenticatorKeyAsync invalidaba también la sesión actual: se corrigió renovándola después de preparar la clave.
- Una primera versión de la prueba de recuperación conservaba una entidad de Identity obsoleta tras peticiones HTTP. Se corrigió usando los códigos antes del acceso y una nueva unidad de trabajo para verificar su consumo.
- Build Release: 0 errores, 0 advertencias.
- Las dos pruebas específicas pasan en Release.
- La primera suite completa terminó con 135 aprobadas y una fallida: la tabla de usuarios intentaba obtener una inicial de un nombre vacío. Se añadió AdminUsersAcceptsAnAccountWithoutDisplayName, que reprodujo el 500 antes del arreglo. Ahora la vista utiliza un nombre alternativo y obtiene la inicial por elemento de texto Unicode cuando existe.
- `dotnet test Bisuteria.slnx` no pudo compilar Debug por DLL ocupadas por la vista previa Bisuteria.Web, PID 24080. No se detuvo ese proceso ni se tocó su base. Se usa `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=ampliacion-fase2.trx' --results-directory audit/test-results`.
- `dotnet list Bisuteria.slnx package --vulnerable --include-transitive`: los cinco proyectos sin paquetes vulnerables según los orígenes consultados.
- Resultado final de la suite Release tras corregir el fallo: **137 aprobadas, 0 fallidas, 0 omitidas**, duración de pruebas 40 segundos. Evidencia local: `audit/test-results/ampliacion-fase2.trx`. Compilación final Release: 0 errores, 0 advertencias.

## Revisión y límites

Los controles existentes de CSP, cabeceras, proxy configurable, claves persistentes, validación de sesiones, cuotas, imágenes y auditoría se conservan. El único Html.Raw encontrado en las vistas serializa JSON-LD mediante System.Text.Json con su escapado predeterminado; no representa HTML libre del usuario.

Se revisaron patrones de claves privadas, tokens GitHub, claves AWS y asignaciones Password en ocho commits y 429 blobs únicos alcanzables por las referencias locales. El escaneo devolvió cuatro candidatos en versiones de DbInitializer y EmailSender, donde se leen opciones de configuración. Este análisis por patrones no acredita ausencia absoluta de secretos; tampoco cubre referencias remotas no descargadas, logs externos ni secretos fuera de Git. Las pruebas históricas contienen una contraseña ficticia fija, que no corresponde a una cuenta real; las pruebas nuevas usan valores efímeros.

TOTP es recomendado, no obligatorio para Admin. No se incorpora todavía autoservicio para desactivar o regenerar el autenticador; se deben guardar los códigos de recuperación. No se certifica rechazo de reutilización del mismo TOTP dentro de su ventana temporal ni igualdad temporal de las respuestas de login. La integración se probó mediante HTTP y códigos calculados, no con un teléfono físico. La auditoría administrativa sigue registrando solicitudes, no diferencias transaccionales por campo. Los límites operativos de proxy, correo y despliegue requieren validación externa. No constituye certificación OWASP completa.

## Archivos

- src/Bisuteria.Web/Controllers/AccountController.cs
- src/Bisuteria.Web/Controllers/AccountTwoFactor.cs
- src/Bisuteria.Web/Models/TwoFactorViewModel.cs
- src/Bisuteria.Web/Views/Account/Authenticator.cshtml
- src/Bisuteria.Web/Views/Account/TwoFactor.cshtml
- src/Bisuteria.Web/Views/Account/Profile.cshtml
- src/Bisuteria.Web/Security/SecurityConfiguration.cs
- src/Bisuteria.Web/Areas/Admin/Views/Users/Index.cshtml
- tests/Bisuteria.Tests/TwoFactorTests.cs
- Este informe y docs/ampliacion-20261012.md.

## Defensa oral

La contraseña comprueba lo que sabes; TOTP comprueba que tienes la clave del autenticador. Identity mantiene una sesión temporal de desafío, sin acceso al perfil hasta completar el segundo factor. Los códigos de recuperación permiten entrar si se pierde el dispositivo y se consumen al usarse. La regresión demostró el bypass anterior y verifica que la contraseña sola ya no basta.
