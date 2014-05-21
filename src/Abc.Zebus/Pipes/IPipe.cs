namespace Abc.Zebus.Pipes
{
    public interface IPipe
    {
        string Name { get; }
        int Priority { get; }
        bool IsAutoEnabled { get; }

        void BeforeInvoke(BeforeInvokeArgs args);
        void AfterInvoke(AfterInvokeArgs args);
    }
}