# Fase 10 — panel de tienda y desarrollador

Actualizado: 21 de octubre de 2026.

## Cambios

- El dashboard y la campana incluyen comprobantes por revisar y envíos por confirmar. Cada aviso declara el permiso necesario; el contador y sus enlaces solo incluyen las tareas autorizadas para la cuenta.
- Los avisos de comprobantes, envíos, pagos pendientes y pedidos antiguos abren un filtro SQL antes de la paginación. El endpoint comprueba el permiso específico del filtro.
- Se ocultan exportaciones, configuración de la meta, cambios de pago y de estado cuando la matriz no concede el permiso correspondiente. La misma autorización protege la URL o acción de servidor.
- El detalle del pedido muestra una línea de tiempo con creación, fechas de pago y comprobantes, y eventos de auditoría existentes. No reconstruye fechas de transiciones antiguas sin evidencia. Todos los tiempos se presentan en `America/Guayaquil`.
- Los estados de pago y comprobantes se muestran en español. El conteo por estado de la lista de pedidos usa una consulta agrupada y contempla todos los estados.
- El panel Developer tiene una consulta paginada de permisos efectivos por identificador de cuenta, sin exponer sus datos de contacto. La unión de permisos respeta el límite funcional de cada rol y las cuentas desactivadas muestran cero permisos efectivos.
- La pantalla «Mi actividad» consulta exclusivamente el identificador de la sesión, aunque la URL incluya parámetros de otra cuenta. La propietaria puede revisar su actividad sin acceder a la auditoría general.
- Se habilitan los TagHelpers en el área Developer, de modo que sus enlaces y formularios generan destinos reales. El perfil muestra una insignia del rol y el buscador global también se oculta si se revoca su permiso.

## Pruebas añadidas

`Phase10PanelTests.cs` cubre los avisos y filtros de envío/comprobantes; ausencia de acciones financieras en la interfaz del operador y rechazo de sus URL directas; permisos efectivos sin correos; enlaces reales del panel Developer; y aislamiento de la actividad propia frente a otro actor.

## Verificación

Verificación Release del 21 de octubre: `dotnet build Bisuteria.slnx -c Release` terminó con 0 advertencias y 0 errores. `dotnet test Bisuteria.slnx -c Release --no-build` pasó 234 pruebas, 0 fallidas y 1 omitida (auditoría de navegador optativa). La prueba corta de navegador validó los flujos de compra y comprobante con una base SQL temporal.

El sistema técnico existente ya muestra health checks, entorno, versión, SMTP configurado y estado de la cola. Las claves SMTP nunca se presentan en la interfaz. El historial de errores generales fuera de la cola depende del destino de logging configurado para el despliegue.
