using System;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IEntityTable
    {
        string Name { get; }
        Type EntityType { get; }
    }
}