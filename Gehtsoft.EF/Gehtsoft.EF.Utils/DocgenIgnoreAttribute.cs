using System;
using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Utils
{
    [AttributeUsage(AttributeTargets.Class |
        AttributeTargets.Constructor |
        AttributeTargets.Property |
        AttributeTargets.Field |
        AttributeTargets.Interface |
        AttributeTargets.Method)]
    public class DocgenIgnoreAttribute : Attribute
    {
    }
}
