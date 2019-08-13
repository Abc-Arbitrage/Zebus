using System;
using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization;

namespace Abc.Zebus.Tests.Serialization
{
    public class TestMessageSerializer : IMessageSerializer
    {
        private readonly Dictionary<MessageTypeId, Exception> _serializationExceptions = new Dictionary<MessageTypeId, Exception>();
        private readonly Dictionary<MessageTypeId, Func<IMessage, Stream>> _serializationFuncs = new Dictionary<MessageTypeId, Func<IMessage, Stream>>();
        private readonly MessageSerializer _serializer = new MessageSerializer();

        public void AddSerializationFuncFor<TMessage>(Func<TMessage, Stream> func)
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

        public IMessage Deserialize(MessageTypeId messageTypeId, Stream stream)
        {
            if (_serializationExceptions.TryGetValue(messageTypeId, out var exception))
                throw exception;

            return _serializer.Deserialize(messageTypeId, stream);
        }

        public bool TryClone(IMessage message, out IMessage clone)
            => _serializer.TryClone(message, out clone);

        public Stream Serialize(IMessage message)
        {
            if (_serializationExceptions.TryGetValue(message.TypeId(), out var exception))
                throw exception;

            if (_serializationFuncs.TryGetValue(message.TypeId(), out var serializationFunc))
                return serializationFunc.Invoke(message);

            return _serializer.Serialize(message);
        }
    }
}
