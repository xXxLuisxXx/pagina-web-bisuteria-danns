# Fase 3: validaciones — 10 de octubre de 2026

Resultado final: compilación Release con 0 errores y 0 advertencias; 126 pruebas aprobadas, 0 fallidas y 0 omitidas (32 s informados por el runner). Revisión de whitespace sin errores.

Se añadieron límites de búsqueda, páginas, ordenamientos, importes, enumeraciones y fechas antes de consultar datos. Los filtros inválidos devuelven 400 con errores de campo; los identificadores no positivos devuelven 404. Blog, productos, cupones y reseñas ya no anuncian éxito al intentar modificar un registro inexistente en las acciones corregidas.

Blog limita etiquetas y contenido; cupones y costos de envío rechazan fracciones de centavo. Los DTO de formularios administrativos incluyen reglas DataAnnotations para que Razor emita validación cliente de longitudes, obligatoriedad y rangos, además de FluentValidation en servidor. La configuración incluye controles numéricos con step 0.01, campos de correo/URL, límites de texto y selector de política de cupones. Los errores de confirmación de correo se muestran en español. Los precios de compra siguen calculándose en servidor.

Pruebas añadidas en ValidationBoundaryTests: parámetros inválidos por HTTP, rangos de fechas, recursos inexistentes, precisión del descuento, límites de blog, ordenamientos permitidos, metadatos de validación cliente y rechazo del costo de envío sin persistirlo. Las regresiones existentes cubren precio comparado, checkout, stock, personalizaciones y autorización.

Comandos: `dotnet build Bisuteria.slnx -c Release`, `dotnet test Bisuteria.slnx -c Release --no-build --logger "trx;LogFileName=fase3.trx" --results-directory audit/test-results`, `git diff --check`. Todas las escrituras de integración usan bases BisuteriaAudit_<GUID>.

Incidencias resueltas durante la verificación: uso de una extensión de diccionario incompatible en el nuevo filtro (sustituida por TryGetValue); interferencia del límite compartido de login entre casos (deshabilitado exclusivamente en Testing salvo la prueba que lo activa expresamente). Se mantienen los límites en desarrollo y producción.

Límites de evidencia: la suite prueba los casos indicados, no todas las combinaciones de cada endpoint; no se ejecutaron todos los casos contra el commit original. La presencia de reglas cliente se verifica en HTML; la revisión visual completa pertenece a la comprobación final del dashboard. La validación condicional del negocio en servidor sigue siendo la autoridad incluso cuando el navegador no reproduce una regla cruzada.
