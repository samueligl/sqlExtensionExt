using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace PowerSql.Services
{
    public static class SsmsConnectionService
    {
        public static async System.Threading.Tasks.Task<string> GetFormattedColumnsAsync(string fullTableName)
        {
            try
            {
                fullTableName = fullTableName.Replace("[", "").Replace("]", "");
                var parts = fullTableName.Split('.');
                string tableName = parts[parts.Length - 1];
                string schemaName = parts.Length > 1 ? parts[parts.Length - 2] : "dbo";

                Logger.Log($"Looking up columns for Table: '{tableName}', Schema: '{schemaName}'");

                // Obtenemos el string de conexión sincrónicamente desde el UI Thread
                string connectionString = string.Empty;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                connectionString = GetActiveConnectionStringFromDte();

                // Pasamos a un hilo de background (ThreadPool) para no bloquear la UI durante el I/O de red/BD
                await System.Threading.Tasks.TaskScheduler.Default;

                if (string.IsNullOrEmpty(connectionString))
                {
                    Logger.Log("Failed to determine connection context from VS SDK EnvDTE.");
                    return "-- Error: No se pudo determinar la conexión activa. Asegúrate de estar conectado en la ventana de consulta actual. --";
                }

                List<string> columns = new List<string>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    Logger.Log($"Opening DB connection asynchronously...");
                    await conn.OpenAsync();
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

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
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
                        IntegratedSecurity = true,
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
