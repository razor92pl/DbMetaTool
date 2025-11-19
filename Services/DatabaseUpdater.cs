using FirebirdSql.Data.FirebirdClient;
using DbMetaTool.Interfaces;
using DbMetaTool.Infrastructure;

namespace DbMetaTool.Services
{
    /// <summary>
    /// Implementacja aktualizacji bazy Firebird 5.0 na podstawie plików SQL.
    /// Wykorzystuje klasy SqlScriptFile i SqlScriptRunner.
    /// </summary>
    public class DatabaseUpdater : IDatabaseUpdater
    {
        public void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString nie może być puste.", nameof(connectionString));

            if (string.IsNullOrWhiteSpace(scriptsDirectory))
                throw new ArgumentException("scriptsDirectory nie może być puste.", nameof(scriptsDirectory));

            if (!Directory.Exists(scriptsDirectory))
                throw new DirectoryNotFoundException($"Katalog ze skryptami nie istnieje: {scriptsDirectory}");

            string errorLogPath = Path.Combine(scriptsDirectory, "error.log");

            // Pobierz wszystkie pliki SQL z katalogu, posortowane alfabetycznie
            var sqlFiles = Directory.GetFiles(scriptsDirectory, "*.sql")
                                    .OrderBy(f => f)
                                    .Select(f => new SqlScriptFile(f))
                                    .ToList();

            if (!sqlFiles.Any())
            {
                Console.WriteLine("Brak plików SQL w katalogu.");
                return;
            }

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

            var runner = new SqlScriptRunner(conn, errorLogPath);

            // Wykonanie wszystkich skryptów (domeny, tabele, procedury)
            runner.ExecuteScripts(sqlFiles);

            conn.Close();
            Console.WriteLine("Wykonanie skryptów zakończone.");
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
                // ignorujemy błędy logowania
            }
        }
    }
}
