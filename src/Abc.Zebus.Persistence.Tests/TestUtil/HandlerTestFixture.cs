using System;
using Abc.Zebus.Testing;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.TestUtil
{
    [TestFixture]
    public abstract class HandlerTestFixture<THandler> where THandler : class
    {
        private IDisposable _contextScope;
        protected MockContainer MockContainer { get; private set; }

        protected TestBus Bus { get; private set; }
        protected THandler Handler { get; private set; }
        protected MessageContext CurrentMessageContext { get; set; }

        [SetUp]
        public virtual void Setup()
        {
            CurrentMessageContext = CreateMessageContext();
            _contextScope = MessageContext.SetCurrent(CurrentMessageContext);


            MockContainer = new MockContainer();
            Bus = new TestBus();
            MockContainer.Configure(x =>
            {
                x.For<IBus>().Use(Bus);
                x.For<MessageContext>().Use(CurrentMessageContext);
            });
            
            
            BeforeBuildingHandler();
            CreateHandler();
        }

        [TearDown]
        public virtual void Teardown()
        {
            _contextScope.Dispose();
        }

        protected virtual MessageContext CreateMessageContext()
        {
            return MessageContext.CreateTest();
        }

        protected void CreateHandler()
        {
            MockContainer.FillMissingParameterTypesWithMocks<THandler>();
            var handler = MockContainer.GetInstance<THandler>();
            var messageContextAware = handler as IMessageContextAware;
            if (messageContextAware != null)
                messageContextAware.Context = CurrentMessageContext;

            Handler = handler;
        }

        protected virtual void BeforeBuildingHandler() { }

    }
}