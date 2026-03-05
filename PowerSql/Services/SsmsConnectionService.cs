using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using Microsoft.SqlServer.Management.UI.VSIntegration;
using Microsoft.SqlServer.Management.UI.VSIntegration.Editors;

namespace PowerSql.Services
{
    public static class SsmsConnectionService
    {
        /// <summary>
        /// Obtiene la lista de columnas formateada para una tabla dada,
        /// usando la conexión actual de la ventana activa de SSMS.
        /// </summary>
        public static string GetFormattedColumns(string fullTableName)
        {
            try
            {
                // Limpiar posibles corchetes del nombre
                fullTableName = fullTableName.Replace("[", "").Replace("]", "");
                var parts = fullTableName.Split('.');
                string tableName = parts[parts.Length - 1];
                string schemaName = parts.Length > 1 ? parts[parts.Length - 2] : "dbo";

                List<string> columns = new List<string>();

                // 1. INTENTO PRIMARIO: Obtener la "Live Connection" mediante Reflection profunda
                SqlConnection liveConnection = TryGetLiveSqlConnection();

                if (liveConnection != null && liveConnection.State == ConnectionState.Open)
                {
                    // ¡Éxito! Tenemos la conexión real de SSMS, sin importar si usó contraseña o no.
                    columns = ExecuteColumnQuery(liveConnection, schemaName, tableName);
                }
                else
                {
                    // 2. INTENTO SECUNDARIO (Fallback): Crear nueva conexión con Integrated Security
                    string fallbackConnectionString = GetFallbackConnectionString();
                    if (!string.IsNullOrEmpty(fallbackConnectionString))
                    {
                        using (SqlConnection fallbackConn = new SqlConnection(fallbackConnectionString))
                        {
                            fallbackConn.Open();
                            columns = ExecuteColumnQuery(fallbackConn, schemaName, tableName);
                        }
                    }
                }

                if (columns.Count == 0)
                {
                    return null; // No se encontraron columnas
                }

                // Formateamos el resultado: [Columna1], \n    [Columna2]...
                return "\n    " + string.Join(",\n    ", columns);
            }
            catch (Exception ex)
            {
                // En producción: Loguear ex.Message usando ActivityLog
                return $"-- Error al obtener columnas: {ex.Message} --";
            }
        }

        private static List<string> ExecuteColumnQuery(SqlConnection conn, string schemaName, string tableName)
        {
            List<string> columns = new List<string>();
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
            return columns;
        }

        private static SqlConnection TryGetLiveSqlConnection()
        {
            try
            {
                // Obtenemos la información de conexión de la ventana activa
                var activeWindowInfo = ServiceCache.ScriptFactory?.CurrentlyActiveWndConnectionInfo;
                if (activeWindowInfo == null) return null;

                // Reflection (Esto puede variar levemente dependiendo de la build de SSMS 22,
                // pero es el patrón estándar)

                // Intento 1: A través de Document.ExecutionConnection.SqlConnection
                var docProp = activeWindowInfo.GetType().GetProperty("Document", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (docProp != null)
                {
                    var doc = docProp.GetValue(activeWindowInfo);
                    if (doc != null)
                    {
                        var execConnProp = doc.GetType().GetProperty("ExecutionConnection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (execConnProp != null)
                        {
                            var execConn = execConnProp.GetValue(doc);
                            if (execConn != null)
                            {
                                var sqlConnProp = execConn.GetType().GetProperty("SqlConnection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                                if (sqlConnProp != null)
                                {
                                    return sqlConnProp.GetValue(execConn) as SqlConnection;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silencioso. Falló la Reflection.
            }
            return null;
        }

        private static string GetFallbackConnectionString()
        {
            try
            {
                UIConnectionInfo connectionInfo = ServiceCache.ScriptFactory?.CurrentlyActiveWndConnectionInfo?.UIConnectionInfo;
                if (connectionInfo != null)
                {
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
                    {
                        DataSource = connectionInfo.ServerName,
                        // Asumimos Integrated Security como Fallback si la Live Connection falla.
                        IntegratedSecurity = true,
                        ApplicationName = "POWERSQL Extension"
                    };

                    if (connectionInfo.AdvancedOptions.TryGetValue("DATABASE", out string databaseName))
                    {
                        builder.InitialCatalog = databaseName;
                    }

                    return builder.ConnectionString;
                }
            }
            catch
            {
                // Silencioso
            }
            return null;
        }
    }
}
