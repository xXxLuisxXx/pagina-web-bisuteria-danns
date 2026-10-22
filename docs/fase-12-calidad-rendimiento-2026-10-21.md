# Fase 12 — estructura, rendimiento y calidad

## Cambios

- Se mantiene la preparación del carrito en `CartPreparationService`, fuera del controlador; las pruebas confirman que el precio nunca se toma del navegador.
- `AdminService.GetUsersAsync` agrega pedidos y totales por usuario en SQL, recupera roles por lote, pagina los resultados y filtra las cuentas únicas Customer/Operator en SQL para el Owner.
- Se añade el índice `Orders.UserId` y se limita la columna a 450 caracteres, el tamaño máximo de claves de usuario de Identity. La migración es aditiva a nivel de índice, no se aplicó a ninguna base real y forma parte de la migración temporal probada por la suite.
- El workflow de GitHub ejecuta restore, build, suite SQL temporal, `npm audit` y la auditoría de navegador con Chromium. Recoge los resultados como artefactos.
- README documenta SDK 10.0.402, SMTP local `.eml` privado, Node/Playwright y el comando reproducible de auditoría real.

## Resultados locales

- `dotnet build Bisuteria.slnx -c Release`: 0 advertencias y 0 errores.
- `dotnet test Bisuteria.slnx -c Release --no-build`: 234 aprobadas, 0 fallidas, 1 omitida (solo si no se habilita la auditoría real).
- `dotnet list Bisuteria.slnx package --vulnerable --include-transitive`: cinco proyectos sin paquetes vulnerables en los orígenes NuGet consultados.
- `npm audit --prefix tests/visual`: 0 vulnerabilidades.
- Prueba estructural `BusinessAccountFilterRunsBeforePaginationAndExcludesPrivilegedRoles`: aprobada contra SQL Server temporal.

La estrechez de `Orders.UserId` se apoya en que el valor solo puede proceder de Identity. Antes de desplegar, se debe verificar la longitud de datos existentes y aplicar la migración como paso controlado en staging.
