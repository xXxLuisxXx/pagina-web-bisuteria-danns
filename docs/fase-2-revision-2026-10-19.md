# Fase 2 — límites de la matriz y cuentas privilegiadas

Se revisó la matriz existente en `RolePermission`, las políticas por permiso y la migración inicial de roles. No se convierten cuentas Admin a Developer. La propietaria confirmó que puede invitar solamente operadores. Confirmó también conservar el botón de WhatsApp; la API oficial no se implementará en esta revisión.

## Correcciones

- La edición de la matriz respeta un techo por rol: Owner conserva funciones comerciales; Operator conserva pedidos e inventario. Ninguno recibe permisos técnicos. Quien modifica la matriz solo puede conceder permisos que posee.
- El autorizador aplica el techo también al leer la base: una concesión técnica accidental a Owner no permite abrir el panel Developer. Los usuarios inactivos no obtienen permisos.
- Se centralizó el cambio de estado de cuentas en `AdminService`. Un bloqueo transaccional SQL por base serializa las desactivaciones, incluso entre instancias; el conteo de Developer activos se hace en SQL. Dos solicitudes concurrentes no pueden desactivar a los dos últimos Developer.
- La edición de clientes rechaza cuentas que combinen Customer con roles privilegiados también en GET.
- Se prepararon permisos separados para fijar envío, ver/revisar comprobantes, administrar clientes y revelar datos. La migración `OperationalPermissions` es aditiva y exclusivamente de datos; solo se aplicó mediante las bases temporales de pruebas. Sus endpoints de envíos/comprobantes corresponden a la fase 5.

## Matriz máxima

| Funciones | Developer | Owner | Operator | Customer |
| --- | --- | --- | --- | --- |
| Configuración técnica, matriz y auditoría global | Sí | No | No | No |
| Pedidos y estados válidos | Sí | Sí | Sí | Solo sus pedidos |
| Envíos, pagos y comprobantes administrativos | Sí | Sí | No | Solo sus comprobantes |
| Productos, contenidos, tienda y reportes | Sí | Sí | No | No |
| Ajustar inventario | Sí | Sí | Sí | No |
| Invitar Owner | Sí | No | No | No |
| Invitar Operator | Sí | Sí | No | No |

Los permisos efectivos pueden reducirse dentro del techo. Los permisos nuevos de comprobantes/envíos no se presentan como funcionalidad terminada antes de la fase 5.

## Evidencia

Regresiones: cuatro intentos de escalada de matriz; concesión técnica corrupta en base; desactivación concurrente de los dos últimos desarrolladores. Se mantienen las regresiones anteriores de invitación, caducidad, autorización directa y cierre de sesiones al modificar la matriz.

`dotnet build Bisuteria.slnx -c Release`: 0 errores y 0 advertencias. `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=fase2-20261019.trx' --results-directory audit/test-results`: 181 aprobadas, 0 fallidas, 0 omitidas, 1 minuto 37 segundos. TRX en `audit/test-results/fase2-20261019.trx`. El árbol conserva las modificaciones previas del usuario; las migraciones se probaron exclusivamente en `BisuteriaAudit_<GUID>`. Las seis regresiones nuevas se ejecutaron con la corrección; no se afirma una ejecución previa contra el código anterior.
