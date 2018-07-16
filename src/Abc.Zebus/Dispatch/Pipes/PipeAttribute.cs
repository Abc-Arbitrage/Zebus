using System;
using JetBrains.Annotations;

namespace Abc.Zebus.Dispatch.Pipes
{
    [AttributeUsage(AttributeTargets.Class), UsedImplicitly]
    public class PipeAttribute : Attribute
    {
        public PipeAttribute(Type pipeType)
        {
            PipeType = pipeType;
        }

        public Type PipeType { get; private set; }
    }
}