using System;
using System.Collections.Generic;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Scan
{
    [TestFixture]
    public class MultiEventHandlerInvokerLoaderTests
    {
        private MultiEventHandlerInvokerLoader _loader;
        private Container _container;

        [SetUp]
        public void Setup()
        {
            _container = new Container();

            _loader = new MultiEventHandlerInvokerLoader(_container);
        }

        [Test]
        public void should_not_auto_subscribe_to_routable_events_on_startup()
        {
            var invokers = _loader.LoadMessageHandlerInvokers(typeof(EventForwarderWithRoutableEvent));

            var invoker = invokers.ExpectedSingle();
            invoker.ShouldBeSubscribedOnStartup.ShouldBeFalse();
        }

        [Routable]
        public class RoutableEvent : IEvent
        {
            [RoutingPosition(1)]
            public readonly string RoutingKey;

            public RoutableEvent(string routingKey)
            {
                RoutingKey = routingKey;
            }
        }

        public class EventForwarderWithRoutableEvent : IMultiEventHandler
        {
            public void Handle(IEvent e)
            {
            }

            public IEnumerable<Type> GetHandledEventTypes()
            {
                yield return typeof(RoutableEvent);
            }
        }
    }
}