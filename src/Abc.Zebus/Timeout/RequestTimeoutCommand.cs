using System;
using System.ComponentModel;
using Abc.Zebus;
using ProtoBuf;

// TODO: Namespace intentionally wrong, do not fix (will be removed from the assembly)
namespace ABC.ServiceBus.Contracts
{
    [ProtoContract]
    public class RequestTimeoutCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly string Key;

        [ProtoMember(2, IsRequired = true)]
        public readonly DateTime DateTimeUtc;

        [ProtoMember(3, IsRequired = true)]
        public readonly string DataType;

        [ProtoMember(4, IsRequired = true)]
        public readonly byte[] Data;

        [ProtoMember(5), DefaultValue("")]
        public readonly string ServiceName;

        [ProtoMember(6)]
        public readonly string HandlerName;

        public RequestTimeoutCommand(string key, DateTime dateTimeUtc, string dataType, byte[] data, string serviceName, string handlerName = null)
        {
            HandlerName = handlerName;
            Key = key;
            DataType = dataType;
            Data = data;
            ServiceName = serviceName;
            DateTimeUtc = dateTimeUtc;
        }

        public override string ToString()
        {
            return $"Key: {Key}, DateTimeUtc: {DateTimeUtc:G}, ServiceName: {ServiceName}, HandlerName: {HandlerName}";
        }
    }
}
