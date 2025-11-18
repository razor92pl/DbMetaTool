namespace DbMetaTool.Interfaces
{
    /// <summary>
    /// Interfejs odpowiedzialny za aktualizację istniejącej bazy Firebird 5.0
    /// na podstawie plików SQL.
    /// </summary>
    public interface IDatabaseUpdater
    {
        void UpdateDatabase(string connectionString, string scriptsDirectory);
    }
}
