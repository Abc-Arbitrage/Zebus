using Abc.Zebus.Util.Annotations;

namespace Abc.Zebus
{
    [UsedImplicitly]
    public interface IExtendedMessageHandler<T> : IMessageHandler<T> where T : class
    {
        
    }
}