using System.Security.Authentication;
using MongoDB.Bson;
using MongoDB.Driver;
using server.Logging;
using server.Collections;
using server.Helpers;

namespace server.DBmgmt;

public class Database
{
    private readonly string connectionString;
    private readonly MongoClient client;
    public IMongoDatabase _database;
    public readonly Dictionary<string, Dictionary<string, string>> collectionEncryption;

    public Database(DBConfig config)
    {
        connectionString = ConnectionString.Create(config);
        client = new MongoClient(connectionString);
        _database = client.GetDatabase(config.Name);
        collectionEncryption = config.CollectionEncryption;
    }

    /// <summary>
    /// Runs a MongoDB command
    /// </summary>
    /// <param name="command">a valid mongo command</param>
    /// <returns>The result of the executed command</returns>
    public async Task<BsonDocument> RunCommand(BsonDocument command)
    {
        return await _database.RunCommandAsync<BsonDocument>(command);
    }

    /// <summary>
    /// Checks the connection to the remote database.
    /// </summary>
    /// <returns>true if connection is successful, false if not.</returns>
    public async Task<bool> CheckConnection()
    {
        var logger = new Logger();
        try
        {
            await RunCommand(new BsonDocument("ping", 1));
            return true;
        }
        catch (MongoAuthenticationException exception)
        {
            await logger.LogException(exception);
            await Console.Out.WriteLineAsync("Unable to authenticate, re-enter your credentials.");
            return false;
        }
        catch (MongoConfigurationException exception)
        {
            await logger.LogException(exception);
            await Console.Out.WriteLineAsync("The connection string is invalid.");
            return false;
        }
        catch (TimeoutException exception)
        {
            await logger.LogException(exception);
            await Console.Out.WriteLineAsync("A timeout has occurred.");
            return false;
        }
    }

    /// <summary>
    /// Checks if the Database matches the OpenCafe template.
    /// </summary>
    /// <returns>true if it does, false if not.</returns>
    public bool CheckForOpenCafe()
    {
        var areCollectionsPresent = new Dictionary<string, bool>()
        {
            { "customers", false }, { "admins", false },
            { "dishes", false }, { "images", false }, {"cards", false}
        };

        var collectionNames = _database.ListCollectionNames().ToList();
        foreach (var name in collectionNames)
        {
            if (areCollectionsPresent.ContainsKey(name))
            {
                areCollectionsPresent[name] = true;
            }
        }

        foreach (KeyValuePair<string, bool> kvp in areCollectionsPresent)
        {
            if (kvp.Value == false)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Initializes new collections in a non-OpenCafe database. 
    /// </summary>
    public async Task InitCollections()
    {
        var logger = new Logger();
        await logger.New(new Log(type: "Info", message: "Initializing database collections.", where: "Database::InitCollections()"));

        var key = collectionEncryption["admins"]["key"];
        var iv = collectionEncryption["admins"]["iv"];
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            var e = new InvalidCredentialException(
                "Check your config file as it doesn't contain collection encryption credentials. Regenerate it and try again.");
            await logger.LogException(e);
            throw e;
        }

        await _database.CreateCollectionAsync("customers");
        await _database.CreateCollectionAsync("admins");
        await _database.CreateCollectionAsync("dishes");
        await _database.CreateCollectionAsync("images");
        await _database.CreateCollectionAsync("cards");
        var firstHeadToken = await new Admins().GenTokenAsync();
        await Console.Out.WriteLineAsync("This is an auto-generated token for a head admin, it's CRUCIAL to write it down somewhere secure. It is also stored in a file in the app folder. You NEED to remove it afterwards." + Environment.NewLine + firstHeadToken);

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directoryPath = Path.Combine(appDataPath, "OpenCafe");
        Directory.CreateDirectory(directoryPath); // Ensure directory exists
        await File.WriteAllTextAsync(Path.Combine(directoryPath, "firsthead_token.txt"), firstHeadToken);
        // Creates duplicates of firsthead_token.txt
        await File.WriteAllTextAsync(Path.Combine(directoryPath, "firsthead_token.txt.1.bckp"), firstHeadToken);
        await File.WriteAllTextAsync(Path.Combine(directoryPath, "firsthead_token.txt.2.bckp"), firstHeadToken);
        firstHeadToken = await CryptoHelper.EncryptAsync(firstHeadToken, key, iv);
        var adminCollection = _database.GetCollection<BsonDocument>("admins");
        // will rename that later smh
        await adminCollection.InsertOneAsync(new Admin(name: "JETLGGD", role: "head", token: firstHeadToken).ToBsonDocument());
    }
}
