using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.MongoDb
{
    internal interface IMongoPathResolver
    {
        string TranslatePath(string path);
    }
}
