# Auditoría final — BISUTERIAS DANN’S

Fechas de trabajo: 4 y 5 de octubre de 2026 (America/Guayaquil). Alcance: proyecto existente, sin reconstrucción y conservando las cuatro capas.

**Resultado general: correcciones implementadas, compilación certificada y suite final aprobada en Debug y Release: 60/60 en cada configuración.** La certificación se limita a los escenarios comprobados; no implica servicios externos operativos ni seguridad absoluta.

| Clasificación | Alcance concreto |
|---|---|
| **CERTIFICADO** | Restauración y compilación limpia Debug/Release; 60 pruebas aprobadas tanto en Debug como en Release sobre SQL Server; 42 comprobaciones de páginas renderizadas y 5 interacciones; consulta de vulnerabilidades NuGet sin avisos. |
| **IMPLEMENTADO NO VERIFICABLE EXTERNAMENTE** | Envío real de recuperación SMTP y operación real de métodos de pago configurables. |
| **PENDIENTE** | Configuración y validación del despliegue y los servicios externos reales. |

Las funciones parcialmente implementadas y las ampliaciones opcionales se detallan por separado en [Funciones pendientes](funciones-pendientes.md); una prueba general no convierte esas ampliaciones en funciones completas.

La certificación final corresponde a la ejecución del 5 de octubre sobre el código actual. Los registros de las ejecuciones anteriores, incluidas las bloqueadas, se conservaron. Windows permitió la ejecución final sin modificar sus protecciones.

## 1. Estado inicial

Aplicación .NET 10, ASP.NET Core MVC, EF Core SQL Server, Identity y capas Domain/Application/Infrastructure/Web. No existe repositorio Git. Se conservaron los cambios de la auditoría anterior: validación, protección administrativa, carrito por variantes, cancelación idempotente, imágenes y pruebas SQL aisladas.

Se verificó nuevamente la base de 45 pruebas: 45 aprobadas, 0 fallidas, 0 omitidas. Faltaban `.gitignore` y documentación coherente con el estado actual. Los tests estaban concentrados en un archivo, AutoMapper 15.1.3 seguía instalado y persistían fallos no cubiertos por esas pruebas.

Respaldo original: `audit/source-before.zip` e `audit/initial-hashes.csv`. Respaldo antes de esta revisión final: `audit/source-before-final-2026-10-04.zip`. No son respaldos de SQL Server.

## 2. Problemas encontrados

| Problema | Corrección y comprobación |
|---|---|
| El pedido confiaba en precios/nombres/envío del carrito fuera de su transacción | Relectura y cálculo dentro de transacción; prueba de importe manipulado aprobada en bloque 2. |
| Categorías inactivas o personalizaciones de otro producto podían llegar al servicio de pedidos | Validación de categoría, producto y asociación; pruebas aprobadas en bloque 2. |
| Edición de producto podía restaurar stock antiguo tras una venta | Control `OriginalStock`, transacción y movimientos; prueba aprobada en bloque 2. |
| La prueba de stock agregado usaba un producto inactivo | Fixture activa de stock 5, compra 4+4; verifica error de stock y ausencia de escrituras. Aprobada. |
| Prueba de concurrencia aceptaba cualquier InvalidOperationException | Solo acepta el rechazo por stock insuficiente. Aprobada. |
| Notas internas no se guardaban si no cambiaba el estado y mostraban TrackingNumber | Notas independientes, límite de longitud, consulta exclusiva desde controlador Admin. Prueba de persistencia aprobada. |
| Dos reseñas concurrentes podían pasar la consulta de duplicados | Creación serializable; prueba de envío concurrente aprobada. |
| Cupón inválido podía producir pedido sin descuento y consumir un uso | Ahora rechaza antes de modificar stock; redondeo monetario a dos decimales. Prueba final aprobada. |
| Carga múltiple fallida podía dejar archivos nuevos huérfanos | Limpieza limitada a archivos creados por la instancia de esa solicitud. Prueba final aprobada. |
| Galería solo validaba datos en MVC | Validación también en el servicio para crear/actualizar. Prueba final aprobada. |
| Desbordamiento del personalizador y tabla del dashboard en móvil | Anchos de imágenes/nombres y contenedor desplazable. Verificación con HTML generado por la aplicación y CSS actuales satisfactoria. |
| Dirección complementaria y código postal no llegaban al detalle/confirmación | DTO, mapeo y vistas completados; prueba HTTP con comprobación del contenido. |
| El editor del blog anunciaba HTML pero mostraba texto codificado | Etiqueta e instrucciones corregidas; se conserva el formato de texto seguro. |
| Gráfico de ventas crecía demasiado y CSS tenía caracteres mal codificados | Contenedor de gráfico con altura definida y reparación UTF-8. Verificación visual. |

