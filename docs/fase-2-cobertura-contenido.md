# Fase 2: textos públicos de cobertura

La cobertura comunicada en la barra superior, pie de página, inicio, detalle de producto, página de envíos y preguntas frecuentes queda limitada a **Manta y Jaramijó, Manabí**.

Se retiraron las promesas de cobertura nacional, seguimiento, tarifa fija, envío gratis desde un importe escrito en la vista y plazos de preparación o entrega sin confirmar. El sitio explica que la tarifa depende del sector y que los sectores pendientes requieren cotización antes del pago. El retiro se describe como coordinación de lugar y hora.

## Ajustes editables utilizados

| Clave de SiteSetting | Uso | Si está vacía |
| --- | --- | --- |
| `ProductionTimeText` | Preparación del pedido y respuesta en preguntas frecuentes | La tienda debe confirmar el plazo de preparación. |
| `ShippingEstimatedTime` | Tiempo general de entrega | El plazo se confirma según el sector. |
| `ShippingConditions` | Condiciones adicionales de entrega | Se avisa que están pendientes de confirmar. |
| `ReturnsPolicyText` | Política de cambios y devoluciones, también en preguntas frecuentes | Se pide consultar las condiciones antes de concretar la compra. |

Los valores se muestran codificados como texto; no se interpreta HTML guardado en ajustes. No se agregaron datos reales del negocio ni tarifas o sectores inventados. La regla de envío gratis por monto debe permanecer desactivada hasta que el negocio la confirme; la página pública solo anuncia como gratuito un sector marcado con esa condición. Cualquier activación futura del umbral debe compartir la misma regla en el cálculo del servidor y en los textos.

## Metadatos y alcance de la comprobación

Las páginas de envíos y preguntas frecuentes disponen de descripción propia para metadatos, Open Graph y Twitter. El JSON-LD actual del producto no declaraba cobertura nacional ni seguimiento; no se agregó una tarifa uniforme al marcado porque el precio depende del sector.

Se revisaron las vistas, la búsqueda de textos obsoletos y el recorrido real con Playwright. La pasada final generó 114 capturas en 360, 768 y 1366 px; no detectó desbordes estructurales ni flujos fallidos. Se probaron seis flujos críticos, incluido el checkout con un sector y la confirmación. Las pruebas integradas verifican el cálculo en servidor y escenarios de tarifas.

La auditoría automática encontró problemas de contraste que se detallan en `revision-fases-negocio-2026-10-22.md`. Se conservaron los colores existentes como pide el negocio; por eso el resultado no equivale a conformidad WCAG 2.2 AA.

No se modificaron colores, CSS, logo, tamaño o posición del logo ni familias tipográficas.
