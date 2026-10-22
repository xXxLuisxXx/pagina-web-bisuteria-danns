# Revisión de fases solicitadas — 22 de octubre de 2026

Esta revisión parte de `main` en `0270fbe` y se realizó en `mejoras/envios-checkout-ui`. No es una certificación de producción ni significa que el proyecto esté terminado al 100 %. La migración nueva es aditiva y no se aplicó a la base habitual del negocio.

## Avance por fase

| Fase | Resultado comprobado | Estado y límite actual |
| --- | --- | --- |
| 0. Preparación | Se leyó el estado del repositorio, se creó esta rama y se hizo inventario de las herramientas disponibles. Build y suite final pasan; se ejecutaron dos auditorías de navegador, antes y después. | Parcial: `dotnet list package --vulnerable --include-transitive` terminó sin diagnóstico legible (inconcluso). No se usó una base real. |
| 1. Desborde del checkout | Se añadieron reglas de tamaño y ajuste de texto para las miniaturas y líneas del pedido. Playwright ya no detecta desbordes estructurales a 360, 768 y 1366 px, también se capturaron estados oscuros. | Corregido y verificado en navegador. |
| 2. Envíos y código postal | Zonas configurables para Manta/Jaramijó, sector, tarifa, gratuidad y texto estimado; opción “Otro sector (por confirmar)”; validación/recalculo en servidor; snapshot de zona/tarifa en el pedido; destinos y zonas administrables; se oculta el código postal en la experiencia nueva y se conserva la columna histórica. Se muestra el envío como pendiente hasta conocer destino/tarifa. | Implementado; la dueña debe completar los sectores y precios reales. El campo histórico se conserva deliberadamente. La migración aún debe aplicarse mediante procedimiento de despliegue controlado. |
| 3. Transferencia y comprobantes | El flujo preexistente se revisó junto con los ajustes; los datos bancarios se leen de `SiteSetting`, el comprobante se valida/guarda por el servicio existente y el panel ofrece su revisión. Las pruebas de integración continúan pasando. | El flujo de software está cubierto por pruebas ficticias. No se verificó una transferencia bancaria real ni se cargaron datos bancarios reales. |
| 4. Aviso a la tienda | Pedido enviado a la cola `EmailOutbox` a la dirección de contacto editable en `SiteSetting`; canal de WhatsApp abstraído, con mensaje preparado y envío iniciado por el cliente. Se corrigió la dirección de notificación para usar el mismo buzón configurado por la empresa. Se agregaron pruebas para la cola/canal. | Implementado en código. La entrega al Gmail real sigue sin confirmación externa; pedidos ya procesados como `.eml` locales no se reenvían solos. WhatsApp no usa API oficial. |
| 5. Menú del panel | Navegación agrupada por área y permisos, estado del grupo, badges, cola de pedidos pendientes, enlaces activos, cajón móvil y estado accesible. Se corrigieron conteos de inventario y pedidos. | Implementado y revisado en vistas; el estilo/color del panel se conserva. |
| 6. Tienda pública | Se ajustaron textos/jerarquía y vistas de carrito, detalle, checkout, confirmación, envíos, preguntas frecuentes, privacidad, estados y metadatos. La política de privacidad ahora se edita como texto plano en configuración. Se ejecutó Playwright, axe y Lighthouse. | Parcial. axe detecta 86 instancias `color-contrast`; se conservaron los colores por instrucción. Lighthouse móvil: rendimiento 58, accesibilidad 94, buenas prácticas 100, SEO 100. Quedan mejoras de rendimiento, revisión WCAG manual y otras capacidades no certificadas (por ejemplo cookies y dispositivos reales). Los 28 candidatos de solapamiento son heurísticos y requieren inspección visual. |
| 7. Seguridad y validaciones | Las funciones nuevas tienen validación servidor, antiforgery, permisos, límites y auditoría según los controladores/servicios afectados; se añadieron regresiones para las reglas principales y privacidad del destino del pedido para operadores. | Parcial: no se pudo confirmar exhaustivamente recuperación uniforme, proxy/dominio de producción, límites compartidos multi-instancia ni revisión de pinning de acciones CI en esta pasada. No se probó SMTP real ni un proveedor productivo. |
| 8. Calidad y cierre | Se ejecutó Release build, suite de integración, verificación JS, `git diff --check`, tres revisiones independientes del código y auditoría de navegador completa. | Este informe registra evidencias y pendientes; sigue abierta la configuración comercial y la validación externa. |

