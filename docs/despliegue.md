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
| `AllowedHosts` | Dominio real, sin comodín |
| `PublicBaseUrl` | Origen HTTPS público |
| `DataProtection__KeysPath` | Directorio persistente con acceso restringido al proceso |
| `ReverseProxy__KnownProxies__0` | IP del proxy, solo si existe y es confiable |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UseSsl` | Configuración del proveedor de correo |
| `Smtp__From`, `Smtp__Username`, `Smtp__Password` | Remitente y credenciales privadas |
| `Payments__EnabledMethods__0` | Método confirmado por la tienda: `CashOnDelivery` o `BankTransfer` |

Activar `Identity__RequireConfirmedEmail=true` únicamente después de verificar la entrega de correo. No habilitar `Card`: no hay pasarela integrada.

## Secuencia de publicación

1. Elegir hosting compatible con .NET 10 y SQL Server. Mantener una sola instancia mientras el carrito sea local al proceso.
2. Preparar volúmenes persistentes para claves y `wwwroot/img/uploads`. Respaldar SQL y archivos de imágenes por separado y comprobar restauración en un entorno aislado.
3. Para IIS en Windows generar el artefacto desde la raíz: `dotnet publish src/Bisuteria.Web/Bisuteria.Web.csproj -c Release -r win-x64 --self-contained false -o audit/publish-win-x64`. El directorio está ignorado por Git. Paquete preparado en `audit/Bisuteria-Web-Release.zip` (aprox. 29 MB comprimido, solo recursos win-x64); antes de usarlo hay que crear el sitio y su base, añadir configuración de producción al hosting y migrar una base vacía revisada. El paquete aún no contiene secretos ni configuración del hosting.
4. Revisar y generar el script idempotente (`dotnet ef migrations script --idempotent --project src/Bisuteria.Infrastructure --startup-project src/Bisuteria.Web -o migrations.sql`); respaldar y aplicar ese artefacto a la base real mediante el paso controlado del proveedor. No uses `Database__ApplyMigrations=true` en el sitio.
5. Configurar secretos, HTTPS y proxy del proveedor. Tras aplicar las migraciones, iniciar con `Database__Initialize=true` y `InitialDeveloper__Email`/`InitialDeveloper__Password` para crear roles, datos base y Developer; retirar la clave inicial del entorno después del alta.
6. Publicar primero en un entorno de prueba equivalente al productivo. Verificar `/health`, catálogo, registro, acceso administrativo, recuperación y confirmación de correo, carga de imágenes y un pedido de prueba con cancelación y restitución de stock.
7. Reiniciar la instancia y comprobar conservación de imágenes y claves. El carrito puede perderse al reiniciar y requiere almacenamiento compartido si el negocio necesita esa garantía. La cola de correo ya es durable en SQL y debe respaldarse junto a la base.
8. Publicar al dominio definitivo y repetir las comprobaciones externas; registrar fecha, versión y evidencia sin credenciales ni datos personales.

## Recuperación ante un fallo

Conservar el artefacto anterior y un respaldo verificado antes del despliegue. Una reversión de código requiere comprobar su compatibilidad con el esquema migrado. No borrar tablas ni ejecutar migraciones descendentes automáticamente para revertir. Restaurar datos solo mediante un procedimiento explícito que considere los pedidos recibidos desde el respaldo.

## Pendientes externos

- Crear el sitio Free y la base SQL de prueba en el panel del proveedor; elegir un subdominio `runasp.net` o `tryasp.net`. Para una demo es gratuito y no requiere dominio propio. El servicio gratuito solo permite dominio de ellos y está en Europa; un dominio propio exige plan Premium. Para pedidos reales, usar hosting con recursos y operación garantizados, presupuesto y ubicación apropiados.
- SQL Server y almacenamiento persistente disponibles.
- Cuenta SMTP y prueba real de entrega.
- Métodos de cobro y política de reembolso confirmados.
- Objetivos de recuperación de respaldos y responsables de operación.

En MonsterASP, guardar `ASPNETCORE_ENVIRONMENT=Production`, `Database__ApplyMigrations=false`, `Database__Initialize=true` tras el paso controlado de migración, `InitialDeveloper__Email`, `InitialDeveloper__Password`, `ConnectionStrings__DefaultConnection` (cadena **Local access**, nunca compartirla), `AllowedHosts`, `PublicBaseUrl`, `DataProtection__KeysPath` y `Smtp__*` como variables de entorno del sitio y reiniciarlo. La ayuda oficial explica [variables de entorno](https://help.monsterasp.net/websites/configuration/environment-variables), [cadena SQL local/remota](https://help.monsterasp.net/databases/mssql/connection-string) y [Web Deploy desde Visual Studio](https://help.monsterasp.net/websites/deploy/webdeploy). No pegar contraseñas en Git ni en el chat. Para correo en plan Free se necesita SMTP externo.

Esta lista prepara la publicación; no acredita que los servicios ya estén configurados.
