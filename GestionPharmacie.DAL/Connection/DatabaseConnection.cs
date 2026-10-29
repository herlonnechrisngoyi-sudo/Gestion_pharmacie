using System.Data;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL.Connection;

/// <summary>
/// Gestionnaire centralisé et sécurisé des connexions à la base de données locale SQLite.
/// Garantit l'activation des clés étrangères, le mode WAL pour la performance,
/// et fournit des méthodes d'accès sécurisées (requêtes paramétrées exclusivement).
/// </summary>
public class DatabaseConnection
{
    private static readonly object LockObject = new();
    private static DatabaseConnection? _instance;
    private readonly string _connectionString;
    private readonly string _databaseFilePath;

    /// <summary>
    /// Instance singleton du gestionnaire de connexion.
    /// </summary>
    public static DatabaseConnection Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (LockObject)
                {
                    _instance ??= new DatabaseConnection();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initialise une nouvelle instance avec un chemin par défaut ou personnalisé.
    /// </summary>
    /// <param name="customDbPath">Chemin optionnel vers le fichier SQLite.</param>
    public DatabaseConnection(string? customDbPath = null)
    {
        if (string.IsNullOrWhiteSpace(customDbPath))
        {
            // Emplacement sécurisé par défaut dans le répertoire de l'application / dossier Data
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataDirectory = Path.Combine(baseDirectory, "Data");
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }
            _databaseFilePath = Path.Combine(dataDirectory, "pharmacie.db");
        }
        else
        {
            string? dir = Path.GetDirectoryName(customDbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _databaseFilePath = customDbPath;
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            DefaultTimeout = 30
        };

        _connectionString = builder.ToString();
    }

    /// <summary>
    /// Permet de reconfigurer l'instance singleton (ex: lors des tests ou configuration spécifique).
    /// </summary>
    public static void Initialize(string customDbPath)
    {
        lock (LockObject)
        {
            _instance = new DatabaseConnection(customDbPath);
        }
    }

    /// <summary>
    /// Chemin absolu du fichier de base de données SQLite.
    /// </summary>
    public string DatabaseFilePath => _databaseFilePath;

    /// <summary>
    /// Chaîne de connexion SQLite configurée.
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Crée et ouvre une nouvelle connexion SQLite sécurisée avec clés étrangères et mode WAL activés.
    /// </summary>
    public SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Sécurité et intégrité SQLite : Clés étrangères et optimisation WAL
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA busy_timeout = 5000;
            ";
            cmd.ExecuteNonQuery();
        }

        return connection;
    }

    /// <summary>
    /// Crée et ouvre une connexion de manière asynchrone.
    /// </summary>
    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA busy_timeout = 5000;
            ";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return connection;
    }

    /// <summary>
    /// Exécute une commande non-requête (INSERT, UPDATE, DELETE) de manière paramétrée et sécurisée.
    /// </summary>
    public int ExecuteNonQuery(string sql, Dictionary<string, object?>? parameters = null, SqliteTransaction? transaction = null)
    {
        if (transaction != null)
        {
            using var cmd = transaction.Connection!.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);
            return cmd.ExecuteNonQuery();
        }
        else
        {
            using var connection = CreateOpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);
            return cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Exécute une commande scalaire (COUNT, MAX, LAST_INSERT_ROWID) de manière paramétrée et sécurisée.
    /// </summary>
    public object? ExecuteScalar(string sql, Dictionary<string, object?>? parameters = null, SqliteTransaction? transaction = null)
    {
        if (transaction != null)
        {
            using var cmd = transaction.Connection!.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);
            return cmd.ExecuteScalar();
        }
        else
        {
            using var connection = CreateOpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);
            return cmd.ExecuteScalar();
        }
    }

    /// <summary>
    /// Exécute une série d'opérations dans une transaction ACID isolée.
    /// </summary>
    public void ExecuteInTransaction(Action<SqliteConnection, SqliteTransaction> action)
    {
        using var connection = CreateOpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            action(connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Exécute une série d'opérations dans une transaction avec valeur de retour.
    /// </summary>
    public T ExecuteInTransaction<T>(Func<SqliteConnection, SqliteTransaction, T> func)
    {
        using var connection = CreateOpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            T result = func(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Ajoute des paramètres typés à la commande SQLite pour prévenir les injections SQL.
    /// Gère convenablement les types 'decimal' et 'DateTime'.
    /// </summary>
    public static void AddParameters(SqliteCommand command, Dictionary<string, object?>? parameters)
    {
        if (parameters == null) return;

        foreach (var kvp in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = kvp.Key.StartsWith("@") ? kvp.Key : "@" + kvp.Key;
            
            if (kvp.Value == null)
            {
                parameter.Value = DBNull.Value;
            }
            else if (kvp.Value is decimal decVal)
            {
                // Enregistrement monétaire décimal haute précision au format texte ou numérique
                parameter.Value = decVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (kvp.Value is DateTime dtVal)
            {
                parameter.Value = dtVal.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else if (kvp.Value is bool bVal)
            {
                parameter.Value = bVal ? 1 : 0;
            }
            else if (kvp.Value.GetType().IsEnum)
            {
                parameter.Value = kvp.Value.ToString();
            }
            else
            {
                parameter.Value = kvp.Value;
            }

            command.Parameters.Add(parameter);
        }
    }
}
