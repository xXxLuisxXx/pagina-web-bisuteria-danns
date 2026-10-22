# Guía permanente del proyecto

## Arquitectura y convenciones
- Solución ASP.NET Core MVC sobre .NET 10, con capas `Bisuteria.Domain`, `Bisuteria.Application`, `Bisuteria.Infrastructure` y `Bisuteria.Web`.
- Respeta la arquitectura, el estilo y las dependencias actuales. No agregues paquetes sin justificar por escrito su necesidad.
- Nombres de código en inglés; comentarios, documentación y commits en español.
- Mantén los colores, el logo, su tamaño, ubicación y tipografías actuales de la tienda pública. En el panel conserva la apariencia existente salvo cambios de navegación solicitados.

## Seguridad y datos
- Nunca agregues secretos, contraseñas, tokens ni credenciales al repositorio. Usa user-secrets en desarrollo y variables de entorno en despliegue.
- Este repositorio es público: no agregues datos reales del negocio (cuentas bancarias, teléfonos, WhatsApp, redes, correos ni titulares) en código, pruebas, documentación o commits. Adminístralos en `SiteSetting`.
- No ejecutes pruebas contra la base del negocio. Las pruebas deben usar exclusivamente bases temporales `BisuteriaAudit_<GUID>` y datos ficticios.
- Las migraciones deben ser aditivas. No elimines columnas ni datos existentes. No borres la entidad `Address` sin autorización del dueño del proyecto.
- Valida los datos en servidor, protege formularios con antiforgery y aplica autorización por permisos, límites de peticiones y auditoría a las funciones administrativas.

## Verificación local
Desde la raíz del repositorio:

```powershell
dotnet restore Bisuteria.slnx
dotnet build Bisuteria.slnx -c Release
dotnet test Bisuteria.slnx -c Release
node --check tests/visual/browser-audit.cjs
```

Para ejecutar el sitio local:

```powershell
dotnet run --project src/Bisuteria.Web --launch-profile https
```

La auditoría visual completa requiere la base de prueba aislada, Playwright/axe/Lighthouse ya instalados y `BROWSER_AUDIT=1`; no la apuntes a una instancia con datos reales. Los informes y capturas se generan bajo `audit/browser/`.

## Trabajo y Git
- Trabaja en una rama de tarea derivada de `main`; conserva los cambios del usuario y revisa `git status` antes de modificar archivos.
- Usa commits pequeños por fase, con mensajes descriptivos en español.
- No uses `git push --force`, no alteres esquemas de producción y no borres datos sin autorización explícita.
- Documenta resultados reales, límites de comprobación y decisiones que siguen pendientes; no declares certificaciones o terminación total sin evidencia.
