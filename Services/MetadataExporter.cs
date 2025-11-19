using DbMetaTool.Interfaces;
using FirebirdSql.Data.FirebirdClient;
using System.Text;

namespace DbMetaTool.Services
{
    /// <summary>
    /// Eksporter metadanych z istniejącej bazy Firebird 5.0
    /// Generuje trzy pliki: domains.sql, tables.sql, procedures.sql
    /// Uwzględnia tylko domeny, tabele i procedury zdefiniowane przez użytkownika.
    /// </summary>
    public class MetadataExporter : IMetadataExporter
    {
        public void ExportScripts(string connectionString, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString nie może być puste.", nameof(connectionString));

            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("outputDirectory nie może być puste.", nameof(outputDirectory));

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string errorLogPath = Path.Combine(outputDirectory, "error.log");

            using var conn = new FbConnection(connectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                LogError($"Błąd połączenia z bazą: {ex.Message}", errorLogPath);
                return;
            }

            var domainsSb = new StringBuilder();
            var tablesSb = new StringBuilder();
            var procsSb = new StringBuilder();

            ExportDomains(conn, domainsSb, errorLogPath);
            ExportTables(conn, tablesSb, errorLogPath);
            ExportProcedures(conn, procsSb, errorLogPath);

            try
            {
                File.WriteAllText(Path.Combine(outputDirectory, "domains.sql"), domainsSb.ToString());
                File.WriteAllText(Path.Combine(outputDirectory, "tables.sql"), tablesSb.ToString());
                File.WriteAllText(Path.Combine(outputDirectory, "procedures.sql"), procsSb.ToString());
            }
            catch (Exception ex)
            {
                LogError($"Błąd zapisu plików eksportu: {ex.Message}", errorLogPath);
            }

            conn.Close();
            Console.WriteLine("Eksport metadanych zakończony.");
        }

