namespace Abc.Zebus.Dispatch.Pipes
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