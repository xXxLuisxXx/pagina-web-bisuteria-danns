# Fase 7 — validaciones

Se revisaron los filtros globales de parámetros y FluentValidation, los DTO de catálogo, blog, galería, categorías, cupones, checkout y los formularios de configuración, perfil y usuarios. Los identificadores inexistentes y las queries inválidas tienen regresiones en `ValidationBoundaryTests`.

Cambios de esta ronda:

- Los rangos decimales de DataAnnotations interpretan sus límites con cultura invariante. Antes un precio válido podía provocar una excepción al validar bajo `es-EC`; se comprueban explícitamente `es-EC` y `en-US`.
- Stock y stock mínimo comparten el límite de 0 a 1 000 000 en navegador y servidor.
- Los cupones limitan fechas a 2000–2100; su formulario ofrece fecha inicial y muestra errores junto a los campos.
- Las reseñas publican restricciones de producto, calificación, título y comentario mediante DataAnnotations además del validador de servidor existente.
- El perfil usa una lista de provincias del Ecuador y rechaza valores inventados en el servidor.
- La cotización del envío rechaza fracciones de centavo y valores fuera de rango sin modificar el pedido.
- El formulario de producto indica correctamente que las imágenes adicionales usan rutas locales `/img/`.

Las operaciones nuevas de edición/eliminación administrativa de clientes se cierran con sus permisos, auditoría y pruebas en la fase 11. La comparación de identificadores de ruta y formulario descubrió otro defecto de model binding; su arreglo permanece con esa funcionalidad.

Verificación ejecutada el 20 de octubre de 2026:

- `dotnet build Bisuteria.slnx -c Release`: correcto, 0 advertencias y 0 errores.
- `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=fase7-definitivo.trx' --results-directory audit/test-results`: 220 correctas, 0 fallidas, 0 omitidas. Incluye el estado acumulado de la copia de trabajo y las pruebas de usuarios que estaban pendientes de commit.
- Regresiones en `ValidationPhase7Tests`: importe de envío, provincias, rangos de stock, fechas de cupones, reseñas y cultura de importes.

No se modificó la base de la aplicación. Las integraciones usaron exclusivamente bases `BisuteriaAudit_<GUID>`.