## 3. Problemas de seguridad

Se eliminó la interpolación de `FullName` dentro de JavaScript inline en la confirmación administrativa: HTML encoding no equivale a escapar una cadena JavaScript. El mensaje ahora es estático.

Se mantuvieron y comprobaron en el bloque aprobado: acceso Admin por rol, denegación a visitantes/clientes, antiforgery, rechazo de asignación de rol Admin desde registro, propiedad de pedidos y confirmación restringida, recuperación con origen configurado y token de un solo uso, sesión invalidada al desactivar usuario, validación del contenido de imágenes y rechazo de rutas/URLs peligrosas.

La configuración usa cookies HttpOnly/Secure/SameSite Lax, bloqueo de Identity por intentos fallidos, limitación de formularios por IP, mensajes de error en español y sin stack trace público. Las reseñas públicas ocultan nombres que contienen correo electrónico; las vistas Razor codifican el contenido y los JSON embebidos usan el serializador.

No se publicaron secretos. No se deshabilitó Smart App Control, antivirus ni políticas del equipo. La revisión no sustituye un pentest completo ni certifica controles de infraestructura productiva.

## 4. Código eliminado

- `src/Bisuteria.Application/Mappings/MappingProfile.cs`: sustituido por `EntityMappings.cs`; se retiraron también mapas no consumidos y `ApplicationUserProxy`.
- Referencia NuGet `AutoMapper` y registro `AddAutoMapper`; constructores/campos/usings asociados, incluido el campo no usado de `AdminService`.
- `ICatalogService.GetComparableAsync` y su implementación: sin consumidores; se conserva GetByIdsAsync, utilizado por el carrito.
- `CartItemState.PriceOverride`, parámetro equivalente de `CartManager.AddAsync` y variable redundante del controlador: la sesión no los leía para calcular el precio. La columna histórica `Customization.PriceOverride` se conserva.
- `src/Bisuteria.Web/Views/Shared/_Layout.cshtml.css`: CSS residual de plantilla Bootstrap, sin referencia a la hoja aislada generada desde los layouts.

No se eliminaron entidades históricas, tablas, migraciones, blog, cupones, galería, uploads ni archivos comerciales. Los archivos retirados por reorganización se detallan a continuación. [Inventario exacto de código](archivos-modificados.json), comparado con el respaldo previo a esta revisión.

## 5. Archivos movidos

- `src/Bisuteria.Web/Services/Pricing.cs` → `src/Bisuteria.Application/Services/Pricing.cs`, namespace actualizado: regla común de carrito y pedido.
- `audit/visual-check.cjs` → `tests/visual/check.cjs`, ahora con resolución de Playwright portable y `package.json`.
- La fixture, doble SMTP y entorno de tests salieron de `AuditTests.cs` hacia `tests/Bisuteria.Tests/AuditFactory.cs`.
- `DTOs/Catalog/CatalogDtos.cs` se distribuyó en `ProductDtos.cs`, `CategoryDtos.cs`, `ReviewDtos.cs` y `GalleryImageDto.cs`, dentro de la misma carpeta y namespace. Se conservan juntas las clases de cada responsabilidad.

## 6. Archivos renombrados

`tests/Bisuteria.Tests/AuditTests.cs` → `StorefrontTests.cs`, después de separar la infraestructura compartida. La clase parcial sigue siendo `AuditTests` para compartir una única fixture SQL; los casos nuevos están en `OrderAndSecurityTests.cs` y `AdminModuleTests.cs`. No se renombraron tipos públicos ni rutas comerciales.

## 7. Refactorizaciones

Mapeos explícitos y proyecciones LINQ traducibles a SQL sustituyen AutoMapper. El tamaño del modelo no justificaba una nueva dependencia. AutoMapper 15 requiere licencia según su [guía oficial](https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html); se resolvió retirando la dependencia, sin ocultar el aviso.

