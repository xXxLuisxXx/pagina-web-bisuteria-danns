# Preparación del despliegue

El código fue validado en Windows y en GitHub Actions sobre Linux. Todavía no existe un despliegue productivo verificado. El proveedor, dominio y servicios externos deben definirse antes de elegir instrucciones específicas de instalación.

## Configuración necesaria

Configura estos valores en el almacén de secretos o entorno del hosting, nunca en Git:

| Variable | Valor requerido |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | SQL Server productivo, usuario con permisos mínimos y certificado válido |
| `Database__Initialize` | `false`; aplicar migraciones revisadas por separado |
| `AllowedHosts` | Dominio real, sin comodín |
| `PublicBaseUrl` | Origen HTTPS público |
| `DataProtection__KeysPath` | Directorio persistente con acceso restringido al proceso |
| `ReverseProxy__KnownProxies__0` | IP del proxy, solo si existe y es confiable |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UseSsl` | Configuración del proveedor de correo |
| `Smtp__From`, `Smtp__Username`, `Smtp__Password` | Remitente y credenciales privadas |
| `Payments__EnabledMethods__0` | Método confirmado por la tienda: `CashOnDelivery` o `BankTransfer` |

Activar `Identity__RequireConfirmedEmail=true` únicamente después de verificar la entrega de correo. No habilitar `Card`: no hay pasarela integrada.

## Secuencia de publicación

1. Elegir hosting compatible con .NET 10 y SQL Server. Mantener una sola instancia mientras carrito y cola de correo sean locales al proceso.
2. Preparar volúmenes persistentes para claves y `wwwroot/img/uploads`. Respaldar SQL y archivos de imágenes por separado y comprobar restauración en un entorno aislado.
3. Generar el artefacto desde la raíz: `dotnet publish src/Bisuteria.Web/Bisuteria.Web.csproj -c Release -o audit/publish`. El directorio de salida está ignorado por Git.
4. Revisar migraciones pendientes y respaldo antes de aplicarlas a la base real en una ventana controlada. No usar el arranque de la web para migrar automáticamente.
5. Configurar secretos, HTTPS y proxy del proveedor. Preparar roles, configuración inicial y primer administrador mediante un procedimiento controlado; retirar las credenciales iniciales después. La inicialización deshabilitada no crea estos datos.
6. Publicar primero en un entorno de prueba equivalente al productivo. Verificar `/health`, catálogo, registro, acceso administrativo, recuperación y confirmación de correo, carga de imágenes y un pedido de prueba con cancelación y restitución de stock.
7. Reiniciar la instancia y comprobar conservación de imágenes y claves. El carrito y los correos en cola pueden perderse al reiniciar; requieren almacenamiento durable si el negocio necesita esa garantía.
8. Publicar al dominio definitivo y repetir las comprobaciones externas; registrar fecha, versión y evidencia sin credenciales ni datos personales.

## Recuperación ante un fallo

Conservar el artefacto anterior y un respaldo verificado antes del despliegue. Una reversión de código requiere comprobar su compatibilidad con el esquema migrado. No borrar tablas ni ejecutar migraciones descendentes automáticamente para revertir. Restaurar datos solo mediante un procedimiento explícito que considere los pedidos recibidos desde el respaldo.

## Pendientes externos

- Proveedor, dominio y presupuesto.
- SQL Server y almacenamiento persistente disponibles.
- Cuenta SMTP y prueba real de entrega.
- Métodos de cobro y política de reembolso confirmados.
- Objetivos de recuperación de respaldos y responsables de operación.

Esta lista prepara la publicación; no acredita que los servicios ya estén configurados.
