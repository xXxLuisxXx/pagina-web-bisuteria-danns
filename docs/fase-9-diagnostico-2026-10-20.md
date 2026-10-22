# Fase 9 — diagnóstico reproducible en navegador real

Actualizado: 21 de octubre de 2026.

## Herramienta preparada

`BrowserAuditTests.BrowserFlowsAndAccessibilityUseDisposableDatabase` abre Kestrel HTTPS en una dirección de loopback con puerto asignado por el sistema. Reutiliza la fixture de integración que fuerza la base `BisuteriaAudit_<GUID>`, aplica las migraciones solo allí, genera usuarios ficticios y elimina la base y los comprobantes temporales en `finally`. Sustituye SMTP por `MailCapture`; no envía correos.

El script `tests/visual/browser-audit.cjs` usa Playwright contra ese servidor: las páginas y los formularios recorren el middleware y MVC reales. Las contraseñas aleatorias y claves TOTP de la fixture se transmiten únicamente al proceso hijo mediante variables de entorno. Los informes omiten tokens y las capturas usan exclusivamente datos ficticios.

Se añadieron herramientas de auditoría como dependencias de desarrollo, sin incorporarlas a la aplicación: Playwright 1.62.1, `@axe-core/playwright` 4.14.0 y Lighthouse 13.5.0. El archivo lock permite repetir la instalación. `npm install` informó cero vulnerabilidades el 20 de octubre de 2026. Esto no sustituye el escaneo NuGet.

## Cobertura prevista

- Capturas a **360, 768 y 1366 px**: inicio, catálogo, búsqueda, filtros existentes, detalle, personalizador, carrito, checkout, confirmación/comprobante, registro/login/recuperación, perfil, pedidos, blog/detalle, galería, contacto, información, envíos, privacidad, FAQ y plantillas de error.
- Paneles Owner, Developer y Operator, incluidas lista y detalle de pedidos, productos, usuarios, ajustes, matriz, auditoría y cola.
- HTTP, errores JavaScript/CSP, imágenes rotas, desbordes, enlaces internos, pares candidatos de controles solapados y objetivos táctiles menores de 44 px.
- axe con etiquetas WCAG 2/2.1/2.2 A y AA. Las reglas incompletas se conservan para revisión manual; los candidatos de solapamiento y tamaño no son por sí solos incumplimientos confirmados.
- Lighthouse móvil sobre la página de inicio del servidor HTTPS, conservando JSON y HTML originales, puntuaciones, versión y configuración de medición.
- Flujos con escrituras aisladas: catálogo → carrito → checkout con transferencia y retiro → confirmación → comprobante; registro/login; contacto. El test comprueba también que pedido, comprobante, mensaje y usuario quedaron en SQL temporal.

## Ejecución

Desde la raíz del repositorio, con SDK .NET 10, Node y SQL Server accesible mediante `BISUTERIA_TEST_CONNECTION` (autenticación Windows local como alternativa):

```powershell
npm ci --prefix tests/visual
dotnet build Bisuteria.slnx -c Release
$env:BROWSER_AUDIT = "1"
$env:BROWSER_AUDIT_PHASE = "before"
dotnet test tests/Bisuteria.Tests/Bisuteria.Tests.csproj -c Release --no-build --filter Category=Browser
Remove-Item Env:BROWSER_AUDIT, Env:BROWSER_AUDIT_PHASE
```

La segunda medición usa `BROWSER_AUDIT_PHASE=after`. Los artefactos quedan en `audit/browser/before` y `audit/browser/after`, excluidos del control de versiones por la política existente. En Windows se utiliza Edge; en Linux/CI se instala Chromium de Playwright y puede indicarse `BROWSER_PATH`. El test se omite expresamente en la suite normal si falta `BROWSER_AUDIT=1`.

## Resultados observados

- `npm ci --prefix tests/visual`, `npm audit --prefix tests/visual` y `node --check tests/visual/browser-audit.cjs`: correctos; cero vulnerabilidades.
- La auditoría completa final produjo 111 capturas en 360, 768 y 1366 px: cero fallos estructurales, cero fallos de flujo y seis flujos aprobados (compra y comprobante, registro/login/contacto, cliente, Owner, Developer y Operator).
- El detalle administrativo del pedido mostró un desbordamiento en 360 px porque el identificador largo no tenía puntos de corte; se corrigió el salto de línea y la pasada completa confirmó `scrollWidth=360`.
- axe registró 84 instancias de `color-contrast`, todas serias. Son repeticiones en distintas páginas y tamaños de texto, no 84 reglas distintas. No se alteró la paleta existente. Los 28 candidatos de solapamiento son heurísticos para revisión manual, no fallos confirmados.
- Lighthouse móvil en la corrida final: Performance 64, Accessibility 94, Best Practices 100 y SEO 100.
- El navegador reporta también una inyección de estilo inline bloqueada por CSP con URL de origen vacía, sin bloque `<style>` ni atributo `style` en el HTML entregado. El harness conserva estos avisos separados de errores atribuibles al sitio; no se debilitó la CSP.
- Un aviso de estilo inline con URL de origen vacía proviene de una inyección del navegador y fue bloqueado por la CSP; no aparece un bloque ni atributo de estilo inline en la respuesta inspeccionada. Se mantiene como aviso independiente, sin debilitar la política.

Los scripts históricos `check.cjs` y las exportaciones HTML siguen disponibles, pero no equivalen a recorridos HTTP completos.

## Límites

Las mediciones son de una base ficticia local, con páginas de poco contenido y caché/recursos de ese entorno; no predicen por sí solas el rendimiento del hosting. Fuentes externas pueden afectar Lighthouse. axe no certifica WCAG: quedan pruebas manuales de teclado, lector de pantalla, foco y contexto de contraste. Cualquier contraste insuficiente dentro de la paleta actual se informa para una decisión explícita, sin cambiar colores ni logo.
