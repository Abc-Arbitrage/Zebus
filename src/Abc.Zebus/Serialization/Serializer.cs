using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ProtoBuf.Meta;

namespace Abc.Zebus.Serialization
{
    internal class Serializer
    {
        private readonly ConcurrentDictionary<Type, bool> _hasParameterLessConstructorByType = new ConcurrentDictionary<Type, bool>();

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

        public object Deserialize(Type messageType, Stream stream)
        {
            if (messageType == null)
                return null;

            stream.Position = 0; // Reset position

            object obj = null;
            if (!HasParameterLessConstructor(messageType) && messageType != typeof(string))
                obj = FormatterServices.GetUninitializedObject(messageType);

            return RuntimeTypeModel.Default.Deserialize(stream, obj, messageType);
        }

        public static T TryClone<T>(T message)
            where T : class
        {
            if (message != null && RuntimeTypeModel.Default.CanSerialize(message.GetType()))
                return (T)RuntimeTypeModel.Default.DeepClone(message);

            return message;
        }

        private bool HasParameterLessConstructor(Type messageType)
        {
            return _hasParameterLessConstructorByType.GetOrAdd(messageType, type => ComputeHasParameterLessConstructor(type));
        }

        private static bool ComputeHasParameterLessConstructor(Type type)
        {
            return type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null) != null;
        }
    }
}
