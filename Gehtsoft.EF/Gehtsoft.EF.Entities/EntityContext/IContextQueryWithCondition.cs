namespace Gehtsoft.EF.Entities.Context
{
    public interface IContextQueryWithCondition : IEntityQuery
    {
        IContextFilter Where { get; }
    }
}