namespace Abc.Zebus
{
    public interface IMessageContextAware
    {
        MessageContext? Context { get; set; }
    }
}
