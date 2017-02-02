using System;

namespace Abc.Zebus
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TransientAttribute : Attribute
    {
    }
}
