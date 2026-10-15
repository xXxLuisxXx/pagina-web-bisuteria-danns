# Revisión integral del proyecto

Fecha: 15 de octubre de 2026. Rama: `mejoras/integral`. Alcance: fases 1–10 del encargo, respetando ASP.NET Core MVC, .NET 10, EF Core, SQL Server y la arquitectura existente.

## Estado por fases

| Fase | Estado | Resultado y límite principal |
|---|---|---|
| 1. Credenciales | Implementada | Se desactiva la cuenta histórica identificada; no se siembra contraseña fija. La revisión del historial fue por patrones y commits alcanzables; no se reescribió Git. Ver [fase 1](fase-1-credenciales.md). |
| 2. Roles y permisos | Implementada | Developer, Owner, Operator y Customer usan una matriz persistida, políticas por permiso, filtros de interfaz y 403 por URL. El Admin histórico no se eleva. Ver [matriz](matriz-permisos.md). |
| 3. Altas seguras | Implementada con límites | Developer inicial protegido por configuración privada, cambio forzado de contraseña y TOTP; Owner acepta invitación de un uso y configura su contraseña. Reautenticación sensible usa antigüedad de cookie; el bloqueo aún no aumenta progresivamente y debe completarse antes de producción. |
| 4. Defectos de auditoría | Corregida para los hallazgos descritos | H-01–H-05 y H-08 tienen correcciones y regresiones. H-06 y H-07 conservan límites descritos abajo. |
| 5. OWASP | Implementada en código, falta validación de entorno | CSP con nonce y sin atributos inline, cabeceras, proxy configurable, límites por IP, límites de formulario/subida, cookies, CSRF, cuotas, protección de datos y migraciones controladas. Dominio, proxy, claves persistentes, SMTP y configuración de hosting requieren prueba externa. |
| 6. Validaciones | Implementada en los formularios revisados | Se conservan validación de servidor y cliente, límites y mensajes en español en MVC. La suite cubre regresiones conocidas; no es una verificación formal exhaustiva de cada combinación posible. |
| 7. Correo | Implementada, SMTP pendiente | La cola EmailOutbox es durable, protegida y reintenta fallos; invitaciones usan token almacenado como hash y contenido protegido en la cola. Falta proveedor SMTP real, prueba de entregabilidad y pantalla operativa para reintentos agotados/retención. |
| 8. Panel | Implementada localmente | Dashboard Admin rediseñado y vistas separadas del Developer para matriz, auditoría y estado. Las capturas adaptables se generan como evidencia local ignorada por Git. No hay medición de carga productiva. |
| 9. Auditoría y privacidad | Implementada con alcance limitado | Registra acciones administrativas, sesiones privilegiadas, cambios y exportaciones con metadatos; enmascara datos de clientes y audita su revelación al Developer. No se almacenan cuerpos sensibles de formularios en el log. |
| 10. Calidad y operación | Implementada localmente, despliegue pendiente | Arquitectura y CI existente, health checks, pruebas SQL portables, migraciones aditivas y documentación. El script idempotente se generó y revisó sin ejecutarlo. No se aplicó ninguna migración a la base real ni se publicó esta rama. |

## Hallazgos H-01 a H-19

| Hallazgo | Estado | Evidencia o razón |
|---|---|---|
| H-01 | Corregido | Transiciones explícitas; cancelación antes del envío; reposición de stock una sola vez. |
| H-02 | Corregido | Reglas de pago, `PaidAt`, reembolso administrativo antes de cancelar una venta pagada y etiquetas en español. No procesa dinero real. |
| H-03 | Corregido | Token idempotente consumido dentro de la transacción e índice único; pruebas concurrentes previas y suite actual. |
| H-04 | Corregido | Contactos editables, validación y umbral de envío vacío permitido. |
| H-05 | Corregido | Criterio de ventas compartido y fechas de Guayaquil para los reportes revisados. |
| H-06 | Parcial | Proxy configurable y cabeceras; falta validarlos contra el proxy y dominio reales. |
| H-07 | Parcial | Respuesta genérica y recuperación en cola reducen enumeración; no se certificó uniformidad temporal ni la operación SMTP real. |
| H-08 | Corregido | `Price` es el precio cobrado; `ComparePrice` debe ser mayor y se valida su precisión. |
| H-09–H-19 | Sin correspondencia en el encargo | El texto recibido no describe estos identificadores. No se les asigna un estado funcional inventado; los requisitos restantes se documentan por fase. |

## Verificación ejecutada en esta rama

| Comando | Resultado real |
|---|---|
| `dotnet build Bisuteria.slnx -c Release` | Correcto; 0 errores y 0 advertencias. |
| `dotnet test tests/Bisuteria.Tests/Bisuteria.Tests.csproj -c Release --no-build --logger "console;verbosity=minimal"` | 155 correctas; 0 fallidas; 0 omitidas. Fixture SQL aislada `BisuteriaAudit_<GUID>`. |
| `dotnet list Bisuteria.slnx package --vulnerable --include-transitive` | Cinco proyectos sin paquetes vulnerables según los orígenes NuGet consultados. |
| `dotnet ef migrations script --idempotent --project src/Bisuteria.Infrastructure --startup-project src/Bisuteria.Web --configuration Release --output audit/migraciones-integral.sql` | Generó un script de 37.371 bytes. Solo se generó el archivo; no se ejecutó contra ninguna base. |
| `npm install --no-package-lock --ignore-scripts` en `tests/visual` | 2 paquetes instalados; 0 vulnerabilidades reportadas por npm. |
| `npm run check` en `tests/visual` | 42 comprobaciones en 390, 768 y 1440 px; `issues: []`. Las 10 interacciones de menú, notificaciones, modo oscuro y personalizador terminaron correctamente. Capturas locales en `audit/screenshots` (incluye Admin oscuro); no se versionan. |

La búsqueda exacta de la cuenta histórica y el escaneo por patrones descritos en [fase 1](fase-1-credenciales.md) no hallaron coincidencias en los 15 commits alcanzables revisados entonces. Ese resultado no prueba que nunca se haya publicado otro secreto con un formato distinto. Si una credencial real se expuso en Git, hay que rotarla; la limpieza del historial queda como procedimiento manual coordinado, no como cambio aplicado.

## Decisiones y comprobaciones externas pendientes

- Ratificar permisos exactos de Owner y que Owner pueda invitar solo Operators (es el valor implementado por defecto).
- Definir cuánto acceso a datos personales tendrá Developer en producción; actualmente se enmascaran y la revelación se audita.
- Configurar SMTP, verificar entrega y decidir si se exige correo confirmado.
- Elegir pasarela de pago real. Cash on delivery/transferencia son registros administrativos; no cobran ni reembolsan dinero.
- Confirmar política de cancelación y límite de cupón por cliente.
- Decidir captcha; actualmente existen honeypot y rate limits.
- Definir hosting y dominio, proxy, `AllowedHosts`, `PublicBaseUrl`, claves persistentes y respaldo/restauración.
- Completar bloqueo progresivo y reautenticación explícita de contraseña/TOTP para acciones críticas si se exige antes de operar.
- Ejecutar migraciones como paso de despliegue controlado y comprobar el sistema en entorno de staging; nunca activar la migración automática en producción.
- Para varias instancias falta almacén compartido de sesión/carrito y claves de Data Protection.

No se creó la cuenta real del usuario, no se imprimieron ni guardaron credenciales reales y no se tocó la base real. Esta revisión deja el código y las pruebas locales listos para revisión; no certifica que el sistema esté 100 % preparado para producción.
