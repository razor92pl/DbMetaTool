using DbMetaTool.Infrastructure;
using DbMetaTool.Interfaces;
using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za budowanie nowej bazy Firebird 5.0
    /// na podstawie skryptów SQL w katalogu.
    /// </summary>
    public class DatabaseBuilder : IDatabaseBuilder
    {
        public void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            if (!Directory.Exists(databaseDirectory))
                Directory.CreateDirectory(databaseDirectory);

            string databasePath = Path.Combine(databaseDirectory, "database.fdb");

            // Usuń istniejącą bazę (dla testów)
            if (File.Exists(databasePath))
                File.Delete(databasePath);

            // Domyślny connection string dla nowej bazy
            var csb = new FbConnectionStringBuilder
            {
                Database = databasePath,
                DataSource = "localhost",
                UserID = "SYSDBA",
                Password = "masterkey",
                Port = 3050,
                Dialect = 3,
                Charset = "UTF8"
            };

            // Utwórz pustą bazę
            FbConnection.CreateDatabase(csb.ToString());
            Console.WriteLine($"Baza utworzona: {databasePath}");

            // Plik log błędów
            string errorLogPath = Path.Combine(databaseDirectory, "error.log");
            if (File.Exists(errorLogPath))
                File.Delete(errorLogPath);

            // Pobierz wszystkie pliki .sql z katalogu
            var files = Directory.GetFiles(scriptsDirectory, "*.sql");

            // Stwórz listę obiektów SqlScriptFile – jeden obiekt na plik
            var scriptFiles = files.Select(f => new SqlScriptFile(f))
                                   .OrderBy(f => f.Type) // logika: domeny -> tabele -> procedury
                                   .ToList();

            using var conn = new FbConnection(csb.ToString());
            conn.Open();

            // Wykonaj wszystkie skrypty w kolejności logicznej
            var runner = new SqlScriptRunner(conn, errorLogPath);
            runner.ExecuteScripts(scriptFiles);

            conn.Close();

            Console.WriteLine("Wykonanie skryptów zakończone.");
            if (File.Exists(errorLogPath))
                Console.WriteLine($"Błędy zapisano w pliku: {errorLogPath}");
        } 
    }
}
