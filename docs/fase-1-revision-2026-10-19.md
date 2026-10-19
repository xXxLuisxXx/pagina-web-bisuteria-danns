# Fase 1 — credenciales por defecto

Fecha: 19 de octubre de 2026. Rama: `mejoras/integral`.

## Punto de partida y alcance

La revisión parte del checkout local `3e43428`, más los cambios de trabajo previos del usuario. El archivo de solicitud cita `6fd5524`, pero retroceder a ese commit perdería mejoras posteriores. Se creó la rama solicitada conservando los cambios existentes; el commit de esta fase incorpora exclusivamente esta corrección y sus pruebas/documentación.

Esta fase revisa cuentas por defecto, configuración versionada y protección del arranque. No certifica las fases 2 a 12. No se ejecutó el sitio contra la base real, no se consultaron user-secrets y no se enviaron correos. Las pruebas de integración usan exclusivamente bases temporales `BisuteriaAudit_<GUID>` y un sustituto de correo.

## Hallazgos y cambios

El inicializador ya creaba el desarrollador únicamente con configuración externa y ya desactivaba una cuenta administrativa histórica. No se encontró una contraseña fija de aplicación en el inicializador ni una creación de usuario con correo literal en el seed o las migraciones. La referencia al identificador de la cuenta histórica se conserva solamente para localizarla y bloquearla; no constituye una cuenta creada por el arranque ni una credencial utilizable. Su fixture reproduce el caso en una base temporal.

Se corrigieron dos defectos del bloqueo histórico:

- Una cuenta ya inactiva con bloqueo incompleto era omitida. Ahora también se bloquea ese caso.
- La actualización no renovaba el sello de seguridad. Ahora se renueva junto con la desactivación para invalidar sesiones anteriores mediante la validación de cookies existente. Un segundo arranque conserva el sello: la operación es idempotente.

No se borran usuarios ni pedidos y no se cambia el esquema. Se conserva el rol histórico para mantener sus relaciones. El resultado de Identity continúa siendo obligatorio: si el bloqueo falla, el inicializador lanza una excepción.

La contraseña compartida de las pruebas se genera en memoria en cada ejecución. Los fixtures SMTP usan el dominio reservado `example.test` y valores efímeros, sin buzones de proveedores reales ni claves fijas. Los ejemplos negativos de validación restantes son datos de prueba, no credenciales de cuentas de la aplicación.

La regresión de configuración ahora consulta `git ls-files`: revisa los JSON de configuración realmente versionados en todo el repositorio, sin leer archivos privados ignorados. Rechaza valores no vacíos en claves de contraseña, secreto, token y clave de API, además de contraseñas dentro de cadenas de conexión. Solo informa rutas y nombres de claves.

## Archivos de esta fase

| Archivo | Cambio |
| --- | --- |
| `src/Bisuteria.Infrastructure/Seed/DbInitializer.cs` | Bloqueo completo y sello de seguridad; comentario sobre alta mediante secretos externos. |
| `tests/Bisuteria.Tests/DefaultCredentialsTests.cs` | Cuenta activa/inactiva, sello, idempotencia, arranque vacío sin secretos y análisis de configuración versionada. |
| `tests/Bisuteria.Tests/AuditFactory.cs` | Contraseña efímera de las bases temporales. |
| `tests/Bisuteria.Tests/SmtpEmailSenderTests.cs` | Remitentes ficticios y claves generadas durante las pruebas. |
| `scripts/Check-ConfigurationHistory.ps1` | Escaneo reproducible de JSON históricos, sin imprimir sus valores. Requiere PowerShell 7. |
| Este informe | Evidencia, alcance y procedimiento de respuesta a una exposición. |

Los cambios previos en estos u otros archivos que pertenecen a otras fases se conservaron en el árbol de trabajo; no se incorporan deliberadamente al commit de la fase 1.

## Pruebas y resultados reales

La regresión `StartupDisablesLegacyDefaultAdministrator` se ejecutó primero con la lógica anterior: sus dos casos fallaron, uno por no activar el bloqueo de una cuenta ya inactiva y otro por conservar el sello anterior.

