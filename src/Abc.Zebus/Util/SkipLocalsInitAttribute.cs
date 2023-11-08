// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

#if NETCOREAPP

[assembly: TypeForwardedTo(typeof(SkipLocalsInitAttribute))]

#else

[AttributeUsage(AttributeTargets.Module
                | AttributeTargets.Class
                | AttributeTargets.Struct
                | AttributeTargets.Interface
                | AttributeTargets.Constructor
                | AttributeTargets.Method
                | AttributeTargets.Property
                | AttributeTargets.Event,
                Inherited = false)]
internal sealed class SkipLocalsInitAttribute : Attribute
{
}

#endif
