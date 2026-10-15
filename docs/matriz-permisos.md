# Matriz inicial de permisos

La migración aditiva `RolesAndPermissionMatrix` crea los roles Developer, Owner, Operator y Customer, y persiste los permisos iniciales de roles administrativos. Las cuentas que aún tienen el rol histórico `Admin` no se convierten ni reciben permisos automáticamente.

| Capacidad | Developer | Owner | Operator | Customer |
|---|---:|---:|---:|---:|
| Dashboard, notificaciones y búsqueda del panel | Sí | Sí | Sí | No |
| Ver pedidos y actualizar estados válidos | Sí | Sí | Sí | No |
| Actualizar pagos | Sí | Sí | No | No |
| Productos, inventario y categorías | Sí | Sí | Solo inventario | No |
| Cupones, reseñas, mensajes, blog y galería | Sí | Sí | No | No |
| Ver y exportar reportes | Sí | Sí | No | No |
| Ajustes comerciales y clientes limitados | Sí | Sí | No | No |
| Invitar Owner | Sí | No | No | No |
| Invitar y gestionar Operator | Sí | Sí | No | No |
| Ver y editar matriz de permisos | Sí | No | No | No |
| Auditoría, estado del sistema y ajustes técnicos | Sí | No | No | No |

Los valores son configurables desde `Developer/Roles`; el formulario solo expone Owner y Operator. Cada cambio invalida los sellos de seguridad de las cuentas del rol editado. Las rutas administrativas devuelven 403 si la cuenta autenticada no tiene el permiso. La interfaz del panel evalúa los permisos antes de mostrar sus entradas.

La política inicial permite al Owner invitar y desactivar Operators, nunca Owner ni Developer. Developer puede invitar Owner y Operator, y gestionar los roles de equipo. No hay una ruta que permita crear o asignar el rol Developer desde una invitación.

## Cuentas históricas Admin

No se elevan automáticamente. Para migrar una cuenta histórica, primero se debe autenticar al responsable, decidir explícitamente qué persona tendrá la cuenta Developer, configurar `InitialDeveloper:Email` y `InitialDeveloper:Password` de forma privada, iniciar la aplicación para crearla y luego acordar por separado el destino de cada cuenta antigua. No se deben reasignar credenciales compartidas ni limpiar el historial público sin respaldo y coordinación del equipo.
