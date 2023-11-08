namespace Abc.Zebus;

internal interface IInternalBus : IBus
{
    void Publish(IEvent message, PeerId targetPeer);
}
