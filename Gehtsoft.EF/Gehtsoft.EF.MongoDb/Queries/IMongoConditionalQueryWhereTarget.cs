namespace Gehtsoft.EF.MongoDb
{
    internal interface IMongoConditionalQueryWhereTarget
    {
        void EndWhereGroup(MongoConditionalQueryWhereGroup group);
    }
}
