using System;

namespace Abc.Zebus.Routing
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class Routable : Attribute
    {
    }
}