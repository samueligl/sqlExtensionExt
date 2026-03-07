# POWERSQL - Expansor Inteligente de Asteriscos para SSMS 22

POWERSQL es una extensión de alta productividad (VSIX) diseñada específicamente para **SQL Server Management Studio 22 (64-bit)**.

Su objetivo principal es acelerar el desarrollo SQL evitando que el desarrollador tenga que escribir manualmente todas las columnas de una tabla. Intercepta la tecla `TAB` justo después de un asterisco `*` en una consulta `SELECT`, y lo reemplaza automáticamente por la lista completa de columnas de la tabla.

---

## 🚀 Flujo Principal de Trabajo (Instancia Real)

Para asegurar que la extensión funcione perfectamente en la instancia principal de SSMS 22 que usas a diario, sigue **estrictamente** este orden.

### 1. Limpiar Instalación Anterior (Evita error "Ya está instalada")
Si instalaste previamente la extensión, el VSIX Installer puede bloquearse por cachés o residuos.
Hemos creado el script `Clean-SsmsExtensions.ps1` en la raíz del proyecto para limpiar esto.

1. Abre PowerShell como Administrador.
2. Navega a la carpeta del proyecto.
3. Ejecuta: `.\Clean-SsmsExtensions.ps1`
   - *Este script cierra SSMS 22, elimina la extensión de las rutas de "Extensions" (Real y Experimental), y borra el `ComponentModelCache` para forzar a SSMS a re-escanear los componentes.*

### 2. Compilar
1. Abre `PowerSql.sln` en **Visual Studio 2022**.
2. Cambia la configuración a **Release**.
3. Haz clic en **Recompilar Solución** (Rebuild Solution).

### 3. Generar VSIX e Instalar (Instancia Real)
1. Navega a la carpeta de salida: `PowerSql\bin\Release\`.
2. Haz **doble clic** en `PowerSql.vsix`.
3. El VSIX Installer se abrirá. Selecciona *SQL Server Management Studio 22* e instala.
   - *Al haber limpiado la caché y las carpetas con el script, la instalación será 100% limpia.*

### 4. Probar en SSMS Real
1. Abre SSMS 22 de forma normal (desde tu menú de inicio).
2. Abre una ventana de "New Query".
3. Conéctate a cualquier base de datos (Ej: `master`).
4. Escribe: `SELECT * FROM dbo.spt_values`
5. Pon el cursor de texto inmediatamente después del `*` y presiona `TAB`.
6. Observa cómo el `*` se expande a la lista de columnas de forma asíncrona (sin congelar el UI).

---

## 🛠 Instancia Real vs Experimental (Modo Debug)

### Instancia Real (El objetivo)
- Es la versión normal de SSMS 22.
- Las extensiones se instalan globalmente en `C:\Program Files\...` o localmente en `%LocalAppData%\Microsoft\SSMS\22.0_...\Extensions`.
- Se requiere el doble clic en el archivo `.vsix`.

### Instancia Experimental (Auxiliar de Desarrollo)
- Si presionas `F5` en Visual Studio, se lanza `ssms.exe` con el argumento `/rootsuffix Exp`.
- Esto levanta un SSMS paralelo y **completamente vacío** de tus configuraciones diarias, usado solo para probar que el código no explota antes de hacer el VSIX.
- **Limitación Real:** Para evitar la confusión de versiones y cachés cruzados, prioriza siempre compilar en Release e instalar en tu instancia real con el script de limpieza cuando quieras probar el comportamiento funcional.

---

## ⚠️ Limitaciones y Cuándo Podría Fallar
- **Fallo al obtener la conexión (Timeout / No parsea):** El método actual lee el título (`Caption`) de la pestaña del editor para deducir el Servidor y Base de datos (`EnvDTE.ActiveWindow.Caption`). Si alteras las opciones de SSMS para ocultar la base de datos de las pestañas, la extensión fallará porque no podrá construir el `ConnectionString`. Si falla, imprimirá un mensaje de "Error al obtener columnas" en línea.
- **Versiones VSIX Congeladas:** Si no usas el script `Clean-SsmsExtensions.ps1` y quieres actualizar, *debes* acordarte de aumentar manualmente el campo `Version` en el archivo `source.extension.vsixmanifest`.