## Evidencia de la última verificación

- `dotnet build Bisuteria.slnx -c Release`: correcto, 0 advertencias y 0 errores.
- `dotnet test Bisuteria.slnx -c Release --no-build --no-restore -m:1 -nr:false`: 262 pasaron, 1 omitida, 0 fallidas (263 total). La fixture usó bases temporales `BisuteriaAudit_<GUID>`.
- `node --check tests/visual/browser-audit.cjs` y `node --check src/Bisuteria.Web/wwwroot/js/site.js`: correctos.
- `git diff --check`: correcto; Git informó que normalizará LF a CRLF en `Views/Home/Privacy.cshtml` al tocarlo.
- Playwright full (`BROWSER_AUDIT_PHASE=after`): 114 capturas en 360, 768 y 1366 px; 6 flujos pasaron; 0 desbordes estructurales y 0 flujos fallidos. Flujos: catálogo-carrito-checkout-confirmación-comprobante, registro-login-contacto, perfil-pedidos, panel Owner, panel Developer y panel Operator.
- axe: 86 hallazgos, todos de contraste `color-contrast` (serios según axe); el auditor informa 28 candidatos de solapamiento para inspección, no fallos confirmados.
- Lighthouse móvil local: rendimiento 58, accesibilidad 94, buenas prácticas 100 y SEO 100. La medición es una referencia local y puede variar; no garantiza el rendimiento desplegado.
- Evidencia visual local ignorada por Git: `audit/browser/before/` y `audit/browser/after/` (114 capturas de la pasada final, JSON de reporte y resultados Lighthouse). No se incluyeron capturas ni datos de clientes en el repositorio.

## Revisiones y herramientas

Se ejecutó `codex features list`; se utilizaron la suite local .NET, Playwright, axe, Lighthouse y tres revisiones paralelas de seguridad, panel/envíos y cobertura/documentación. Los hallazgos accionables corregidos incluyeron campos de destino visibles a operadores, destino de entrega local ausente en el detalle administrativo, cola de pedidos que abría la lista general, conteo de stock agotado, texto de total cuando faltaba cotizar, configuración SMTP y privacidad editable. También se hizo revisión manual del diff.

No se instalaron plugins, paquetes ni servidores MCP. No se consultó un MCP oficial de documentación: no había uno disponible en este entorno. El servidor MCP de GitHub no se usó para publicar/revisar un PR porque esta rama no tiene PR. La herramienta `codex review` como comando no produjo un informe separado; la revisión disponible fue la revisión manual más los tres agentes y la inspección automatizada. No se desactivó el sandbox.

## Decisiones y pasos externos pendientes

1. La propietaria debe registrar los sectores y tarifas reales de Manta y Jaramijó, indicar qué sectores son gratis, confirmar o desactivar el envío gratis desde un monto y definir tiempos/condiciones.
2. La propietaria debe revisar y completar desde Configuración los datos bancarios, WhatsApp, correo de contacto, privacidad, cambios/devoluciones y demás textos comerciales. Los textos legales deben validarse con un profesional y la normativa ecuatoriana de protección de datos.
3. Confirmar Gmail SMTP habilitado y comprobar recepción con un pedido de prueba controlado. El código por sí solo no demuestra entrega; no se imprimen credenciales ni se hizo un envío de prueba a clientes.
4. Confirmar las reglas definitivas de permisos de Owner/Operator y la política de cancelación.
5. Antes del despliegue: revisar/aplicar la migración aditiva en una copia de respaldo, configurar dominio, hosting, HTTPS, secretos, almacenamiento persistente y políticas de varias instancias; después probar en producción con autorización de la propietaria.
6. Resolver contraste y rendimiento sin cambiar la paleta ni el logo hasta recibir autorización sobre colores; completar revisión manual de teclado y tamaños táctiles en dispositivos reales.

La dirección del correo, credenciales, teléfonos, WhatsApp, datos bancarios y otros datos del negocio no se incluyen en este informe ni en el código público.
