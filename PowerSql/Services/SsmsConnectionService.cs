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

                Logger.Log($"Looking up columns for Table: '{tableName}', Schema: '{schemaName}'");

                // Obtenemos la conexión usando la API pública DTE
                string connectionString = GetActiveConnectionStringFromDte();

                if (string.IsNullOrEmpty(connectionString))
                {
                    Logger.Log("Failed to determine connection context from VS SDK EnvDTE.");
                    return "-- Error: No se pudo determinar la conexión activa. Asegúrate de estar conectado en la ventana de consulta actual. --";
                }

                List<string> columns = new List<string>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    Logger.Log($"Opening DB connection...");
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

                Logger.Log($"Found {columns.Count} columns.");

                if (columns.Count == 0)
                {
                    return null; // No se encontraron columnas
                }

                return "\n    " + string.Join(",\n    ", columns);
            }
            catch (Exception ex)
            {
                Logger.Log($"Exception retrieving metadata: {ex.Message}");
                return $"-- Error al obtener columnas: {ex.Message} --";
            }
        }

        private static string GetActiveConnectionStringFromDte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Logger.Log("Attempting to get active connection using EnvDTE...");
                // DTE es el objeto principal de automatización de Visual Studio / SSMS Shell
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null)
                {
                    Logger.Log("DTE service is null.");
                    return null;
                }

                if (dte.ActiveWindow == null)
                {
                    Logger.Log("ActiveWindow is null in DTE.");
                    return null;
                }

                string caption = dte.ActiveWindow.Caption;
                Logger.Log($"ActiveWindow.Caption: '{caption}'");

                // Fallback robusto 1: Formato "SQLQueryX.sql - Servidor.Base (User (51))" o similar.
                // Acepta nombres de servidor con puntos (ej: 192.168.1.1 o mssql.midominio.com)
                // Se busca el guion espacio, luego todo hasta el último punto como servidor,
                // luego la base de datos hasta el espacio antes del paréntesis.
                // Regex: -\s+(.+)\.([^\.\s]+)\s+\(

                var match = Regex.Match(caption, @"-\s+(.+)\.([^\.\s]+)\s+\(");

                if (match.Success)
                {
                    string serverName = match.Groups[1].Value;
                    string databaseName = match.Groups[2].Value;

                    Logger.Log($"Parsed Server: '{serverName}'");
                    Logger.Log($"Parsed Database: '{databaseName}'");

                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
                    {
                        DataSource = serverName,
                        InitialCatalog = databaseName,
                        IntegratedSecurity = true, // Asumimos Windows Auth por ser la alternativa pública sin contraseña
                        ApplicationName = "POWERSQL Extension"
                    };

                    string partialConnString = $"Server={serverName};Database={databaseName};Integrated Security=True";
                    Logger.Log($"Connection String derived (No passwords shown): {partialConnString}");
                    return builder.ConnectionString;
                }
                else
                {
                    Logger.Log("Regex match failed. ActiveWindow.Caption does not match expected SSMS query connection format.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing connection from DTE: {ex.Message}");
            }

            return null;
        }
    }
}
