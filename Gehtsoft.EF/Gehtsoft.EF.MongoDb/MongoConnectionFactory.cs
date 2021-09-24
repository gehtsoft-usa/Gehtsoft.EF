using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The factory for Mongo connections.
    ///
    /// Please note that there is no async method to create connection because Mongo does not
    /// communicate the server until the first operation is performed.
    /// </summary>
    public static class MongoConnectionFactory
    {
        public static MongoConnection Create(string connectionString)
        {
            MongoUrl url = MongoUrl.Create(connectionString);
            IMongoClient client = new MongoClient(url);
            IMongoDatabase database = client.GetDatabase(url.DatabaseName);
            return new MongoConnection(database);
        }
    }
}
