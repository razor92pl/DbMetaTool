namespace DbMetaTool.Interfaces
{
    /// <summary>
    /// Interfejs odpowiedzialny za tworzenie nowej bazy danych FireBird 5.0
    /// </summary>
    public interface IDatabaseBuilder
    {
        void BuildDatabase(string databaseDirectory, string scriptsDirectory);
    }
}

