# Revisión integral del proyecto

Fecha: 21 de octubre de 2026. Rama: `mejoras/integral`. Alcance: fases 1–12 del encargo, respetando ASP.NET Core MVC, .NET 10, EF Core, SQL Server y la arquitectura existente.

## Estado por fases

| Fase | Estado | Resultado y límite principal |
|---|---|---|
| 1. Credenciales | Implementada | Se desactiva la cuenta histórica identificada; no se siembra contraseña fija. La revisión del historial fue por patrones y commits alcanzables; no se reescribió Git. Ver [fase 1](fase-1-credenciales.md). |
| 2. Roles y permisos | Implementada | Developer, Owner, Operator y Customer usan una matriz persistida, políticas por permiso, filtros de interfaz y 403 por URL. El Admin histórico no se eleva. Ver [matriz](matriz-permisos.md). |
| 3. Altas seguras | Implementada con límites operativos | Developer inicial protegido por configuración privada y cambio forzado de contraseña; Owner acepta invitación de un uso y configura su contraseña. Owner y Developer no requieren TOTP; el segundo factor sigue opcional para clientes. Tras 15 minutos, las acciones críticas devuelven al login para contraseña (y TOTP solo si la cuenta de cliente lo activó); un POST rechazado debe repetirse manualmente al volver al formulario. Los bloqueos progresan de 5 a 10, 20, 40 y hasta 60 minutos; un acceso completo exitoso reinicia el nivel. |
| 4. Defectos de auditoría | Corregida para los hallazgos descritos | H-01–H-05 y H-08 tienen correcciones y regresiones. H-06 y H-07 conservan límites descritos abajo. |
| 5. OWASP | Implementada en código, falta validación de entorno | CSP con nonce y sin atributos inline, cabeceras, proxy configurable, límites por IP, límites de formulario/subida, cookies, CSRF, cuotas, protección de datos y migraciones controladas. Carrito usa caché SQL distribuida configurable en producción; pruebas confirman lectura entre dos hosts. Dominio, proxy, claves persistentes, SMTP y hosting requieren prueba externa. |
| 6. Validaciones | Implementada en los formularios revisados | Se conservan validación de servidor y cliente, límites y mensajes en español en MVC. La suite cubre regresiones conocidas; no es una verificación formal exhaustiva de cada combinación posible. |
| 7. Correo | Implementada, SMTP pendiente | La cola EmailOutbox es durable, protegida y reintenta fallos; invitaciones usan token almacenado como hash y contenido protegido en la cola. Developer puede inspeccionar mensajes pendientes con destinatarios enmascarados y reencolar fallidos con auditoría. Falta SMTP real, prueba de entregabilidad y una política de retención acordada. |
| 8. Panel | Implementada localmente | Dashboard Admin rediseñado y vistas separadas del Developer para matriz, auditoría y estado. Las capturas adaptables se generan como evidencia local ignorada por Git. No hay medición de carga productiva. |
| 9. UX, accesibilidad y SEO | Auditada, con contraste pendiente de decisión visual | Auditoría completa: 111 capturas, seis flujos aprobados y cero errores estructurales. axe detecta 84 instancias de contraste insuficiente en la paleta existente; se reportan sin cambiar los colores solicitados. Lighthouse móvil 64/94/100/100. Ver [fase 9](fase-9-diagnostico-2026-10-20.md). |
| 10. Paneles y dashboard | Implementada localmente | Avisos accionables y filtros con permiso, vistas efectivas de Developer, actividad propia, línea de tiempo y controles de UI coherentes con la autorización. 234 pruebas Release correctas; sin prueba de carga productiva. Ver [fase 10](fase-10-revision-2026-10-20.md). |
| 11. Privacidad de clientes | Implementada en código | Edición y baja segura de clientes; pedidos conservados con PII anonimizados; operadores no ven contactos; accesos de Owner/Developer y comprobantes registrados. Revisar obligaciones de conservación antes de aplicar en cuentas reales. Ver [fase 11](fase-11-privacidad-2026-10-21.md). |
| 12. Rendimiento y CI | Implementada localmente | Filtro de usuarios en SQL, roles y pedidos por lote, índice Orders.UserId, comprobación de navegador y dependencias en CI. Migración revisada y aplicada solo en SQL temporal; staging/producción pendientes. Ver [fase 12](fase-12-calidad-rendimiento-2026-10-21.md). |

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
| `dotnet test tests/Bisuteria.Tests/Bisuteria.Tests.csproj -c Release` | 234 aprobadas, 0 fallidas y 1 omitida (auditoría de navegador, ejecutada aparte). Fixtures SQL `BisuteriaAudit_<GUID>` desechables. |
| Auditoría real `BROWSER_AUDIT=1`, fase `after` | Aprobada; 111 capturas, seis flujos, cero fallos estructurales. axe reportó 84 instancias serias de contraste; Lighthouse 64/94/100/100. Detalles en [fase 9](fase-9-diagnostico-2026-10-20.md). |
| `dotnet list Bisuteria.slnx package --vulnerable --include-transitive` | Cinco proyectos sin paquetes vulnerables según NuGet. |
| `npm audit --prefix tests/visual` | Cero vulnerabilidades. `node --check tests/visual/browser-audit.cjs` y `git diff --check` también pasaron. |
| Migración nueva `IndexOrderUserId` | Probada por la suite sobre SQL desechable. No se aplicó a ninguna base real; verificar longitudes existentes antes de staging. |

La búsqueda exacta de la cuenta histórica y el escaneo por patrones descritos en [fase 1](fase-1-credenciales.md) no hallaron coincidencias en los 15 commits alcanzables revisados entonces. Ese resultado no prueba que nunca se haya publicado otro secreto con un formato distinto. Si una credencial real se expuso en Git, hay que rotarla; la limpieza del historial queda como procedimiento manual coordinado, no como cambio aplicado.

## Decisiones y comprobaciones externas pendientes

- Ratificar permisos exactos de Owner y que Owner pueda invitar solo Operators (es el valor implementado por defecto).
- Definir cuánto acceso a datos personales tendrá Developer en producción; actualmente se enmascaran y la revelación se audita.
- Configurar SMTP, verificar entrega y decidir si se exige correo confirmado.
- Elegir pasarela de pago real. Cash on delivery/transferencia son registros administrativos; no cobran ni reembolsan dinero.
- Confirmar política de cancelación y límite de cupón por cliente.
- Decidir captcha; actualmente existen honeypot y rate limits.
- Definir hosting y dominio, proxy, `AllowedHosts`, `PublicBaseUrl`, claves persistentes y respaldo/restauración.
- Ejecutar migraciones como paso de despliegue controlado y comprobar el sistema en entorno de staging; nunca activar la migración automática en producción.
- Para varias instancias aún hay que compartir claves de Data Protection y distribuir los contadores del rate limiter; hoy son locales a cada proceso.

No se creó la cuenta real del usuario, no se imprimieron ni guardaron credenciales reales y no se tocó la base real. Esta revisión deja el código y las pruebas locales listos para revisión; no certifica que el sistema esté 100 % preparado para producción.
