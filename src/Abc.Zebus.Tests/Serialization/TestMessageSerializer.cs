using System;
using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization;

namespace Abc.Zebus.Tests.Serialization
{
    public class TestMessageSerializer : IMessageSerializer
    {
        private readonly Dictionary<MessageTypeId, Exception> _serializationExceptions = new Dictionary<MessageTypeId, Exception>();
        private readonly Dictionary<MessageTypeId, Func<IMessage, ReadOnlyMemory<byte>>> _serializationFuncs = new Dictionary<MessageTypeId, Func<IMessage, ReadOnlyMemory<byte>>>();
        private readonly MessageSerializer _serializer = new MessageSerializer();

        public void AddSerializationFuncFor<TMessage>(Func<TMessage, ReadOnlyMemory<byte>> func)
            where TMessage : IMessage
        {
            _serializationFuncs.Add(MessageUtil.TypeId<TMessage>(), msg => func((TMessage)msg));
        }

        public void AddSerializationExceptionFor(MessageTypeId messageTypeId, string exceptionMessage = "Error")
        {
            _serializationExceptions.Add(messageTypeId, new Exception(exceptionMessage));
        }

        public void AddSerializationExceptionFor<TMessage>(Exception exception)
            where TMessage : IMessage
        {
            _serializationExceptions.Add(MessageUtil.TypeId<TMessage>(), exception);
        }

        public IMessage Deserialize(MessageTypeId messageTypeId, ReadOnlyMemory<byte> bytes)
        {
            if (_serializationExceptions.TryGetValue(messageTypeId, out var exception))
                throw exception;

            return _serializer.Deserialize(messageTypeId, bytes);
        }

        public bool TryClone(IMessage message, out IMessage clone)
            => _serializer.TryClone(message, out clone);

        public ReadOnlyMemory<byte> Serialize(IMessage message)
        {
            if (_serializationExceptions.TryGetValue(message.TypeId(), out var exception))
                throw exception;

            if (_serializationFuncs.TryGetValue(message.TypeId(), out var serializationFunc))
                return serializationFunc.Invoke(message);

            return _serializer.Serialize(message);
        }
    }
}
