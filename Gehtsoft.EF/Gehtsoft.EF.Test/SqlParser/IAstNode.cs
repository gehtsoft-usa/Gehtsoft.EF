using System;

namespace Gehtsoft.EF.Test.SqlParser
{
    public interface IAstNode : IEquatable<IAstNode>, IFormattable
    {
        string Symbol { get; }
        string Value { get; }

        public IAstNodeChildren Children { get; }
    }
}
