# Seguimiento de la auditoría por fases

Fecha: 6 de octubre de 2026. Rama: `mejoras/auditoria`. Base: `6fd5524`.

## Estado actual

Fase 1 en curso. Fases 2–5 no iniciadas. El usuario autorizó usar `dotnet test Bisuteria.slnx -c Release` en lugar de Debug y continuar investigando errores. La última compilación pasa; la última ejecución de pruebas está bloqueada por Smart App Control. No se alteraron protecciones de Windows. No se han creado commits de fase ni publicado cambios sin validar.

## Cambios preparados

| Hallazgo | Estado | Implementación y límite |
| --- | --- | --- |
| H-01 | Parcial: código y regresiones implementados | Tabla compartida de transiciones, rechazo de cancelación tras envío/entrega y estados válidos en Admin. Casos iniciales pasaron; falta validar nuevamente el árbol final. |
| H-02 | Parcial: código y regresiones implementados | Transiciones de pago, rechazo en cancelados, PaidAt conservado, reembolso confirmado antes de cancelación pagada y etiquetas en español. No se procesa dinero. |
| H-03 | Parcial: código y regresiones implementados | Token de formulario validado contra sesión, índice único SQL y recuperación del pedido dentro de transacción serializable; botón deshabilitado al enviar. Concurrencia de servicio y HTTP pasó en la revisión intermedia. |
| H-04 | Parcial: código implementado | Configuración desde SiteSetting, retiro de constantes de contacto y readonly, validación de teléfono/correo/HTTPS, umbral vacío y campo identificado al rechazar. El caso de guardado pasó; faltan ajustes de presentación de todos los mensajes. |
| H-05 | Parcial | SalesCriteria compartido, exclusión de cancelados, filtro de pago y límites diarios Guayaquil en reporte de ventas. Dashboard usa criterio y límites locales. Falta completar consistencia de filtros de estado en exportaciones y fechas del reporte por producto. |
| H-06 | Pendiente | Proxy y despliegue: fase 2. |
| H-07 | Parcial | Login desactivado/bloqueado con mensaje genérico; comprobación de contraseña antes de iniciar sesión. Recuperación en cola limitada sin consulta de cuenta en la petición. Falta ampliar pruebas de cola y métricas de tiempo; cola en memoria no es durable. |
| H-08 | Parcial: código y regresiones implementados | Price es el importe cobrado; ComparePrice solo referencia superior, validaciones de 2 decimales y catálogo consulta Price. Casos de precios pasaron. No se cambian importes históricos. |
| H-09–H-19 | Pendiente de correspondencia | El texto recibido no identifica inequívocamente estos números; no se inventan equivalencias. Se mantienen los requisitos por su nombre. |

Otros cambios preparados: índice SKU único filtrado adicional (con comprobación previa de duplicados, sin borrar registros), rechazo amistoso de SKU duplicado, filtro de errores de persistencia, nombre real en checkout, dirección opcional al retirar, CouponCode persistido y devolución de UsedCount una sola vez, límite configurable de cupón por cuenta, fondo blanco para PNG transparente.

`CouponSingleUsePerCustomer` se inicializa en false y puede configurarse en SiteSetting. Al habilitarlo se exige cuenta autenticada. Cancelar libera el uso. Estas reglas requieren confirmación comercial antes de producción. No se limita por correo de invitado, porque no identifica de forma fiable a una persona.

## Migración

`20261007040000_CheckoutIdempotency`: añade dos columnas nullable a Orders, índice único del token y un índice único filtrado de SKU, conservando el índice anterior. Up es aditivo y aborta si ya hay SKU duplicados. No se ejecutó contra BisuteriaDb. Snapshot y diseñador incluidos. La herramienta EF fue bloqueada por integridad de Windows; la migración se escribió y revisó como código y la suite intermedia confirmó la concordancia del modelo y su aplicación en base temporal.

## Resultados reales

