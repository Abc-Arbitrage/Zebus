using System;

namespace Abc.Zebus
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TransientAttribute : Attribute
    {
    }
}