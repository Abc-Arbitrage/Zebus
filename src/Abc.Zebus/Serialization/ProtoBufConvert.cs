using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ProtoBuf.Meta;

namespace Abc.Zebus.Serialization
{
    internal static class ProtoBufConvert
    {
        private static readonly ConcurrentDictionary<Type, bool> _hasParameterLessConstructorByType = new ConcurrentDictionary<Type, bool>();

        public static ReadOnlyMemory<byte> Serialize(object message)
        {
            var stream = new MemoryStream();
            Serialize(stream, message);

            return new ReadOnlyMemory<byte>(stream.GetBuffer(), 0, (int)stream.Position);
        }

        public static void Serialize(Stream stream, object message)
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

        public static object Deserialize(Type messageType, ReadOnlyMemory<byte> bytes)
        {
            var obj = CreateMessageIfRequired(messageType);

            return RuntimeTypeModel.Default.Deserialize(bytes, type: messageType, value: obj);
        }

        public static object Deserialize(Type messageType, Stream stream)
        {
            var obj = CreateMessageIfRequired(messageType);

            return RuntimeTypeModel.Default.Deserialize(stream, value: obj, type: messageType);
        }

        private static object? CreateMessageIfRequired(Type messageType)
        {
            if (!HasParameterLessConstructor(messageType) && messageType != typeof(string))
                return FormatterServices.GetUninitializedObject(messageType);

            return null;
        }

        [SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
        private static bool HasParameterLessConstructor(Type messageType)
            => _hasParameterLessConstructorByType.GetOrAdd(messageType, type => ComputeHasParameterLessConstructor(type));

        private static bool ComputeHasParameterLessConstructor(Type type)
            => type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null;

        public static bool CanSerialize([NotNullWhen(true)] Type? messageType)
            => messageType != null && RuntimeTypeModel.Default.CanSerialize(messageType);
    }
}
