# POWERSQL - Expansor Inteligente de Asteriscos para SSMS 22

POWERSQL es una extensión de alta productividad (VSIX) diseñada específicamente para **SQL Server Management Studio 22 (64-bit)**.

Su objetivo principal es acelerar el desarrollo SQL evitando que el desarrollador tenga que escribir manualmente todas las columnas de una tabla. Intercepta la tecla `TAB` justo después de un asterisco `*` en una consulta `SELECT`, y lo reemplaza automáticamente por la lista completa de columnas de la tabla.

---

## 🚀 Cómo Funciona (UX)

El flujo esperado de la extensión es el siguiente:

1. El usuario escribe una consulta en el editor de SSMS:
   ```sql
   SELECT * FROM LORENA.dbo.ARTICULOS
   ```
2. El cursor queda justo después del asterisco `*`.
3. El usuario presiona la tecla **TAB**.
4. La extensión detecta el `*`, consulta la base de datos conectada en esa ventana, y reemplaza automáticamente el asterisco por la lista de columnas:
   ```sql
   SELECT
       [CODARTICULO],
       [DESCRIPCION],
       [TIPOIMPUESTO],
       [DPTO],
       ...
   FROM LORENA.dbo.ARTICULOS
   ```

---

## 🛠 Requisitos Previos

Para clonar, compilar y modificar esta extensión, necesitas:

1. **SQL Server Management Studio 22** instalado (para pruebas).
2. **Visual Studio 2022** (Community, Professional o Enterprise).
3. **Cargas de trabajo de Visual Studio (Workloads)**:
   - Desarrollo de extensiones de Visual Studio (Visual Studio extension development).
   - Desarrollo de escritorio de .NET (para tener las herramientas C# y WPF básicas).
4. Asegúrate de que el componente **Visual Studio SDK** esté instalado.

---

## 📥 Cómo Clonar y Configurar el Proyecto

1. **Clona el repositorio:**
   ```bash
   git clone https://github.com/TU_USUARIO/POWERSQL.git
   cd POWERSQL/PowerSql
   ```

2. **Abre la solución en Visual Studio 2022:**
   - Abre el archivo `PowerSql.csproj` con Visual Studio 2022 (o simplemente la carpeta del proyecto).
   - ¡Listo! El proyecto restaurará automáticamente las dependencias públicas mediante NuGet (`Microsoft.VisualStudio.SDK`) y las referencias del sistema (`System.ComponentModel.Composition`, `System.Data`).
   - **Nota importante:** A diferencia de proyectos antiguos, esta versión 100% pública de POWERSQL **NO requiere** que copies DLLs privadas o internas de la instalación de SSMS. El proyecto compilará "Out of the box".

---

## ⚙️ Cómo Compilar y Depurar (Debug)

1. En Visual Studio 2022, establece la configuración de compilación en **Debug**.
2. Haz clic derecho en el proyecto `PowerSql` -> **Propiedades**.
3. Ve a la pestaña **Depurar** (Debug).
4. Selecciona **Iniciar programa externo** (Start external program) e ingresa la ruta del ejecutable de SSMS 22:
   `C:\Program Files\Microsoft SQL Server Management Studio 22\Common7\IDE\ssms.exe`
5. Opcional: En "Argumentos de la línea de comandos", puedes agregar `/log` para generar un archivo de registro de actividad.
6. Presiona **F5**.
   - Visual Studio compilará la extensión.
   - La instalará en la "Instancia Experimental" de SSMS.
   - Abrirá SSMS adjuntando el depurador para que puedas poner puntos de interrupción (breakpoints) en el código.

---

## 📦 Cómo Generar el Instalador VSIX

1. En Visual Studio, cambia la configuración de compilación de `Debug` a **Release**.
2. Ve al menú superior y selecciona **Compilar** -> **Recompilar solución** (Rebuild Solution).
3. Navega a la carpeta de salida: `PowerSql\bin\Release\`.
4. Encontrarás el archivo **`PowerSql.vsix`**. ¡Este es el instalador final de tu extensión!

---

## 🔌 Cómo Instalar la Extensión en SSMS 22

1. Asegúrate de que todas las ventanas de SQL Server Management Studio 22 estén cerradas.
2. Haz doble clic en el archivo `PowerSql.vsix` que generaste en el paso anterior.
3. El "VSIX Installer" de Microsoft se abrirá.
4. Mostrará `SQL Server Management Studio` en la lista de productos compatibles. Haz clic en **Install**.
5. Espera a que termine la instalación y abre SSMS 22.
6. Abre una nueva ventana de Query, conéctate a una base de datos, escribe `SELECT * FROM tabla` y presiona la tecla `TAB`.

### Actualizaciones
Para actualizar la extensión, simplemente abre `source.extension.vsixmanifest` en Visual Studio, incrementa la versión (por ejemplo, de `1.0` a `1.1`), recompila en Release y ejecuta el nuevo VSIX. El instalador reemplazará la versión anterior automáticamente.

---

## 📝 Arquitectura Técnica (SSMS 22 - 64 bits)

POWERSQL utiliza el Isolated Shell de Visual Studio 2022 en el cual se basa SSMS 22. Toda la arquitectura está diseñada utilizando **únicamente APIs públicas** mantenibles.

- **VSIX Manifest:** Apunta explícitamente a `Microsoft.VisualStudio.Ssms` (Versión `[22.0, 23.0)`) y especifica `<ProductArchitecture>amd64</ProductArchitecture>`.
- **MEF (`SsmsEditorListener.cs`):** Utiliza `IWpfTextViewCreationListener` para inyectarse silenciosamente al crear un editor SQL (`"SQL Server Tools"`).
- **Interceptor (`CommandFilter.cs`):** Implementa `IOleCommandTarget` para interceptar la pulsación del tabulador (`VSStd2KCmdID.TAB`) y manejar el reemplazo del texto en el `ITextBuffer`.
- **Conexión Activa (`SsmsConnectionService.cs`):** En lugar de depender de DLLs privadas y propensas a romperse (como `SQLEditors.dll` o `Microsoft.SqlServer.Management.UI.VSIntegration`), el servicio utiliza **EnvDTE** (`Package.GetGlobalService(typeof(DTE))`). La extensión parsea el título de la ventana activa (`Caption`) para extraer de forma segura el Servidor y la Base de Datos actuales, y construye dinámicamente el `ConnectionString` utilizando Autenticación de Windows (`IntegratedSecurity=true`) y `System.Data.SqlClient`.
