# Preparación del despliegue

El código fue validado en Windows y en GitHub Actions sobre Linux. Todavía no existe un despliegue verificado. Para publicar una demo gratuita compatible con .NET 10 y SQL Server, MonsterASP.NET es la ruta inicial más directa: ofrece subdominio, HTTPS bajo solicitud, 1 sitio, 1 base de datos de hasta 1 GB y 256 MB RAM. Su documentación destina el plan Free a aprendizaje/pruebas; no incluye copias diarias, capacidad AlwaysRunning ni garantías, y sitios inactivos se archivan a los seis meses. La cola durable de correo de este proyecto requiere que la aplicación esté corriendo; en el plan Free su procesamiento no queda garantizado cuando el sitio duerme. Sirve para enseñar/probar el proyecto, no para operar una tienda con pedidos y correos que deban atenderse continuamente. Fuente: [comparativa oficial Free/Premium](https://help.monsterasp.net/getting-started/first-steps/free-vs-premium), [primeros pasos](https://help.monsterasp.net/getting-started/first-steps/create-first-website).

## Configuración necesaria

Configura estos valores en el almacén de secretos o entorno del hosting, nunca en Git:

| Variable | Valor requerido |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | SQL Server productivo, usuario con permisos mínimos y certificado válido |
| `Database__ApplyMigrations` | `false`; producción nunca migra al arrancar |
| `Database__Initialize` | `true` después de aplicar el script de migración; crea roles, configuración base y Developer inicial |
| `Session__Provider` | `SqlServer`; guarda el carrito en la tabla `DistributedCache` creada por la migración |
| `AllowedHosts` | Dominio real, sin comodín |
| `PublicBaseUrl` | Origen HTTPS público |
| `DataProtection__KeysPath` | Directorio persistente con acceso restringido al proceso |
| `ReverseProxy__KnownProxies__0` | IP del proxy, solo si existe y es confiable |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UseSsl` | Configuración del proveedor de correo |
| `Smtp__From`, `Smtp__Username`, `Smtp__Password` | Remitente y credenciales privadas |
| `Payments__EnabledMethods__0` | Método confirmado por la tienda: `CashOnDelivery` o `BankTransfer` |

Activar `Identity__RequireConfirmedEmail=true` únicamente después de verificar la entrega de correo. No habilitar `Card`: no hay pasarela integrada.

## Secuencia de publicación

1. Elegir hosting compatible con .NET 10 y SQL Server. Configurar `Session__Provider=SqlServer` para compartir el carrito y conservarlo tras reinicios; la caché usa la base de la aplicación. [Microsoft recomienda un almacén dedicado cuando el tráfico crezca](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0), porque compartir la base puede afectar el rendimiento.
2. Preparar volúmenes persistentes para claves y `wwwroot/img/uploads`. Respaldar SQL y archivos de imágenes por separado y comprobar restauración en un entorno aislado.
3. Desde la raíz, compilar en Release y publicar el proyecto web framework-dependent para el .NET 10 del proveedor: `dotnet build Bisuteria.slnx -c Release` y después `dotnet publish src/Bisuteria.Web/Bisuteria.Web.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false`. El artefacto queda en `src/Bisuteria.Web/bin/Release/net10.0/publish/`. Paquete local actualizado: `audit/Bisuteria-Web-Release.zip` (138 MB comprimido). El paquete no incluye secretos ni configuración de producción. Antes de usarlo hay que crear el sitio y su base, añadir la configuración de producción al hosting y migrar una base vacía revisada.
4. Revisar y generar el script idempotente (`dotnet ef migrations script --idempotent --configuration Release --no-build --project src/Bisuteria.Infrastructure --startup-project src/Bisuteria.Web -o audit/database-migrations-idempotent.sql`); respaldar y aplicar ese artefacto a la base real mediante el paso controlado del proveedor. El script generado se revisó: no contiene `DROP COLUMN`, `DROP TABLE` ni `TRUNCATE TABLE`. No uses `Database__ApplyMigrations=true` en el sitio.
5. Configurar secretos, HTTPS y proxy del proveedor. Tras aplicar las migraciones, iniciar con `Database__Initialize=true` y `InitialDeveloper__Email`/`InitialDeveloper__Password` para crear roles, datos base y Developer; retirar la clave inicial del entorno después del alta.
6. Publicar primero en un entorno de prueba equivalente al productivo. Verificar `/health`, catálogo, registro, acceso administrativo, recuperación y confirmación de correo, carga de imágenes y un pedido de prueba con cancelación y restitución de stock.
7. Reiniciar la instancia y comprobar conservación de imágenes, claves y carrito SQL. La cola de correo también es durable en SQL y debe respaldarse junto a la base.
8. Publicar al dominio definitivo y repetir las comprobaciones externas; registrar fecha, versión y evidencia sin credenciales ni datos personales.

## Recuperación ante un fallo

Conservar el artefacto anterior y un respaldo verificado antes del despliegue. Una reversión de código requiere comprobar su compatibilidad con el esquema migrado. No borrar tablas ni ejecutar migraciones descendentes automáticamente para revertir. Restaurar datos solo mediante un procedimiento explícito que considere los pedidos recibidos desde el respaldo.

## Pendientes externos

- Crear el sitio Free y la base SQL de prueba en el panel del proveedor; elegir un subdominio `runasp.net` o `tryasp.net`. Según la documentación actualizada del proveedor, Free ofrece 256 MB de RAM, base de 1 GB, centro de datos europeo, no incluye respaldos automáticos y está diseñado para hobby/pruebas; HTTPS se activa por solicitud. No es apropiado para procesar ventas reales con garantías. El servicio gratuito solo permite su subdominio; un dominio propio exige plan Premium. Para pedidos reales, usar hosting con recursos y operación garantizados, presupuesto y ubicación apropiados. Fuente: [planes Free y Premium](https://help.monsterasp.net/getting-started/first-steps/free-vs-premium).
- SQL Server y almacenamiento persistente disponibles.
- Para varias instancias, compartir claves de Data Protection y distribuir también el rate limiter; sus contadores actuales son locales a cada proceso.
- Cuenta SMTP y prueba real de entrega.
- Métodos de cobro y política de reembolso confirmados.
- Objetivos de recuperación de respaldos y responsables de operación.

En MonsterASP, guardar `ASPNETCORE_ENVIRONMENT=Production`, `Database__ApplyMigrations=false`, `Database__Initialize=true` tras el paso controlado de migración, `InitialDeveloper__Email`, `InitialDeveloper__Password`, `ConnectionStrings__DefaultConnection` (cadena **Local access**, nunca compartirla), `AllowedHosts`, `PublicBaseUrl`, `DataProtection__KeysPath` y `Smtp__*` como variables de entorno del sitio y reiniciarlo. La ayuda oficial explica [variables de entorno](https://help.monsterasp.net/websites/configuration/environment-variables), [cadena SQL local/remota](https://help.monsterasp.net/databases/mssql/connection-string) y [Web Deploy desde Visual Studio](https://help.monsterasp.net/websites/deploy/webdeploy). No pegar contraseñas en Git ni en el chat. Para correo en plan Free se necesita SMTP externo.

Esta lista prepara la publicación; no acredita que los servicios ya estén configurados.
