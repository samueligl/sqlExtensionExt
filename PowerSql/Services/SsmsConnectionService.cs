using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace PowerSql.Services
{
    public static class SsmsConnectionService
    {
        /// <summary>
        /// Obtiene la lista de columnas formateada para una tabla dada.
        /// Analiza el entorno de VS (DTE) para obtener el string de conexión de la ventana activa.
        /// </summary>
        public static string GetFormattedColumns(string fullTableName)
        {
            try
            {
                fullTableName = fullTableName.Replace("[", "").Replace("]", "");
                var parts = fullTableName.Split('.');
                string tableName = parts[parts.Length - 1];
                string schemaName = parts.Length > 1 ? parts[parts.Length - 2] : "dbo";

                // Obtenemos la conexión usando la API pública DTE
                string connectionString = GetActiveConnectionStringFromDte();

                if (string.IsNullOrEmpty(connectionString))
                {
                    return "-- Error: No se pudo determinar la conexión activa. Asegúrate de estar conectado en la ventana de consulta actual. --";
                }

                List<string> columns = new List<string>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT c.name
                        FROM sys.columns c
                        INNER JOIN sys.objects o ON c.object_id = o.object_id
                        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                        WHERE o.name = @tableName AND s.name = @schemaName
                        ORDER BY c.column_id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@tableName", tableName);
                        cmd.Parameters.AddWithValue("@schemaName", schemaName);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                columns.Add($"[{reader.GetString(0)}]");
                            }
                        }
                    }
                }

                if (columns.Count == 0)
                {
                    return null; // No se encontraron columnas
                }

                return "\n    " + string.Join(",\n    ", columns);
            }
            catch (Exception ex)
            {
                return $"-- Error al obtener columnas: {ex.Message} --";
            }
        }

        private static string GetActiveConnectionStringFromDte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // DTE es el objeto principal de automatización de Visual Studio / SSMS Shell
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null || dte.ActiveWindow == null)
                {
                    return null;
                }

                // En SSMS, el Caption de la ventana de Query usualmente tiene el formato:
                // SQLQuery1.sql - Servidor.BaseDeDatos (Usuario (SPID))
                string caption = dte.ActiveWindow.Caption;

                // Parseamos el caption para extraer servidor y base de datos
                // Regex para buscar el patrón: Servidor.BaseDeDatos (Usuario (SPID))
                // Notar que esto puede variar por configuraciones de usuario en SSMS
                var match = Regex.Match(caption, @"-\s+([^\.]+)\.([^\s]+)\s+\(");

                if (match.Success)
                {
                    string serverName = match.Groups[1].Value;
                    string databaseName = match.Groups[2].Value;

                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
                    {
                        DataSource = serverName,
                        InitialCatalog = databaseName,
                        IntegratedSecurity = true, // Asumimos Windows Auth por ser la alternativa pública sin contraseña
                        ApplicationName = "POWERSQL Extension"
                    };

                    return builder.ConnectionString;
                }
            }
            catch
            {
                // Fallo silencioso si no podemos parsear la ventana
            }

            return null;
        }
    }
}
