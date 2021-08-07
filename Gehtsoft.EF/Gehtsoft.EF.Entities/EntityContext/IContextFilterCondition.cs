namespace Gehtsoft.EF.Entities.Context
{
    public interface IContextFilterCondition
    {
        IContextFilterCondition Property(string name);

        IContextFilterCondition Is(CmpOp op);

        IContextFilterCondition Value(object value);
    }
}