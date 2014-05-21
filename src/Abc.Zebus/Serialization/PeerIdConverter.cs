using System;
using Abc.Zebus.Util.Annotations;
using Newtonsoft.Json;

namespace Abc.Zebus.Serialization
{
    [UsedImplicitly]
    public class PeerIdConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var peerId = (PeerId)value;
            writer.WriteValue(peerId.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String)
                return Activator.CreateInstance(objectType); // objectType can be Nullable<PeerId>

            var value = reader.Value.ToString();
            return new PeerId(value);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PeerId);
        }
    }
}