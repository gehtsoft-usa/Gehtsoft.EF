using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Utils
{
    [DocgenIgnore]
    public static class ValueTaskExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SyncOp(this ValueTask vt)
        {
            if (!vt.IsCompleted)
                throw new ArgumentException("The task is expected to be completed", nameof(vt));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T SyncResult<T>(this ValueTask<T> vt)
        {
            if (!vt.IsCompleted)
                throw new ArgumentException("The task is expected to be completed", nameof(vt));
#pragma warning disable S5034 // false positive: "ValueTask" should be consumed correctly
            return vt.Result;
#pragma warning restore S5034
        }
    }
}
