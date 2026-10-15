# Ampliación del encargo: seguridad, correo y diseño administrativo

## Punto de partida

Solicitud del 12 de octubre de 2026, con seis fases e imagen de referencia. Se retoma `mejoras/auditoria` desde `6d91342`, igual a main al iniciar. El commit `6fd5524` mencionado en el encargo es histórico: no se descartan las correcciones posteriores. Los informes de cinco fases anteriores no certifican este nuevo alcance de seis fases.

## Fase 1: revisión de las correcciones existentes

Se leyeron las reglas compartidas de transiciones de pedido/pago, OrderService, SalesCriteria, SettingService, validación de precios y regresiones existentes. Se conserva su implementación; no se detectó en esta revisión un defecto nuevo que requiriese cambiar estas reglas.

Evidencia de regresión existente:

| Requisito | Pruebas |
|---|---|
| Transiciones y stock una sola vez | OrderTransitionsPreserveStock, CancellationBeforeShippingRestoresStockOnce |
| Pago, reembolso y fecha | PaidOrderRequiresRefundBeforeCancellationAndPreservesPaidAt |
| Checkout concurrente | ConcurrentCheckoutTokenCreatesExactlyOneOrder, ConcurrentHttpCheckoutCreatesOneOrder |
| Contactos editables y envío gratuito opcional | SettingsCanChangeContactAndDisableFreeShipping |
| Ventas y Guayaquil | SalesExcludeCancelledAndUseGuayaquilDay |
| Login desactivado genérico | DisabledLoginHasGenericResponseAndNeverCreatesSession |
| Precio cobrado y referencia | PriceValidationUsesSalePrice |
| SKU, cupón, retiro e imagen | DuplicateSkuIsRejectedWithoutChangingTheOtherProduct, CancellationReleasesCouponOnceAndSingleUseCanBeEnabled, PickupCheckoutDoesNotRequireAddressOverHttp, TransparentPngBecomesWhiteJpeg |

La fixture fuerza un catálogo `BisuteriaAudit_<GUID>` y comprueba su nombre antes de migrar y eliminar. No se inició la aplicación con su conexión real ni se aplicaron migraciones a esa base. No se añadieron nuevas pruebas porque en esta fase se revalidaron correcciones previas, sin modificar código. No se reprodujo la fase roja contra el commit histórico. La igualdad temporal de recuperación no está acreditada; su cola actual es de memoria.

- `dotnet build Bisuteria.slnx -c Release`: correcto, 0 errores y 0 advertencias.
- `dotnet test Bisuteria.slnx --logger 'trx;LogFileName=revision-fase1-20261012.trx' --results-directory audit/test-results`: 134 aprobadas, 0 fallidas, 0 omitidas; ejecución de pruebas de 38 segundos en Debug.

Único archivo cambiado en esta fase: `docs/ampliacion-20261012.md`. Evidencia local: `audit/test-results/revision-fase1-20261012.trx` (ignorada por Git). El escaneo de dependencias del nuevo encargo corresponde a la fase 2 y no se ejecutó en esta fase.

## Trabajo restante, en el orden solicitado

2. Seguridad: TOTP opcional implementado en esta ampliación, con recomendación para Admin, recuperación de uso único y regresiones. Consultar `ampliacion-fase2-seguridad.md` para pruebas, escaneos y límites pendientes; no constituye certificación integral de seguridad.
3. Validaciones: ampliadas y verificadas con 143 pruebas; ver ampliacion-fase3-validaciones.md.
4. Estructura: revalidada con 143 pruebas; Address conservada y limitaciones de índices documentadas en ampliacion-fase4-estructura.md.
5. Administración y correo: implementados MustChangePassword, prohibición de reutilizar contraseña inicial, EmailOutbox persistente, reintentos, plantillas y notificaciones por evento con sustituto SMTP. 147 pruebas aprobadas; ver ampliacion-fase5-correo.md. Cuenta real y SMTP externo pendientes.
6. Diseño: layout compartido, búsqueda global, notificaciones, calendario, meta mensual, modo oscuro y gráficos locales implementados; ver ampliacion-fase6-panel.md para evidencia y límites. No se aplican migraciones sobre la base real.

## Decisiones externas

Se mantiene la preferencia del usuario por hosting gratuito. La entrega real de correos necesita un proveedor y hosting que permitan correo saliente; no se acredita con un sustituto SMTP. Gmail se configurará privadamente y se verificará su compatibilidad al implementar la fase 5. Siguen pendientes decisiones comerciales sobre pagos, cancelaciones, cupón por cliente, captcha y dominio.

## Explicación para defensa

Se parte del código ya probado para evitar perder correcciones. Las reglas de negocio se comprueban con SQL aislado y regresiones HTTP, mientras que correo e interfaz se incorporan en fases posteriores. Compilar y pasar pruebas de la fase 1 no demuestra que las seis fases nuevas estén terminadas.
