using System;
using Abc.Zebus.Dispatch.Pipes;

namespace Abc.Zebus.Tests.Dispatch.Pipes
{
    public class TestPipe : IPipe
    {
        public TestPipe()
        {
            Name = "TestPipe";
        }

        public string Name { get; set; }
        public int Priority { get; set; }
        public bool IsAutoEnabled { get; set; }
        public Action<BeforeInvokeArgs> BeforeCallback { get; set; }
        public Action<AfterInvokeArgs> AfterCallback { get; set; }
        public BeforeInvokeArgs BeforeInvokeArgs { get; private set; }
        public AfterInvokeArgs AfterInvokeArgs { get; private set; }

        public void BeforeInvoke(BeforeInvokeArgs args)
        {
            BeforeCallback?.Invoke(args);
            BeforeInvokeArgs = args;
        }

        public void AfterInvoke(AfterInvokeArgs args)
        {
            AfterCallback?.Invoke(args);
            AfterInvokeArgs = args;
        }
    }
}
