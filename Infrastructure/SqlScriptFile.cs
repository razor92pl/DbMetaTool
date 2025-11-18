namespace SenteRecruitmentTask.Infrastructure
{
    /// <summary>
    /// Klasa pomocnicza do obsługi skryptów SQL
    /// </summary>
    public class SqlScriptFile
    {
        public string FilePath { get; }
        public string Content { get; }
        public ScriptType Type { get; }

        public SqlScriptFile(string filePath)
        {
            FilePath = filePath;
            Content = File.ReadAllText(filePath).Trim();

            // Rozpoznawanie typu obiektu po pierwszych słowach
            if (Content.StartsWith("CREATE DOMAIN", StringComparison.OrdinalIgnoreCase))
                Type = ScriptType.Domain;
            else if (Content.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                Type = ScriptType.Table;
            else if (Content.StartsWith("CREATE PROCEDURE", StringComparison.OrdinalIgnoreCase))
                Type = ScriptType.Procedure;
            else
                Type = ScriptType.Unknown;
        }
    }

    public enum ScriptType
    {
        Domain,
        Table,
        Procedure,
        Unknown
    }
}