Las pruebas nuevas de arranque utilizan un contenedor de servicios mínimo y una base SQL Server independiente por caso. En desarrollo, dos arranques sin secretos no crean usuarios; en producción se exige que el arranque falle y que siga sin existir ninguna cuenta. El nombre de la base se valida antes de migrar y antes de eliminar únicamente esa base temporal.

| Comando / comprobación | Resultado |
| --- | --- |
| `dotnet test tests/Bisuteria.Tests/Bisuteria.Tests.csproj -c Release --filter FullyQualifiedName~StartupDisablesLegacyDefaultAdministrator` con lógica anterior | 2 fallidas; detección del defecto confirmada. |
| `dotnet build Bisuteria.slnx -c Release` con corrección | Código 0; 0 errores y 0 advertencias. Última ejecución: 8,24 segundos. |
| `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=fase1-20261019.trx' --results-directory audit/test-results` con corrección | Código 0; 175 aprobadas, 0 fallidas y 0 omitidas; duración 1 minuto 28 segundos. TRX local en `audit/test-results/fase1-20261019.trx`. |
| `git diff --check` y `git diff --cached --check` | Sin errores de espacios en blanco. |
| `pwsh -File scripts/Check-ConfigurationHistory.ps1` | 20 commits alcanzables, 40 versiones de JSON, 0 rutas sospechosas según los patrones descritos; código de salida 0. |

Se usa Release también para las pruebas para ejecutar los binarios recién compilados y evitar el bloqueo del ejecutable Debug que el usuario mantiene abierto. No se detuvo ese proceso ni se reinicializó su base real.

El escaneo histórico se hizo sobre las referencias Git locales disponibles antes del commit de esta fase. Se buscaron las opciones de alta antiguas y el identificador histórico en seed, migraciones, código y documentación. Las referencias restantes del identificador son la cuarentena y su regresión. No se inspeccionaron clones ajenos, forks, referencias internas de pull requests ni objetos inaccesibles. Una revisión por patrones de configuración no equivale a un escaneo exhaustivo de secretos en todo el historial; esa comprobación ampliada sigue pendiente en la fase 6.

## Si una credencial real se publicó en Git

Cualquier contraseña o token real que haya llegado al repositorio público debe considerarse comprometido. Primero revocarlo o rotarlo en su proveedor y actualizar user-secrets o las variables del despliegue. Borrarlo del último commit no lo invalida. Este orden sigue la [guía oficial de GitHub para retirar datos sensibles](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository).

Procedimiento para una limpieza posterior, explicado pero **no ejecutado**:

1. Coordinar una pausa de cambios y guardar un respaldo privado con acceso restringido.
2. Preparar un clon de trabajo exclusivo y usar `git-filter-repo` para reemplazar el valor comprometido en todas sus rutas históricas o retirar los archivos afectados. El archivo de reemplazos que contiene el secreto debe permanecer fuera del repositorio y no compartirse.
3. Revisar ramas, etiquetas y diferencias; repetir el escaneo y las pruebas. Los hashes cambiarán y pueden afectarse pull requests y firmas.
4. Revisar el resultado con el propietario antes de publicar la reescritura. La actualización forzada del remoto requiere coordinación y autorización específica; no forma parte de esta fase.
5. Renovar los clones de colaboradores y coordinar los forks para evitar que una copia antigua vuelva a introducir el historial. Si persisten referencias o vistas sensibles en GitHub, evaluar la solicitud correspondiente a su soporte.

La reescritura no puede retirar copias que terceros ya hayan obtenido. Por eso la rotación inicial es indispensable. No se ejecutaron `reset`, limpieza del historial ni actualizaciones forzadas del remoto.

## Estado y siguiente paso

Fase 1 completada para el alcance descrito y el árbol de trabajo local validado. Los resultados incluyen los cambios previos conservados, no una ejecución aislada del commit sobre una copia limpia. No hay decisiones de negocio necesarias para esta corrección. La siguiente fase será revisar la matriz de permisos existente; por instrucción del usuario se espera su «continúa» antes de avanzar. Las fases 2 a 12 y la inspección completa de secretos históricos siguen pendientes de revisión en esta ronda.