        private void ExportDomains(FbConnection conn, StringBuilder sb, string errorLogPath)
        {
            const string sql = @"
                SELECT
                    rf.RDB$FIELD_NAME AS DOMAIN_NAME,
                    rf.RDB$FIELD_TYPE AS FIELD_TYPE,
                    rf.RDB$FIELD_LENGTH AS FIELD_LENGTH,
                    rf.RDB$CHARACTER_LENGTH AS CHAR_LEN,
                    rf.RDB$FIELD_PRECISION AS PREC,
                    rf.RDB$FIELD_SCALE AS SCALE,
                    rf.RDB$DEFAULT_SOURCE AS DEFAULT_SRC,
                    rf.RDB$VALIDATION_SOURCE AS CHECK_SRC
                FROM RDB$FIELDS rf
                WHERE COALESCE(rf.RDB$SYSTEM_FLAG, 0) = 0
                  AND rf.RDB$FIELD_NAME NOT LIKE 'RDB$%'
                  AND rf.RDB$FIELD_NAME NOT LIKE 'SEC$%'
                ORDER BY rf.RDB$FIELD_NAME";

            using var cmd = new FbCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                try
                {
                    string name = reader.GetString(reader.GetOrdinal("DOMAIN_NAME")).Trim();
                    int type = reader["FIELD_TYPE"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FIELD_TYPE"]);
                    int length = reader["FIELD_LENGTH"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FIELD_LENGTH"]);
                    int charLen = reader["CHAR_LEN"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CHAR_LEN"]);
                    int prec = reader["PREC"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PREC"]);
                    int scale = reader["SCALE"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SCALE"]);

                    string? defaultSrc = reader.IsDBNull(reader.GetOrdinal("DEFAULT_SRC"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("DEFAULT_SRC")).Trim();

                    string? checkSrc = reader.IsDBNull(reader.GetOrdinal("CHECK_SRC"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("CHECK_SRC")).Trim();

                    string typeName = MapFbType(type, length, charLen, prec, scale);

                    sb.AppendLine($"CREATE DOMAIN {name} AS {typeName};");
                    if (!string.IsNullOrWhiteSpace(defaultSrc))
                        sb.AppendLine(defaultSrc.TrimEnd(';') + ";");
                    if (!string.IsNullOrWhiteSpace(checkSrc))
                        sb.AppendLine(checkSrc.TrimEnd(';') + ";");
                    sb.AppendLine();

                    Console.WriteLine($"Wygenerowano domenę: {name}");
                }
                catch (Exception ex)
                {
                    LogError($"Błąd przy domenie: {ex.Message}", errorLogPath);
                }
            }
        }

        private void ExportTables(FbConnection conn, StringBuilder sb, string errorLogPath)
        {
            const string sqlTables = @"
                SELECT RDB$RELATION_NAME
                FROM RDB$RELATIONS
                WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0
                  AND RDB$VIEW_BLR IS NULL
                ORDER BY RDB$RELATION_NAME";

            using var cmdTables = new FbCommand(sqlTables, conn);
            using var readerTables = cmdTables.ExecuteReader();

            while (readerTables.Read())
            {
                try
                {
                    string tableName = readerTables.GetString(readerTables.GetOrdinal("RDB$RELATION_NAME")).Trim();
                    sb.AppendLine($"CREATE TABLE {tableName} (");

                    const string sqlCols = @"
                        SELECT
                            rf.RDB$FIELD_NAME AS COL_NAME,
                            f.RDB$FIELD_TYPE AS FIELD_TYPE,
                            f.RDB$CHARACTER_LENGTH AS CHAR_LEN,
                            f.RDB$FIELD_LENGTH AS FIELD_LENGTH,
                            f.RDB$FIELD_PRECISION AS PREC,
                            f.RDB$FIELD_SCALE AS SCALE,
                            rf.RDB$NULL_FLAG AS NULL_FLAG
                        FROM RDB$RELATION_FIELDS rf
                        JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE
                        WHERE rf.RDB$RELATION_NAME = @TABLE
                        ORDER BY rf.RDB$FIELD_POSITION";

                    using var colCmd = new FbCommand(sqlCols, conn);
                    colCmd.Parameters.AddWithValue("@TABLE", tableName);
                    using var colReader = colCmd.ExecuteReader();

                    var cols = new List<string>();
                    while (colReader.Read())
                    {
                        string colName = colReader.GetString(colReader.GetOrdinal("COL_NAME")).Trim();
                        int type = colReader["FIELD_TYPE"] == DBNull.Value ? 0 : Convert.ToInt32(colReader["FIELD_TYPE"]);
                        int length = colReader["FIELD_LENGTH"] == DBNull.Value ? 0 : Convert.ToInt32(colReader["FIELD_LENGTH"]);
                        int charLen = colReader["CHAR_LEN"] == DBNull.Value ? 0 : Convert.ToInt32(colReader["CHAR_LEN"]);
                        int prec = colReader["PREC"] == DBNull.Value ? 0 : Convert.ToInt32(colReader["PREC"]);
                        int scale = colReader["SCALE"] == DBNull.Value ? 0 : Convert.ToInt32(colReader["SCALE"]);
                        bool notNull = colReader["NULL_FLAG"] != DBNull.Value && Convert.ToInt32(colReader["NULL_FLAG"]) == 1;

                        string mappedType = MapFbType(type, length, charLen, prec, scale);
                        cols.Add($"    {colName} {mappedType}" + (notNull ? " NOT NULL" : ""));
                    }

                    sb.AppendLine(string.Join(",\n", cols));
                    sb.AppendLine(");");
                    sb.AppendLine();

                    Console.WriteLine($"Wygenerowano tabelę: {tableName}");
                }
                catch (Exception ex)
                {
                    LogError($"Błąd przy tabeli: {ex.Message}", errorLogPath);
                }
            }
        }

        private void ExportProcedures(FbConnection conn, StringBuilder sb, string errorLogPath)
        {
            // Pobieramy wszystkie procedury użytkownika
            const string sqlProcs = @"
                SELECT RDB$PROCEDURE_NAME
                FROM RDB$PROCEDURES
                WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0
                ORDER BY RDB$PROCEDURE_NAME";

            using var cmdProcs = new FbCommand(sqlProcs, conn);
            using var readerProcs = cmdProcs.ExecuteReader();

            while (readerProcs.Read())
            {
                try
                {
                    string procName = readerProcs.GetString(readerProcs.GetOrdinal("RDB$PROCEDURE_NAME")).Trim();

                    // Pobieramy parametry wyjściowe procedury
                    const string sqlParams = @"
                        SELECT RDB$PARAMETER_NAME, RDB$FIELD_SOURCE
                        FROM RDB$PROCEDURE_PARAMETERS
                        WHERE RDB$PROCEDURE_NAME = @PROC
                        AND RDB$PARAMETER_TYPE = 1 -- 1 = OUT
                        ORDER BY RDB$PARAMETER_NUMBER";

                    using var paramCmd = new FbCommand(sqlParams, conn);
                    paramCmd.Parameters.AddWithValue("@PROC", procName);

                    var outParams = new List<string>();
                    using var paramReader = paramCmd.ExecuteReader();
                    while (paramReader.Read())
                    {
                        string paramName = paramReader.GetString(paramReader.GetOrdinal("RDB$PARAMETER_NAME")).Trim();
                        string fieldSource = paramReader.GetString(paramReader.GetOrdinal("RDB$FIELD_SOURCE")).Trim();

                        // Mapowanie typu domeny/kolumny na typ SQL
                        string paramType = GetFieldType(conn, fieldSource, errorLogPath);
                        outParams.Add($"{paramName} {paramType}");
                    }

                    // Nagłówek procedury
                    sb.AppendLine($"CREATE PROCEDURE {procName}");
                    if (outParams.Any())
                    {
                        sb.AppendLine("RETURNS (");
                        sb.AppendLine("    " + string.Join(",\n    ", outParams));
                        sb.AppendLine(")");
                    }
                    sb.AppendLine("AS");

                    // Pobranie źródła procedury
                    string srcSql = @"
                        SELECT RDB$PROCEDURE_SOURCE
                        FROM RDB$PROCEDURES
                        WHERE RDB$PROCEDURE_NAME = @PROC";
                    using var srcCmd = new FbCommand(srcSql, conn);
                    srcCmd.Parameters.AddWithValue("@PROC", procName);

                    var srcObj = srcCmd.ExecuteScalar();
                    string? src = srcObj != DBNull.Value ? srcObj.ToString()!.Trim() : null;


                    sb.AppendLine("BEGIN");
                    if (!string.IsNullOrWhiteSpace(src))
                    {
                        // dodajemy wcięcia, żeby było czytelnie
                        foreach (var line in src.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            sb.AppendLine("    " + line.Trim());
                    }
                    sb.AppendLine("END;");
                    sb.AppendLine();

                    Console.WriteLine($"Wygenerowano procedurę: {procName}");
                }
                catch (Exception ex)
                {
                    LogError($"Błąd przy procedurze: {ex.Message}", errorLogPath);
                }
            }
        }

        /// <summary>
        /// Mapuje typ domeny/kolumny na SQL (np. INTEGER, VARCHAR(n), DECIMAL(p,s))
        /// </summary>
        private string GetFieldType(FbConnection conn, string fieldName, string errorLogPath)
        {
            const string sql = @"
                SELECT RF.RDB$FIELD_TYPE, RF.RDB$FIELD_LENGTH, RF.RDB$CHARACTER_LENGTH,
                    RF.RDB$FIELD_PRECISION, RF.RDB$FIELD_SCALE
                FROM RDB$FIELDS RF
                WHERE RF.RDB$FIELD_NAME = @FIELD";

            using var cmd = new FbCommand(sql, conn);
            cmd.Parameters.AddWithValue("@FIELD", fieldName);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError($"Nie znaleziono pola/domeny: {fieldName}", errorLogPath);
                return "UNKNOWN";
            }

            int type = Convert.ToInt32(reader["RDB$FIELD_TYPE"]);
            int length = reader["RDB$FIELD_LENGTH"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RDB$FIELD_LENGTH"]);
            int charLen = reader["RDB$CHARACTER_LENGTH"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RDB$CHARACTER_LENGTH"]);
            int prec = reader["RDB$FIELD_PRECISION"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RDB$FIELD_PRECISION"]);
            int scale = reader["RDB$FIELD_SCALE"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RDB$FIELD_SCALE"]);

            return MapFbType(type, length, charLen, prec, scale);
        }



        private static string MapFbType(int type, int length, int charLen, int prec, int scale)
        {
            return type switch
            {
                7 => "SMALLINT",
                8 => "INTEGER",
                16 => (scale < 0) ? $"DECIMAL({prec},{Math.Abs(scale)})" : "BIGINT",
                14 => $"CHAR({charLen})",
                37 => $"VARCHAR({charLen})",
                10 => "FLOAT",
                27 => "DOUBLE PRECISION",
                12 => "DATE",
                13 => "TIME",
                35 => "TIMESTAMP",
                261 => "BLOB",
                _ => $"UNKNOWN_TYPE_{type}"
            };
        }

        private void LogError(string message, string errorLogPath)
        {
            Console.WriteLine(message);
            try
            {
                File.AppendAllText(errorLogPath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
            }
            catch
            {
                // ignoruj błędy logowania
            }
        }
    }
}
