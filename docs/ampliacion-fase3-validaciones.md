# Fase 3 ampliada: validación y errores de formularios

12 de octubre de 2026. Se conservan las validaciones administrativas, de importes, rutas y consultas de la revisión previa; se revisaron EndpointValidationFilter, los modelos de cuenta, ProductQueryValidator y Configuración.

## Correcciones

- Login limita la contraseña a 256 caracteres y la URL de retorno a 2048. Registro aplica también el límite de retorno. Los límites de contraseña no alteran las credenciales almacenadas; registro y nueva contraseña ya limitaban la longitud a 100.
- Recuperación valida el formato de correo antes de consultar Identity.
- Configuración conserva los valores enviados y muestra errores asociados a cada campo y en el resumen. Valida todo antes de empezar a guardar; una petición inválida no guarda ninguno de sus valores. No se cambia la atomicidad de operaciones válidas ante un fallo de persistencia a mitad del guardado.

DataAnnotations se aplica en servidor y expone metadatos Razor para los formularios que usan sus tag helpers. Las vistas de cuenta conservan su parcial de validación cliente. No se declara cobertura exhaustiva de todas las combinaciones de endpoints ni de todas las reglas cruzadas en navegador.

## Regresiones

AccountValidationTests incluye cinco casos: límites exactos y exceso de longitud, y correo mal formado. Antes del cambio tres fallaban y dos pasaban. Tras el cambio la suite completa de ese momento aprobó 142 casos.

InvalidSettingsKeepInputAndShowFieldErrors falló antes del arreglo porque recibía una redirección. Comprueba que la respuesta conserve el importe enviado y muestre un error ligado al campo. ShippingSettingsRejectFractionalCentsWithoutSaving comprueba además que el dato inválido no se persiste.

Comandos: `dotnet build Bisuteria.slnx -c Release` y `dotnet test Bisuteria.slnx -c Release --no-build --logger 'trx;LogFileName=ampliacion-fase3.trx' --results-directory audit/test-results`. Se utiliza Release porque la vista previa abierta por el usuario mantiene ocupadas las DLL Debug. Solo se escribe en bases temporales BisuteriaAudit_GUID; no hay migraciones.

## Archivos modificados

Resultado final: **143 pruebas aprobadas, 0 fallidas y 0 omitidas**, 41 segundos de ejecución. Compilación Release final con **0 errores y 0 advertencias**. `git diff --check` sin errores.

- src/Bisuteria.Web/Models/AccountViewModels.cs
- src/Bisuteria.Web/Areas/Admin/Controllers/SettingsController.cs
- src/Bisuteria.Web/Areas/Admin/Views/Settings/Index.cshtml
- tests/Bisuteria.Tests/AccountValidationTests.cs
- tests/Bisuteria.Tests/ValidationBoundaryTests.cs
- Este informe.

## Defensa oral

La validación del navegador mejora la experiencia, pero el servidor debe rechazar entradas inválidas aunque el usuario manipule la petición. Las pruebas de límites verifican tanto el último valor permitido como el primero rechazado. Conservar el formulario evita perder trabajo y permite corregir el campo indicado.
