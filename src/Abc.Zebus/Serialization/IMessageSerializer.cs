namespace Abc.Zebus.Serialization
{
    public interface IMessageSerializer
    {
        IMessage Deserialize(MessageTypeId messageTypeId, byte[] messageBytes);
        byte[] Serialize(IMessage message);
    }
}