Se centralizó el precio de personalización en Application; se encapsuló el guardado de productos en transacción; se separaron tests por responsabilidad y se organizaron DTOs de catálogo. `Models` se conserva porque ya es la convención de Web y moverlo a `ViewModels` no aportaría comportamiento ni claridad suficiente.

Se añadió `.gitignore`; el perfil HTTPS aparece primero. README documenta configuración, permisos de prueba, restricciones y diferencias entre probar un token y enviar correo real.

## 8. Estructura final

```text
Proyecto_Bisuteria/
├── src/
│   ├── Bisuteria.Domain/          # Common, Entities, Enums
│   ├── Bisuteria.Application/     # DTOs, Interfaces, Mappings, Services, Validators
│   ├── Bisuteria.Infrastructure/  # Data, Identity, Migrations, Repositories, Seed, Services
│   └── Bisuteria.Web/             # Areas/Admin, Controllers, Models, Views, Cart, Filters, Services, wwwroot
├── tests/
│   ├── Bisuteria.Tests/           # Fixture + tres archivos de casos por responsabilidad
│   └── visual/                    # Script reproducible y dependencia de desarrollo
├── docs/                          # Este informe e inventario
├── audit/                         # Evidencia y respaldos locales; ignorado por Git
├── .gitignore
├── README.md
└── Bisuteria.slnx
```

## 9. Base de datos

No se cambiaron tablas ni datos comerciales en esta revisión final. La consulta de solo lectura registró en `BisuteriaDb`: **1 producto, stock total 0, 0 pedidos**. No quedaron bases `BisuteriaAudit_*` en la consulta de cierre. Evidencia: `audit/database-readonly.log` y comprobación de cierre `audit/database-readonly-final-2026-10-05.log`.

Las pruebas exitosas crearon, migraron y eliminaron bases temporales con GUID. La fixture verifica la conexión antes de migrar y exige nombre exacto y prefijo antes de borrar. No se ejecutó EnsureDeleted sobre la base real.

No se dispone de un respaldo SQL completo previo para hacer una comparación criptográfica de cada fila; el alcance de esta afirmación es que las operaciones ejecutadas sobre la base comercial fueron SELECT.

## 10. Migraciones

Se conserva exactamente `20260821011850_InitialCreate`, su Designer y `AppDbContextModelSnapshot`. No se añadieron ni modificaron migraciones. `HasPendingModelChanges()` pasó en el bloque 2 y en las suites finales. Las migraciones solo se aplicaron en las bases temporales.

## 11. Autenticación

**CERTIFICADO (bloque 2 y revalidación final):** registro, login/logout, protección de perfil, retorno local, recuperación/token reutilizado rechazado, desactivación que revoca sesión y rechazo de rol Admin falsificado. Identity mantiene hashing, tokens y lockout. Las credenciales de la fixture son ficticias.

**IMPLEMENTADO NO VERIFICABLE EXTERNAMENTE:** entrega real SMTP. No se asegura que un correo llegue a una bandeja de entrada.

## 12. Autorización Admin

**CERTIFICADO (bloque 2 y revalidación final):** rutas representativas GET/POST y CSRF; `AdminBaseController` aplica el rol al área. La prueba final amplía acceso directo a todos los módulos y exportaciones y **pasó en la suite de 60 casos**. Ocultar enlaces nunca se usa como control de autorización.

## 13. Catálogo

**CERTIFICADO (bloque 2 y revalidación final):** navegación pública, filtros/precio, productos y categorías inactivos excluidos, detalle, sitemap y enlaces oficiales. No se cambiaron los datos reales ni la regla histórica de precio efectivo. Se conservó la búsqueda y la personalización existentes. Las URLs de imágenes se validan y las tarjetas tienen fallback.

## 14. Carrito

**CERTIFICADO (bloque 2 y revalidación final):** añadir, cantidades inválidas rechazadas, vaciar, variantes personalizadas independientes, recálculo del precio y checkout HTTP que vacía sesión tras guardar. Se mantiene sesión del lado servidor; no se confía en un precio enviado por navegador. No se certifica persistencia distribuida entre varias instancias.

## 15. Pedidos

**CERTIFICADO (bloque 2 y revalidación final):** creación HTTP, importes recalculados, categoría/personalización válidas, reserva atómica, control de propiedad, cancelación idempotente y bloqueo de reapertura, notas sin cambio de estado y sincronización del estado de pagos.

La validación final más estricta de cupón y su prueba de competencia por el último uso **pasaron sobre la versión final**. Pago “pendiente/pagado” es un registro administrativo, no una transacción bancaria.

