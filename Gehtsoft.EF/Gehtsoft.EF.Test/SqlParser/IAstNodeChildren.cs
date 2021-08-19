using System.Collections.Generic;

namespace Gehtsoft.EF.Test.SqlParser
{
    public interface IAstNodeChildren : IEnumerable<IAstNode>
    {
        public int Count { get; }
        public IAstNode this[int index] { get; }
    }
}
