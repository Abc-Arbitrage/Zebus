using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ProtoBuf.Meta;

namespace Abc.Zebus.Serialization
{
    internal static class Serializer
    {
        private static readonly ConcurrentDictionary<Type, bool> _hasParameterLessConstructorByType = new ConcurrentDictionary<Type, bool>();

        public static ReadOnlyMemory<byte> Serialize(object message)
        {
            var stream = new MemoryStream();
            Serialize(stream, message);

            return new ReadOnlyMemory<byte>(stream.GetBuffer(), 0, (int)stream.Position);
        }

        private static void Serialize(Stream stream, object message)
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

        [return: NotNullIfNotNull("messageType")]
        public static object? Deserialize(Type? messageType, ReadOnlyMemory<byte> bytes)
        {
            if (messageType is null)
                return null;

            var obj = CreateMessageIfRequired(messageType);

            return RuntimeTypeModel.Default.Deserialize(bytes, type: messageType, value: obj);
        }

        [return: NotNullIfNotNull("messageType")]
        private static object? Deserialize(Type? messageType, Stream stream)
        {
            if (messageType is null)
                return null;

            var obj = CreateMessageIfRequired(messageType);

            return RuntimeTypeModel.Default.Deserialize(stream, value: obj, type: messageType);
        }

        private static object? CreateMessageIfRequired(Type messageType)
        {
            if (!HasParameterLessConstructor(messageType) && messageType != typeof(string))
                return FormatterServices.GetUninitializedObject(messageType);

            return null;
        }

        public static bool TryClone<T>(T? message, [NotNullWhen(true)] out T? clone)
            where T : class
        {
            var messageType = message?.GetType();
            if (messageType != null && RuntimeTypeModel.Default.CanSerialize(messageType))
            {
                // Cannot use the DeepClone method as it doesn't handle classes without a parameterless constructor

                using (var ms = new MemoryStream())
                {
                    Serialize(ms, message!);
                    ms.Position = 0;
                    clone = (T)Deserialize(messageType, ms);
                }

                return true;
            }

            clone = null;
            return false;
        }

        [SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
        private static bool HasParameterLessConstructor(Type messageType)
            => _hasParameterLessConstructorByType.GetOrAdd(messageType, type => ComputeHasParameterLessConstructor(type));

        private static bool ComputeHasParameterLessConstructor(Type type)
            => type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null;
    }
}
