using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Abc.Zebus.Util;
using ProtoBuf.Meta;

namespace Abc.Zebus.Serialization
{
    internal class Serializer
    {
        private readonly ConcurrentDictionary<Type, bool> _hasParameterLessConstructorByType = new ConcurrentDictionary<Type, bool>();

        public byte[] SerializeToBytes(object message)
        {
            return Serialize(message).ToArray();
        }
        
        public MemoryStream Serialize(object message)
        {
            var stream = new MemoryStream();
            Serialize(stream, message);
            return stream;
        }

        public void Serialize(Stream stream, object message)
        {
            try
            {
                RuntimeTypeModel.Default.Serialize(stream, message);
            }
            catch (Exception ex)
            {
                throw new ProtocolBufferSerializationException(message, ex);
            }
        }

        public T Deserialize<T>(byte[] bytes)
        {
            return (T)Deserialize(typeof(T), new MemoryStream(bytes));
        }

        public object Deserialize(Type messageType, MemoryStream stream)
        {
            if (messageType == null)
                return null;

            stream.Position = 0; // Reset position

            object obj = null;
            if (!HasParameterLessConstructor(messageType) && messageType != typeof(string))
                obj = FormatterServices.GetUninitializedObject(messageType);


            return RuntimeTypeModel.Default.Deserialize(stream, obj, messageType);
        }

        public object Deserialize(string messageTypeName, MemoryStream stream)
        {
            var messageType = TypeUtil.Resolve(messageTypeName);
            return Deserialize(messageType, stream);
        }

        private bool HasParameterLessConstructor(Type messageType)
        {
            return _hasParameterLessConstructorByType.GetOrAdd(messageType, type =>
                {
                    return messageType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null) != null;
                });
        }
    }
}