using System;
using ProtoBuf;

namespace Abc.Zebus.Tests.Messages
{
    [ProtoContract]
    public class FakeCommandWithTimestamp : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly TimeSpan Timestamp;

        FakeCommandWithTimestamp()
        {
        }

        public FakeCommandWithTimestamp(TimeSpan timestamp)
        {
            Timestamp = timestamp;
        }
    }
}