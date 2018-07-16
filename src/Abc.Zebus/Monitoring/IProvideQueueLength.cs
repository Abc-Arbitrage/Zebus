namespace Abc.Zebus.Monitoring
{
    public interface IProvideQueueLength
    {
        int GetReceiveQueueLength();
        int Purge();
    }
}