using System.IO;

namespace Abc.Zebus.Serialization
{
    public interface IMessageSerializer
    {
        Stream Serialize(IMessage message);
        IMessage Deserialize(MessageTypeId messageTypeId, Stream stream);
        bool TryClone(IMessage message, out IMessage clone);
    }
}
