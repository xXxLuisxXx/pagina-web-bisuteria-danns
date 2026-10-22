# Fases 2 y 3: roles, permisos e invitaciones

## Roles y autorización

`RolePermission` almacena la matriz de permisos y la migración `RolesAndPermissionMatrix` la crea y la inicializa de forma aditiva. La autorización consulta los permisos efectivos de los roles y se cachea por solicitud HTTP. Los controladores del área Admin usan políticas por permiso; el área Developer tiene layout independiente con etiqueta «Modo desarrollador». El menú administrativo se filtra por permiso y los accesos autenticados sin permiso reciben 403.

La pantalla Developer permite modificar permisos de Owner y Operator. Al guardar, actualiza la matriz y el security stamp de cada usuario afectado. El Developer inicial recibe todos los permisos; Owner recibe permisos comerciales y gestión de Operators; Operator recibe lectura de pedidos, cambios válidos de estado e inventario. Customer no recibe permisos administrativos. `Admin` queda como rol legado sin concesiones automáticas.

Owner puede invitar únicamente Operators. Developer puede invitar Owner u Operator. Los enlaces son aleatorios, de un solo uso, expiran a las 24 horas y solo se almacena el hash en `StaffInvitations`; el correo pendiente se entrega a través de `EmailOutbox`. El invitado configura su propia contraseña. Owner no requiere TOTP para entrar al panel (requisito retirado el 21 de octubre de 2026). El panel permite revocar invitaciones y activar/desactivar miembros con límites según el rol; una cuenta no puede desactivar al último Developer activo.

## Primer Developer

En desarrollo, configura las opciones privadas con `dotnet user-secrets`:

```powershell
dotnet user-secrets init --project src/Bisuteria.Web
dotnet user-secrets set "InitialDeveloper:Email" "TU_CORREO" --project src/Bisuteria.Web
dotnet user-secrets set "InitialDeveloper:Password" "UNA_CLAVE_TEMPORAL_SEGURA" --project src/Bisuteria.Web
```

En producción usa las variables `InitialDeveloper__Email` y `InitialDeveloper__Password` del proveedor de secretos; no las agregues al repositorio ni a comandos compartidos. El inicializador crea el usuario solo cuando no hay ningún Developer. En producción, si faltan ambas opciones y no existe un Developer, el arranque falla explícitamente. Después del primer uso, retira la clave temporal del proveedor de configuración.

El primer inicio exige cambiar esa clave. Developer y Owner no necesitan configurar TOTP. Si una de esas cuentas conserva MFA antiguo, el próximo inicio de sesión lo desactiva y limpia el indicador de enrolamiento. Los clientes aún pueden activar TOTP voluntariamente.

Las contraseñas requieren mínimo 12 caracteres, mayúscula, minúscula, número y símbolo; se rechazan palabras comunes y contraseñas que contengan el correo o nombre de usuario. El correo no incluye contraseñas. Los enlaces y claves nunca se escriben en logs.

La invitación de Owner es por correo y vence en 24 horas. La cuenta queda activada tras escoger contraseña y puede usar el panel al iniciar sesión. El segundo factor no es requisito para Owner.

## Reautenticación y bloqueo progresivo

Identity inicia el bloqueo tras cinco fallos. Cada nuevo bloqueo antes de un acceso completo correcto duplica el tiempo anterior (5, 10, 20 y 40 minutos), con un máximo de 60 minutos. El nivel queda guardado como claim de usuario y se reinicia después de un inicio de sesión completo exitoso; los códigos TOTP incorrectos también alimentan el contador de Identity.

Las operaciones administrativas sensibles y las exportaciones exigen sesión con menos de 15 minutos. Si vence ese plazo, la aplicación redirige al login y exige contraseña y, si está activado, TOTP. Las exportaciones GET vuelven a su destino después de autenticarse. Para un POST, se conserva de forma segura la página local del formulario; el usuario vuelve allí y debe repetir el envío. El sistema no guarda ni reproduce automáticamente el cuerpo del POST.
