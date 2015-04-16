using System;
using System.Globalization;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Measurements;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class BindingKeyTests
    {
        [Test]
        public void should_get_routing_key_from_message()
        {
            using (new CultureScope(CultureInfo.GetCultureInfo("FR-fr")))
            {
                var message = new FakeRoutableCommand(42.42m, "name", Guid.NewGuid());
                var rountingKey = BindingKey.Create(message);

                rountingKey.PartCount.ShouldEqual(3);
                rountingKey.GetPart(0).ShouldEqual("42.42");
                rountingKey.GetPart(1).ShouldEqual("name");
                rountingKey.GetPart(2).ShouldEqual(message.OtherId.ToString());
            }
        }

        [Test]
        public void should_get_routing_key_from_message_with_properties()
        {
            var message = new FakeRoutableCommandWithProperties { Id = 100, FeedId = 200 };
            var routingKey = BindingKey.Create(message);

            routingKey.PartCount.ShouldEqual(2);
            routingKey.GetPart(0).ShouldEqual("100");
            routingKey.GetPart(1).ShouldEqual("200");

            routingKey.ToString().ShouldEqual("100.200");
        }

        [Test]
        public void should_use_special_char_for_empty_binding_key()
        {
            var empty = new BindingKey(new string[0]);

            empty.ToString().ShouldEqual("#");
        }

        [Test]
        [Ignore]
        [Category("ManualOnly")]
        public void MeasurePerformances()
        {
            var message = new FakeMarketDataEvent("USA", "NASDAQ", "MSFT");
            Measure.Execution(1000000, () => BindingKey.Create(message));
        }

        [Test]
        public void should_send_routing_key_exception()
        {
            var msg = new FakeRoutableCommand(0, null);

            var exception = Assert.Throws<InvalidOperationException>(() => BindingKey.Create(msg));
            exception.Message.ShouldContain(typeof(FakeRoutableCommand).Name);
            exception.Message.ShouldContain("Name");
            exception.Message.ShouldContain("can not be null");
        }

        [Routable]
        public class FakeRoutableCommandWithProperties : ICommand
        {
            [RoutingPosition(1)]
            public int Id { get; set; }

            [RoutingPosition(2)]
            public int FeedId { get; set; }
        }

        [Routable]
        public class FakeMarketDataEvent : IEvent
        {
            [RoutingPosition(1)]
            public readonly string Zone;
            [RoutingPosition(2)]
            public string ExchangeCode { get; private set; }
            [RoutingPosition(3)]
            public readonly string Ticker;

            public FakeMarketDataEvent(string zone, string exchangeCode, string ticker)
            {
                Zone = zone;
                Ticker = ticker;
                ExchangeCode = exchangeCode;
            }
        }
    }
}