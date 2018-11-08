using System;
using System.Threading;
using ProtoBuf;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [ProtoContract]
    public class DispatchCommand : ICommand
    {
        public readonly AutoResetEvent Signal = new AutoResetEvent(false);

        [ProtoMember(1)]
        public Guid Guid { get; set; } = Guid.NewGuid();
    }
}
