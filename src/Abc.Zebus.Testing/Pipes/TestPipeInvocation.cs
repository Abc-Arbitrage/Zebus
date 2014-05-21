using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Scan.Pipes;
using Abc.Zebus.Testing.Dispatch;

namespace Abc.Zebus.Testing.Pipes
{
    public class TestPipeInvocation : PipeInvocation
    {
        public TestPipeInvocation(IMessage message, Type handlerType, Exception exception = null) : base(new TestMessageHandlerInvoker(handlerType, message.GetType()), message, MessageContext.CreateTest("u.name"), new List<IPipe>())
        {
            AddExceptionCallback(exception);
        }

        public TestPipeInvocation(IMessage message, MessageContext messageContext, IMessageHandlerInvoker invoker)
            : base(invoker, message, messageContext, new List<IPipe>())
        {
        }

        public static TestPipeInvocation Create<TMessage>(TMessage message) where TMessage : class, IMessage
        {
            return new TestPipeInvocation(message, MessageContext.CreateTest("u.name"), new TestMessageHandlerInvoker<TMessage>());
        }

        public bool WasRun { get; private set; }
        public bool WasRunAsync { get; private set; }

        public void AddPipe(IPipe pipe)
        {
            Pipes.Add(pipe);
        }

        public void TestRun()
        {
            Run();
        }

        protected internal override void Run()
        {
            WasRun = true;

            base.Run();
        }

        public Task TestRunAsync()
        {
            return RunAsync();
        }

        protected internal override Task RunAsync()
        {
            WasRunAsync = true;

            return base.RunAsync();
        }

        private void AddExceptionCallback(Exception exception)
        {
            if (exception == null)
                return;

            var invoker = (TestMessageHandlerInvoker)Invoker;
            invoker.InvokeMessageHandlerCallback = x =>
            {
                throw exception;
            };
        }
    }
}