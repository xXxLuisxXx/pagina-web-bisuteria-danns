# Fase 6 — seguridad web

## Cambios y revisión

- El login hace una verificación de contraseña de Identity aun cuando el correo no exista; ambos casos responden con el mismo error general. Las pruebas comparan el mensaje visible, no afirman una igualdad temporal que no se midió.
- La confirmación de pedidos de invitados, que contiene un token de acceso en el enlace, responde con `Cache-Control: no-store` y `Referrer-Policy: no-referrer`.
- El almacenamiento de comprobantes rechaza `wwwroot` y cualquier ruta hija, incluso cuando la configuración apunta exactamente a la raíz pública.
- El registro de errores al procesar imágenes ya no imprime el nombre de archivo ni la excepción del decodificador, que podían contener datos aportados por la persona que subió el archivo.
- Se excluye `artifacts/**` del análisis de elementos predeterminados del proyecto web. Así el SDK no vuelve a tratar salidas anidadas de pruebas como contenido de compilación, evitando el error MSB3030 reportado.

La inspección del código confirma una CSP con nonces y sin `unsafe-inline`, HSTS fuera de Development, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, bloqueo de marcos, validación de `AllowedHosts` y `PublicBaseUrl` en producción, proxies reenviados limitados a IP configuradas, claves persistentes de Data Protection y sesión SQL obligatoria en producción. El escaneo de vistas encontró un solo `Html.Raw`: datos JSON-LD serializados por `System.Text.Json`, que escapa caracteres HTML. Las URL de imágenes de catálogo aceptan rutas locales `/img/`, no destinos externos. Las exportaciones XLSX y CSV pasan texto de usuario por neutralización de fórmulas.

El middleware de rate limit separa los límites por grupo de ruta y usa `RemoteIpAddress` después del middleware de proxies confiables; la carga de comprobantes tiene un límite adicional de cuerpo y tipos. Registro y contacto tienen honeypot y límite. La cuota de personalizaciones anónimas es 100 por identificador de sesión cifrado con SHA-256.

## Dependencias e historial

- `dotnet list Bisuteria.slnx package --vulnerable --include-transitive`: cero paquetes vulnerables en las fuentes NuGet configuradas.
- Se ejecutó un escaneo local, con patrones de alta confianza y salida redactada, sobre las referencias Git disponibles. No encontró claves privadas, tokens conocidos de GitHub/AWS/Google ni credenciales de SMTP o base de datos actuales. Los únicos resultados de asignaciones de contraseña fueron fixtures históricos de pruebas y el password aleatorio creado al ejecutar el contenedor SQL de CI; las pruebas actuales generan las claves en memoria. No se inspeccionaron forks, clones externos ni objetos Git inaccesibles. No se reescribió el historial público.

La configuración de producción no se ejecutó contra un hosting: faltan dominio, proxies, ruta persistente de Data Protection, sesión SQL y almacenamiento privado reales. La limitación por IP está en memoria por proceso, de modo que un despliegue con varias instancias necesitaría un limitador distribuido para compartir cuotas.

## Verificación

- `dotnet build Bisuteria.slnx -c Release`: correcto, 0 advertencias y 0 errores.
- `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=fase6-cierre-20261019.trx' --results-directory audit/test-results`: 202/202 correctas.
- Regresiones nuevas: mismo error público de login, no-cache/no-referrer para el enlace de invitado, y rechazo de rutas de comprobantes públicas.
- `git diff --check`: correcto.