- Compilación Release inicial: 0 errores, 0 advertencias.
- Suite base Release: 60/60 aprobadas.
- Pedidos/pagos: 75/75 aprobadas.
- Idempotencia/migración: 76/76 aprobadas.
- Revisión intermedia con reportes, configuración, precios y HTTP concurrente: 87/87 aprobadas, 0 fallidas y 0 omitidas (29 segundos informados por el runner).
- Se añadieron después cuatro casos: SKU duplicado, liberación/límite de cupón, login desactivado y PNG transparente. También se ajustó el formulario de configuración y los enlaces de exportación.
- Último `dotnet build Bisuteria.slnx -c Release`: correcto, 0 errores y 0 advertencias.
- Último `dotnet test Bisuteria.slnx -c Release --no-build`, y su reintento: bloqueo de carga de Bisuteria.Tests.dll, `0x800711C7`. No hubo ejecución de casos; no se atribuye el resultado 87/87 a esta versión final.
- `git diff --check`: sin errores de whitespace; Git advierte normalización LF/CRLF.
- No se ejecutó un nuevo análisis de vulnerabilidades ni una nueva revisión visual en este turno.
- Las regresiones se verificaron después de cambios en las ejecuciones indicadas; no se ejecutaron previamente todas contra el commit original. No se afirma evidencia rojo/verde para los casos no comprobados en ambas versiones.

## Bloqueo de entorno investigado

Registro Microsoft-Windows-CodeIntegrity/Operational, evento 3077 a las 22:44:05: testhost.exe rechazado al cargar la DLL Release de pruebas por requisitos de firma/integridad. Las elevaciones de ejecución no resuelven esta política. Hubo también bloqueos intermedios sobre otras DLL; algunas compilaciones posteriores se ejecutaron sin tocar protecciones, sin que esto demuestre una solución permanente.

SQL Server está en ejecución. Docker no está disponible en PATH. `wsl --list --quiet` falló con `Wsl/CallMsi/Install/REGDB_E_CLASSNOTREG` (clase no registrada). No se instaló ni se reconfiguró WSL.

Para cerrar la fase hace falta un entorno de desarrollo que permita ejecutar los binarios, o un runner CI con SQL Server y configuración portable de pruebas. No se deben aprobar ni publicar los cambios como verificados basándose únicamente en la compilación.

## Próximos pasos

1. Resolver el entorno de ejecución y correr los casos añadidos pendientes.
2. Completar los límites de H-04/H-05/H-07 y pruebas de errores de persistencia y retiro HTTP; revisar mensajes de excepciones inesperadas aún capturados como InvalidOperationException.
3. Repetir build y suite completa, documentar resultados y crear el commit de fase 1.
4. Seguridad (fase 2), validaciones (fase 3), estructura/CI (fase 4) y dashboard (fase 5), cada una con sus comprobaciones y commit.
5. Conservar Address. No aplicar migraciones a datos reales ni simular SMTP o pagos externos.

## Reanudación — 9 de octubre de 2026

El TRX final de la fase 1, escrito el 6 de octubre a las 22:53, confirma 92 pruebas ejecutadas y aprobadas, 0 fallidas y 0 omitidas. El bloqueo descrito arriba es histórico: la ejecución final pudo completar. Al retomar el 9 de octubre, `dotnet build Bisuteria.slnx -c Release` volvió a pasar con 0 errores y advertencias.

Se corrigió la fixture de PNG para incluir sus cabeceras HTTP; su primer intento falló con NullReferenceException y el siguiente pasó. Se completaron filtros de estado en exportaciones y límites Guayaquil del reporte de productos. Se agregó BusinessRuleException: los controladores solo muestran mensajes de errores previstos; las excepciones inesperadas siguen el manejador genérico. Se añadió y aprobó el retiro HTTP sin dirección.

Fase 1 implementada y comprobada con los 92 casos. No se garantiza durabilidad de la cola SMTP en memoria ni entrega externa. La prueba rojo/verde contra el commit original no se ejecutó para todos los hallazgos. Las métricas de tiempo de recuperación no están certificadas. Estos límites se conservan explícitos.
