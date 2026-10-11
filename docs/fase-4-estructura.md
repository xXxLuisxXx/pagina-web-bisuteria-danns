# Fase 4: estructura y ejecución reproducible

CartPreparationService concentra cantidad acumulada, disponibilidad, validación de personalización y recálculo de precio. CartController conserva la orquestación HTTP/sesión. Se conserva Address; no se eliminan datos históricos.

Usuarios se consultan por páginas estables de 25 filas, con roles obtenidos en una consulta conjunta y agregados de pedidos limitados a los usuarios de la página. El cambio de estado consulta el usuario por ID independientemente de la página visible. El top de productos del dashboard obtiene los detalles en lote. Los reportes proyectan columnas necesarias y suman unidades en SQL, sin cargar todas las entidades OrderItem.

QueryIndexes agrega exclusivamente índices CreatedAt y (Status, CreatedAt). El índice UserId se difiere: SQL Server no admite nvarchar(max) como clave y EF proponía estrechar la columna a 450 caracteres, contrario a la restricción de migración aditiva. Esa alteración se retiró del código generado; no se aplicó a bases reales.

Se agregó /health con disponibilidad SQL y respuesta genérica, y logging JSON configurable. El manejador global de errores y filtros de persistencia siguen vigentes. Las pruebas aceptan BISUTERIA_TEST_CONNECTION y siempre sustituyen InitialCatalog por un nombre temporal validado. El workflow de GitHub prepara un contenedor SQL desechable, genera credenciales aleatorias, compila, prueba y analiza paquetes; falla si encuentra vulnerabilidades.

README actualizado: precio cobrado, pruebas portables, claves, proxy y despliegue. El informe antiguo se identifica como histórico y se retiran sus afirmaciones de certificación. Referencias de configuración: [SQL Server en Docker](https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker) y [servicios de GitHub Actions](https://docs.github.com/en/actions/tutorials/use-containerized-services).

Límites: el workflow necesita una ejecución remota para validar Linux/Docker; una ejecución local Windows no lo acredita. El índice UserId permanece pendiente de una migración acordada. Los reportes detallados aún retornan listas completas de filas y necesitan paginación/streaming si el volumen supera el de este proyecto. No se hizo una prueba de carga de producción.
`dotnet build Bisuteria.slnx -c Release`: 0 errores y 0 advertencias. `dotnet test Bisuteria.slnx -c Release --no-build`: 129/129 aprobadas, 0 omitidas; TRX local audit/test-results/fase4.trx. La primera prueba de precio consultaba un DTO que omite ese valor; se corrigió para inspeccionar la entidad persistida.