## 16. Inventario

**CERTIFICADO (bloque 2 y revalidación final):** dos pedidos por la última unidad solo permiten uno; cantidades de variantes se agregan; cancelación repone una vez; formulario antiguo no puede restaurar stock vendido; movimientos iniciales, venta y ajuste tienen StockAfter esperado. SQL Server usa aislamiento serializable y estrategia de reintentos para conflictos transitorios.

No se cambió el inventario comercial para demostrar compras. El producto real sin stock permanece agotado.

## 17. Seguridad de imágenes

**CERTIFICADO (bloque 2 y revalidación final):** contenido falso rechazado, extensión/tamaño validados, carpeta fuera de allowlist rechazada y PNG válido recodificado como JPEG. Límites: 8 MB por archivo, 24 millones de píxeles, un fotograma; nombres generados por GUID y escritura CreateNew; sin rutas suministradas por el usuario.

**CERTIFICADO en la suite final:** limpieza del primer archivo de un lote cuando falla el siguiente. La misma limpieza se invoca si falla el guardado de producto/galería (revisión de código; no se simuló cada posible caída de disco o SQL). Solo se pueden descartar rutas registradas por la misma instancia; no se borran imágenes antiguas o compartidas. El caso correspondiente pasó.

## 18. Responsive

Resoluciones: **390 × 900, 768 × 900 y 1440 × 900**, capturas de página completa. Páginas: inicio, catálogo, detalle, contacto, login, registro, recuperación, perfil, carrito, checkout, dashboard, productos Admin, formulario Admin y personalizador.

42 comprobaciones estáticas sin desbordamiento del documento, imágenes rotas ni errores JavaScript. Se revisaron visualmente capturas de inicio, personalizador, dashboard y perfil. Las tablas amplias permanecen desplazables dentro de sus contenedores.

**Procedencia final:** el 5 de octubre se regeneraron las páginas mediante la suite final de ASP.NET Core. El navegador leyó directamente `audit/rendered`, sin las modificaciones intermedias de `audit/preview`. La comprobación final pasó 42 combinaciones página/resolución y 5 interacciones (texto, recargos de texto/regalo, abrir/cerrar menú móvil). Evidencia: `audit/visual-final-2026-10-05.log`, `audit/visual-results.json`, `audit/visual-interactions.json` y `audit/screenshots`.

La vista derivada y su procedencia del día anterior se conservan como evidencia histórica, pero no sustentan la certificación final. Esta prueba inspecciona HTML real renderizado con recursos locales; no es una sesión completa de compra en un dispositivo físico. Las solicitudes de compra y autorización se comprobaron por HTTP con SQL Server en la suite de integración.

## 19. Build Debug

**PASS — 0 errores, 0 advertencias.** Restauración y compilación después de limpiar `bin`/`obj`. Registro: `audit/build-debug.log`.

## 20. Build Release

**PASS — 0 errores, 0 advertencias.** Registro: `audit/build-release.log`. Compilar no implica que Windows permita cargar los ensamblados durante una prueba o al iniciar el sitio.

## 21. Tests

| Ejecución | Total | Passed | Failed | Skipped | Interpretación |
|---|---:|---:|---:|---:|---|
| Base, 4 de octubre | 45 | 45 | 0 | 0 | Aprobada, revalidada. |
| Bloque 2, 4 de octubre | 55 | 55 | 0 | 0 | Aprobada con SQL Server temporal. |
| Bloque 3 Debug, 4 de octubre | 60 | 0 | 60 | 0 | Histórico: fallo de inicialización por Control de aplicaciones, antes de las aserciones. |
| Release, 4 de octubre | 0 descubiertas | 0 | 0 | 0 | Histórico: bloqueo durante el descubrimiento; no fue una suite aprobada. |
| **Final Debug, 5 de octubre** | **60** | **60** | **0** | **0** | **PASS, código final y SQL Server temporal.** |
| **Final Release, 5 de octubre** | **60** | **60** | **0** | **0** | **PASS, código final y SQL Server temporal.** |

