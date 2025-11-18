namespace DbMetaTool.Interfaces
{
    /// <summary>
    /// Eksporter metadanych z istniejącej bazy Firebird 5.0 do plików SQL.
    /// </summary>
    public interface IMetadataExporter
    {
        void ExportScripts(string connectionString, string outputDirectory);
    }
}