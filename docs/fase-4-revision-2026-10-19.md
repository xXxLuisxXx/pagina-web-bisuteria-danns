# Fase 4 — defectos de pedidos y edición de productos

## Correcciones

- Las reglas de transición ahora rechazan estados de pedido o pago fuera de los enums declarados. Antes, una orden con estado de pago corrupto podía permitir una transición; y un estado de orden desconocido podía permitir actualizar el pago.
- La edición de productos ignora entradas de imágenes nulas, vacías o con espacios. El error reportado al guardar el producto ocurría porque la lista opcional se recorría sin comprobar que existiera. El controlador también filtra esos valores antes de unirlos con las imágenes cargadas.

## Verificación

`dotnet build Bisuteria.slnx -c Release`: 0 advertencias, 0 errores. `dotnet test Bisuteria.slnx -c Release --no-build`: **192 aprobadas, 0 fallidas**. Las pruebas adicionales cubren enums no definidos y una edición sin imágenes nuevas. La suite usa bases `BisuteriaAudit_<GUID>` y correo sustituido.

El resultado de pruebas está en `audit/test-results/fase4-cierre-20261019.trx` (ignorado por Git). No se ejecutaron pruebas contra una base de datos real.
