using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool.Infrastructure
{
    /// <summary>
    /// Klasa pomocnicza do obsługi skryptów SQL
    /// </summary>
    public class SqlScriptRunner
    {
        private readonly FbConnection _connection;
        private readonly string _errorLogPath;

        public SqlScriptRunner(FbConnection connection, string errorLogPath)
        {
            _connection = connection;
            _errorLogPath = errorLogPath;
        }

        public void ExecuteScripts(IEnumerable<SqlScriptFile> scriptFiles)
        {
            foreach (var file in scriptFiles)
            {
                if (file.Type == ScriptType.Domain)
                {
                    // Domeny – każda komenda osobno
                    var domainCommands = file.Content
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    int num = 1;
                    foreach (var cmdText in domainCommands)
                    {
                        string trimmed = cmdText.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed))
                            continue;

                        using var cmd = new FbCommand(trimmed, _connection);
                        try
                        {
                            cmd.ExecuteNonQuery();
                            Console.WriteLine($"Wykonano domenę {num} z pliku: {Path.GetFileName(file.FilePath)}");
                        }
                        catch (Exception ex)
                        {
                            string msg = $"Błąd w domenie {num} pliku {Path.GetFileName(file.FilePath)}: {ex.Message}";
                            Console.WriteLine(msg);
                            File.AppendAllText(_errorLogPath, msg + Environment.NewLine);
                        }
                        num++;
                    }
                }
                else
                {
                    // Tabele i procedury – cały plik jako jeden blok
                    using var cmd = new FbCommand(file.Content, _connection);
                    try
                    {
                        cmd.ExecuteNonQuery();
                        Console.WriteLine($"Wykonano plik: {Path.GetFileName(file.FilePath)}");
                    }
                    catch (Exception ex)
                    {
                        string msg = $"Błąd w pliku {Path.GetFileName(file.FilePath)}: {ex.Message}";
                        Console.WriteLine(msg);
                        File.AppendAllText(_errorLogPath, msg + Environment.NewLine);
                    }
                }
            }
        }
    }
}
