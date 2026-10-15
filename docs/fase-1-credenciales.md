# Fase 1: retirar la cuenta administrativa de ejemplo

## Hallazgos y cambio

En la rama main disponible al iniciar la fase, DbInitializer no contenía usuario ni contraseña administrativos fijos en el código o appsettings. Solo creaba el administrador si se proporcionaban ambas opciones InitialAdmin desde configuración externa. No se encontró la cuenta administrativa histórica de ejemplo en los appsettings versionados ni en el historial Git disponible.

Se añadió al inicializador una protección de arranque: si una base previa conserva la cuenta histórica de ejemplo con acceso activo, el arranque la desactiva y bloquea de forma permanente. No se borra el registro ni se cambia ningún otro usuario. Si no puede actualizarse correctamente, el inicializador falla en lugar de dejar esa cuenta activa.

Se añadió una prueba de regresión que prepara esa cuenta en la base de auditoría aislada, ejecuta el inicializador y comprueba que queda inactiva, bloqueada y sin perder su rol histórico. Otra prueba revisa los appsettings versionados para rechazar valores literales de contraseña, secreto, token o contraseñas dentro de cadenas de conexión.

## Historial y respuesta a exposición

La búsqueda exacta de la cuenta de ejemplo no encontró coincidencias en los 15 commits alcanzables de las ramas públicas revisadas (main y mejoras/auditoria). El escaneo del historial de archivos appsettings encontró cero valores sensibles literales según los patrones de contraseña, secreto, clave de API y token. Es una revisión automatizada por patrones, no una garantía contra cualquier secreto codificado con otro formato o nombre.

Si se confirma que una contraseña o token real llegó a Git, debe considerarse comprometido y rotarse primero donde se use. Para borrar el rastro del repositorio sin ejecutar esos cambios aquí: crear un respaldo privado; usar git-filter-repo para reemplazar el valor expuesto en todas las ramas y etiquetas; revisar el resultado con escáneres; coordinar la reescritura con colaboradores; y actualizar GitHub con git push --force-with-lease únicamente después de la aprobación explícita del propietario. Borrar un archivo en un commit posterior no invalida el secreto filtrado.

## Verificación

- Build Release: aprobado, 0 errores; quedaron avisos NU1902/NU1903 por System.Security.Cryptography.Pkcs 9.0.11 en dependencias transitivas. Corresponden al escaneo de dependencias de una fase posterior y se deben resolver allí.
- `dotnet test Bisuteria.slnx -c Release`: 136 aprobadas, 0 fallidas, 0 omitidas (ejecución completa del 14 de octubre de 2026).
- Base real: no se usó. La regresión utiliza la base temporal BisuteriaAudit_<GUID> compartida por AuditFactory.

## Decisiones y límites

Esta protección solo desactiva la cuenta histórica identificada y no determina qué personas reales deben conservar el rol Admin. No se inspeccionó una base productiva. La limpieza del historial no se ejecutó, pues no había evidencia del patrón buscado y reescribir Git sería una acción disruptiva.
