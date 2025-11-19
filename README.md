# ❗ Komentarze i opis w języku polskim, zgodnie ze schematem w Program.cs

# DbMetaTool

Aplikacja konsolowa w .NET 8.0 służąca do generowania i wykonywania skryptów metadanych
z bazy danych **Firebird 5.0**. Obsługiwane są tylko:
- domeny,
- tabele (z polami),
- procedury.

Pozostałe obiekty (indeksy, triggery, constraints) są pominięte.

---

## 📦 Wymagania
- .NET 8.0 SDK
- Firebird 5.0 (serwer bazodanowy)
- IBExpert (opcjonalnie, do ręcznej weryfikacji)

---

## ⚙️ Instalacja
1. Sklonuj repozytorium:
   ```bash
   git clone https://github.com/<twoje-repo>/DbMetaTool.git
   cd DbMetaTool
2. Zbuduj projekt:
    ```bash
    dotnet build

---

▶️ Użycie
1. Budowa nowej bazy danych ze skryptów
    ```bash
    dotnet run -- build-db --db-dir "/ścieżka/do/katalogu/bazy" --scripts-dir "/ścieżka/do/skryptów"

Tworzy pustą bazę Firebird i wykonuje skrypty SQL (domeny, tabele, procedury).
2. Eksport metadanych z istniejącej bazy
    ```bash
    dotnet run -- export-scripts --connection-string "User=SYSDBA;Password=masterkey;Database=/ścieżka/do/database.fdb;DataSource=localhost;Port=3050;Dialect=3;" --output-dir "/ścieżka/do/output"
Generuje pliki:
domains.sql
tables.sql
procedures.sql
3. Aktualizacja istniejącej bazy na podstawie skryptów
    ```bash
    dotnet run -- update-db --connection-string "User=SYSDBA;Password=masterkey;Database=/ścieżka/do/database.fdb;DataSource=localhost;Port=3050;Dialect=3;" --scripts-dir "/ścieżka/do/skryptów"
Wykonuje skrypty w poprawnej kolejności (domeny → tabele → procedury).

---

ℹ️ Uwagi
Obsługiwane są tylko domeny, tabele i procedury.
Błędy wykonywania skryptów zapisywane są w pliku error.log.
Connection string musi być podany w prostych cudzysłowach " " lub ' ' (nie typograficznych „”).