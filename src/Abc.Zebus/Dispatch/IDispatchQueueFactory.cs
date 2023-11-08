namespace Abc.Zebus.Dispatch;

public interface IDispatchQueueFactory
{
    DispatchQueue Create(string queueName);
}
