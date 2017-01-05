using System;
using System.Collections.Generic;
using System.IO;
using Abc.Zebus.Serialization;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Tests.Serialization
{
    public class TestMessageSerializer : IMessageSerializer
    {
        private readonly Dictionary<MessageTypeId, Exception> _serializationExceptions = new Dictionary<MessageTypeId, Exception>();
        private readonly Dictionary<MessageTypeId, Func<IMessage, Stream>> _serializationFuncs = new Dictionary<MessageTypeId, Func<IMessage, Stream>>();
        private readonly MessageSerializer _serializer = new MessageSerializer();

        public void AddSerializationFuncFor<TMessage>(Func<TMessage, Stream> func) where TMessage : IMessage
        {
            _serializationFuncs.Add(MessageUtil.TypeId<TMessage>(), msg => func((TMessage)msg));
        }

        public void AddSerializationExceptionFor(MessageTypeId messageTypeId, string exceptionMessage = "Error")
        {
            _serializationExceptions.Add(messageTypeId, new Exception(exceptionMessage));
        }

        public void AddSerializationExceptionFor<TMessage>(Exception exception) where TMessage : IMessage
        {
            _serializationExceptions.Add(MessageUtil.TypeId<TMessage>(), exception);
        }

        public IMessage Deserialize(MessageTypeId messageTypeId, Stream stream)
        {
            var exception = _serializationExceptions.GetValueOrDefault(messageTypeId);
            if (exception != null)
                throw exception;

            return _serializer.Deserialize(messageTypeId, stream);
        }

        public Stream Serialize(IMessage message)
        {
            var exception = _serializationExceptions.GetValueOrDefault(message.TypeId());
            if (exception != null)
                throw exception;

            var serializationFunc = _serializationFuncs.GetValueOrDefault(message.TypeId());
            if (serializationFunc != null)
                return serializationFunc.Invoke(message);

            return _serializer.Serialize(message);
        }
    }
}