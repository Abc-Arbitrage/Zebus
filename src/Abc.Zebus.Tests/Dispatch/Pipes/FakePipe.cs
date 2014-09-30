using System;
using Abc.Zebus.Dispatch.Pipes;

namespace Abc.Zebus.Tests.Dispatch.Pipes
{
    public class FakePipe : IPipe
    {
        public FakePipe()
        {
            Name = "FakePipe";
        }

        public string Name { get; set; }
        public int Priority { get; set; }
        public bool IsAutoEnabled { get; set; }
        public Action<BeforeInvokeArgs> BeforeCallback { get; set; }
        public Action<AfterInvokeArgs> AfterCallback { get; set; }

        public void BeforeInvoke(BeforeInvokeArgs args)
        {
            if (BeforeCallback != null)
                BeforeCallback(args);
        }

        public void AfterInvoke(AfterInvokeArgs args)
        {
            if (AfterCallback != null)
                AfterCallback(args);
        }
    }
}