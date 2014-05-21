using System;
using Abc.Shared.Annotations;

namespace Abc.Zebus.Pipes
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