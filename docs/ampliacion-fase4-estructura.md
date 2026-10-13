# Fase 4: revalidación de estructura

Se revisaron CartPreparationService, las consultas administrativas paginadas, UnitOfWork, los controles de aislamiento SQL, health checks y el pipeline. Se retiró del constructor de AccountController la dependencia IEmailSender sin uso: el controlador ya delega recuperación y confirmación en la cola. No se añadieron dependencias ni se eliminó Address.

Comandos reales: `dotnet build Bisuteria.slnx -c Release` (0 errores, 0 advertencias), `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=ampliacion-fase4.trx' --results-directory audit/test-results` (143 aprobadas, 0 fallidas, 0 omitidas, 38 s). Se reutilizan las regresiones de estructura; no se añade una prueba que refleje solamente la eliminación de un campo privado.

Se conserva el límite documentado del índice Order.UserId: la columna histórica nvarchar(max) no permite indexación directa y no se estrecha ni altera la base real. CreatedAt y Status/CreatedAt tienen índices aditivos existentes. Continúan pendientes mediciones de carga y paginación/streaming de exportaciones grandes. El workflow Linux previo fue aprobado; no se ha ejecutado un nuevo workflow remoto para estos commits locales.

Archivo de código modificado: src/Bisuteria.Web/Controllers/AccountController.cs. Evidencia local: audit/test-results/ampliacion-fase4.trx. La fase de estructura se revalida con estos límites, no se declara rendimiento productivo certificado.