Evidencia final: `audit/test-results/final-2026-10-05.trx`, `audit/test-results/final-release-2026-10-05.trx` y sus logs `audit/test-final-2026-10-05.log`/`audit/test-release-2026-10-05.log`. Los 5 casos adicionales prueban módulos/exportaciones, CRUD de categoría/blog/galería, cupón concurrente, contacto y rollback de archivos. Todos pasaron. Se ampliaron además los casos existentes para probar dirección complementaria/código postal en confirmación y detalle Admin, y cambio de cantidad del carrito con rechazo de exceso.

Se conservaron también `audit/block-3.log`, `audit/block-3-retry.log`, `audit/test-release.log` y `audit/windows-code-integrity.log`. El bloqueo histórico fue `0x800711C7`; no se ocultó ni se desactivaron protecciones para obtener el resultado final. Las pruebas de concurrencia solo admiten los rechazos de negocio esperados, no cualquier excepción.

## 22. Dependencias

Consulta `dotnet list Bisuteria.slnx package --vulnerable --include-transitive` contra `https://api.nuget.org/v3/index.json`: **sin paquetes vulnerables reportados** para los cinco proyectos a la fecha de la consulta. Incluye dependencias transitivas. Registro: `audit/dependencies-2026-10-04.log`. No garantiza ausencia de vulnerabilidades aún no publicadas.

AutoMapper retirado; no se suprimió su advertencia de licencia ni se agregó un sustituto. Se mantuvieron las versiones existentes de EF Core/Identity, FluentValidation, ImageSharp y ClosedXML. Playwright 1.62.1 se usa únicamente como herramienta de prueba; no se incorpora al servidor.

## 23. Pendientes externos y límites

1. **Windows/Smart App Control:** bloqueo histórico del 4 de octubre, superado en la ejecución del 5 sin cambiar políticas. Se conserva el diagnóstico para investigar si vuelve a ocurrir; no se recomienda desactivar la protección.
2. **SMTP real:** configurar proveedor, credenciales, remitente y origen HTTPS; realizar una entrega controlada. El doble de prueba no certifica al proveedor.
3. **Pagos reales:** confirmar con la tienda los métodos admitidos. No hay pasarela de tarjeta; no se inventaron datos bancarios. Por defecto, ninguno está habilitado.
4. **Despliegue:** dominio/TLS, AllowedHosts, proxy, permisos de uploads, respaldo SQL+archivos y Data Protection deben validarse en el host de destino. La sesión en memoria es para una instancia.
5. **Contenido comercial:** no se inventaron existencias, dirección, correo o cuentas. Se preservaron datos existentes; la tienda debe confirmar contenido real antes de publicar.
6. **Alcance de QA:** no se hizo una prueba de carga masiva, evaluación integral WCAG, pentest externo ni ejecución física en cada modelo de móvil.

No hay razón técnica para eliminar blog, cupones o galería: tienen controladores, vistas, servicios y persistencia en uso. Se conservan.

## 24. Archivos generados limpiados

Se eliminaron exclusivamente `bin/` y `obj/` de los cinco proyectos (10 directorios), después de resolver y verificar que cada ruta estaba dentro del workspace. Lista exacta: `audit/cleaned-generated-paths.txt`. `dotnet restore` y builds los regeneraron. La presencia posterior de esos directorios es normal; `.gitignore` los excluye.

Se conservaron los logs y TRX, incluidos los fallidos, los respaldos ZIP y las capturas porque son evidencia. Están agrupados en `audit/` e ignorados. No se borraron uploads ni bases comerciales. No se usó Git reset/clean/force, ni se inicializó un repositorio sin necesidad.

## Defensa oral breve

El sistema mantiene cuatro capas: Web recibe solicitudes, Application valida y ejecuta casos de uso, Infrastructure persiste y Domain representa las reglas. La mejora central es confirmar precio e inventario en una sola transacción, de modo que una venta concurrente no venda la última pieza dos veces. La edición administrativa comprueba el stock que vio el usuario antes de guardar. Los mapeos explícitos permiten revisar en compilación qué datos se exponen, sin una dependencia con licencia adicional.

Preguntas posibles: **¿Por qué SQL real?** Para probar aislamiento, traducción LINQ y migraciones que InMemory no reproduce. **¿Por qué no se borró un producto con historial?** Para conservar trazabilidad de pedidos e inventario. **¿Por qué no se afirma 100 %?** Porque los tests cubren escenarios concretos y SMTP/pagos reales necesitan servicios y configuración externos. **¿Compilar equivale a funcionar?** No: el compilador revisa el programa, pero la suite debe poder cargarlo y ejecutar sus flujos.
