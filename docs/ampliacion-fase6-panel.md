# Fase 6: panel administrativo

13 de octubre de 2026. Referencia visual: panel gris/blanco con barra lateral y tarjetas redondeadas proporcionado por el usuario.

## Cambios

El layout compartido aplica a toda el área Admin: fondo gris, lateral blanco, navegación en píldoras, tarjeta de acceso a la tienda, cierre de sesión, buscador, perfil y notificaciones. Colores, radios y sombras se concentran en admin.css. El modo oscuro opcional conserva la preferencia en el navegador. Se mantiene Bootstrap, Razor, iconos existentes y SVG propios; no se agregó una biblioteca.

Dashboard: primera tarjeta negra, cuatro métricas comparables con su periodo previo y productos activos como foto actual. No se inventa una variación histórica de activación de productos. Píldoras Día/Semana/Mes/Año, rango personalizado, barras con máximo destacado, gráficos existentes, calendario mensual navegable con pedidos no cancelados, selección de día y meta mensual configurable con indicador circular. La meta corresponde al mes del calendario, aunque el rango seleccionado sea menor. Los importes siguen SalesCriteria y Guayaquil; no son cobros bancarios confirmados. La tabla incorpora cliente y producto principal, estados y acciones válidas.

Búsqueda global limitada a diez pedidos y diez productos, por número, cliente, nombre o SKU, en dos consultas proyectadas. Autorización heredada de AdminBaseController. Las notificaciones resumen pedidos pendientes, mensajes sin leer, stock bajo y reseñas pendientes; no son una bandeja de eventos leídos por usuario. Caché de 30 segundos compartida con el dashboard predeterminado. El calendario requiere una consulta agrupada mensual y otra para la meta; no hay consultas por cada fila. Sigue sin existir una medición de carga productiva.

El menú móvil usa aria-expanded, inert y Escape. Se añaden foco visible, etiquetas, estados vacíos, acceso por teclado a gráficos desplazables y un estado de carga al enviar filtros. Las tablas pueden desplazarse dentro de su contenedor. No se introducen scripts inline ni dependencias gráficas externas.

## Pruebas

El escaneo del 13 de octubre detectó dos avisos nuevos en System.Security.Cryptography.Pkcs 9.0.11. Se añadió una referencia explícita al parche 9.0.21 en Infrastructure para elevar la dependencia transitiva de SQL Client sin cambiar su versión mayor. Justificación: [aviso de Microsoft CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx) y [CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7). No se afirma explotabilidad de estas rutas en la tienda; se elimina la versión vulnerable.

AdminRedesignTests añade búsqueda por SKU, límite de entrada y denegación a invitados/clientes; calendario y meta con pedidos conocidos, incluyendo cancelados y fecha Guayaquil. Las regresiones anteriores cubren KPI, exportación y acceso a los demás módulos.

Build Release: 0 errores y 0 advertencias. Suite completa: 149 aprobadas, 0 fallidas, 0 omitidas. Después de ajustar el layout final se volvió a compilar y a exportar el HTML con ExportRenderedPagesForResponsiveChecks, aprobado. Evidencia SQL: audit/test-results/ampliacion-fase6.trx.

El script tests/visual/check.cjs verifica 14 páginas a 390/768/1440 px con CSP activa y añade interacciones de menú Admin, notificaciones y modo oscuro a las cinco del sitio público. Las capturas usan HTML renderizado por el servidor y datos temporales; no representan pedidos reales ni equivalen a probar dispositivos físicos.

## Pendientes externos y límites

Comprobación adicional del 14 de octubre: corregido el color heredado de las etiquetas de formularios en modo oscuro y el contraste del botón del tema al pasar el mouse. La regresión visual mide una relación mínima de 4,5:1 para etiquetas, navegación, botones delineados y selector del tema, tras finalizar las transiciones. Pasaron nuevamente las 42 comprobaciones adaptables y las diez interacciones; la comprobación específica de contraste también pasó. Evidencia local: audit/visual-contrast.json. Esto no sustituye una auditoría completa de accesibilidad. GitHub Actions del commit 70af1e4 terminó correctamente.

- No se aplicaron migraciones ni se creó la cuenta oficial en la base real. Se necesita respaldo/revisión para ese paso y configuración privada de InitialAdmin.
- SMTP real y entregabilidad Gmail no verificados. La cola conserva fallos para reintentos; ocho intentos agotados requieren intervención. Falta interfaz de reintento manual y política de retención.
- No hay historial completo para variar el KPI de productos activos ni carga productiva certificada. El índice UserId sigue diferido por la restricción aditiva.
- Pagos son registros administrativos, sin pasarela bancaria. Hosting, dominio, política de negocio y captchas siguen siendo decisiones externas.
- H-09 a H-19 no están numerados en el encargo recibido: no se inventa correspondencia. Se conserva la matriz de docs/resultado-auditoria.md con las razones y estos informes detallan requisitos por nombre.

## Defensa oral

La misma estructura MVC sirve toda la administración y un único archivo de estilos mantiene consistencia. Las cifras se calculan en SQL, el calendario respeta la zona local y las búsquedas tienen límites. La nueva apariencia conserva las reglas de compra y autorización; las pruebas cubren cálculos y acceso por separado de la revisión visual.

## Cierre local

Resultado final tras el parche: build Release sin errores ni advertencias; 149 pruebas aprobadas, 0 fallidas, 0 omitidas (50 s). Escaneo actualizado de los cinco proyectos sin paquetes vulnerables. Revisión visual final: 42 comprobaciones y 10 interacciones aprobadas, sin incidencias. Capturas inspeccionadas de escritorio y móvil; modo oscuro comprobado automáticamente. Se generó audit/migraciones-ampliacion.sql sin ejecutarlo.

Archivos modificados:
README.md
docs/ampliacion-20261012.md
docs/despliegue.md
docs/resultado-auditoria.md
src/Bisuteria.Application/DTOs/Admin/AdminDtos.cs
src/Bisuteria.Application/DTOs/Orders/OrderDtos.cs
src/Bisuteria.Application/Interfaces/IAdminService.cs
src/Bisuteria.Infrastructure/Bisuteria.Infrastructure.csproj
src/Bisuteria.Infrastructure/Services/AdminDashboard.cs
src/Bisuteria.Web/Areas/Admin/Controllers/SettingsController.cs
src/Bisuteria.Web/Areas/Admin/Views/Dashboard/Index.cshtml
src/Bisuteria.Web/Areas/Admin/Views/Settings/Index.cshtml
src/Bisuteria.Web/Areas/Admin/Views/Shared/_AdminLayout.cshtml
src/Bisuteria.Web/Services/EmailSender.cs
tests/Bisuteria.Tests/StorefrontTests.cs
tests/visual/check.cjs
docs/ampliacion-fase6-panel.md
src/Bisuteria.Infrastructure/Services/AdminSearch.cs
src/Bisuteria.Web/Areas/Admin/Controllers/SearchController.cs
src/Bisuteria.Web/Areas/Admin/Views/Dashboard/_Columns.cshtml
src/Bisuteria.Web/Areas/Admin/Views/Search/Index.cshtml
src/Bisuteria.Web/ViewComponents/AdminNotificationsViewComponent.cs
src/Bisuteria.Web/Views/Shared/Components/AdminNotifications/Default.cshtml
src/Bisuteria.Web/wwwroot/css/admin.css
src/Bisuteria.Web/wwwroot/js/admin.js
tests/Bisuteria.Tests/AdminRedesignTests.cs
