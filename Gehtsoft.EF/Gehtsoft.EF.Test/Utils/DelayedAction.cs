using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Test.Utils
{
    public sealed class DelayedAction : IDisposable
    {
        private readonly Action mAction;

        public DelayedAction(Action action)
        {
            mAction = action;
        }

        public void Dispose() => mAction();
    }
}
