using System;
using System.IO;
using System.Runtime.Serialization;
using Abc.Zebus.Serialization;
using Abc.Zebus.Util;

// TODO: Namespace intentionally wrong, do not fix (will be removed from the assembly)
namespace ABC.ServiceBus.Contracts
{
    public static class Timeout
    {
        private static readonly Serializer _serializer = new Serializer();

        public static RequestTimeoutCommand BuildRequest(string key, DateTime dateTimeLocal, object state, string serviceName)
        {
            return BuildRequestUtc(key, dateTimeLocal.ToUniversalTime(), state, serviceName);
        }

        public static RequestTimeoutCommand BuildRequestUtc(string key, DateTime dateTimeUtc, object state, string serviceName)
        {
            var stateTypeName = state != null ? state.GetType().FullName : string.Empty;
            var stateBytes = Serialize(state);

            return new RequestTimeoutCommand(key, dateTimeUtc, stateTypeName, stateBytes, serviceName);
        }

        public static byte[] Serialize(object state)
        {
            var stateBytes = new byte[0];
            if (state != null)
                stateBytes = _serializer.Serialize(state).ToArray();

            return stateBytes;
        }

        public static object Deserialize(TimeoutCommand command)
        {
            return Deserialize(command.Data, command.DataType);
        }

        private static object Deserialize(byte[] data, string dataType)
        {
            object state = null;
            if (data.Length > 0)
            {
                var memoryStream = new MemoryStream(data);
                var type = TypeUtil.Resolve(dataType);
                state = _serializer.Deserialize(type, memoryStream);
            }

            return state;
        }

        public static object Deserialize(this RequestTimeoutCommand command)
        {
            return Deserialize(command.Data, command.DataType);
        }

        public static T Deserialize<T>(this RequestTimeoutCommand command)
        {
            return (T)command.Deserialize();
        }

        public static bool TryDeserialize<T>(this TimeoutCommand command, out T state)
        {
            var expectedDataType = typeof(T).FullName;
            if (expectedDataType != command.DataType)
            {
                state = default(T);
                return false;
            }

            state = (T)Deserialize(command);
            return true;
        }

        public static T Deserialize<T>(this TimeoutCommand command)
        {
            var state = default(T);
            if (command.TryDeserialize(out state))
                return state;

            throw new SerializationException($"{command.DataType} can't be deserialized into {typeof(T).FullName}");
        }
    }
}
