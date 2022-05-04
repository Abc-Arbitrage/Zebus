using System;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Routing
{
    [TestFixture]
    public class MessageBindingTests
    {
        [Test, SetCulture("FR-fr")]
        public void should_create_message_binding_from_message()
        {
            var message = new FakeRoutableCommand(42.42m, "name", Guid.NewGuid());

            var messageBinding = MessageBinding.FromMessage(message);

            messageBinding.MessageTypeId.ShouldEqual(MessageUtil.TypeId<FakeRoutableCommand>());
            messageBinding.RoutingContent.PartCount.ShouldEqual(3);
            messageBinding.RoutingContent[0].ShouldEqual(new RoutingContentValue("42.42"));
            messageBinding.RoutingContent[1].ShouldEqual(new RoutingContentValue("name"));
            messageBinding.RoutingContent[2].ShouldEqual(new RoutingContentValue(message.OtherId.ToString()));
        }

        [Test, SetCulture("FR-fr")]
        public void should_create_message_binding_from_message_with_collections()
        {
            var message = new FakeRoutableCommandWithCollection
            {
                Name = "X",
                IdArray = new[] { 1, 2 },
                ValueList = new() { 1.5m },
            };

            var messageBinding = MessageBinding.FromMessage(message);

            messageBinding.MessageTypeId.ShouldEqual(MessageUtil.TypeId<FakeRoutableCommandWithCollection>());
            messageBinding.RoutingContent.PartCount.ShouldEqual(3);
            messageBinding.RoutingContent[0].ShouldEqual(new RoutingContentValue("X"));
            messageBinding.RoutingContent[1].ShouldEqual(new RoutingContentValue(new[] { "1", "2" }));
            messageBinding.RoutingContent[2].ShouldEqual(new RoutingContentValue(new[] { "1.5" }));
        }

        [Test]
        public void should_create_message_binding_from_message_with_properties()
        {
            var message = new FakeRoutableCommandWithProperties { Id = 100, FeedId = 200 };

            var messageBinding = MessageBinding.FromMessage(message);

            messageBinding.MessageTypeId.ShouldEqual(MessageUtil.TypeId<FakeRoutableCommandWithProperties>());
            messageBinding.RoutingContent.PartCount.ShouldEqual(2);
            messageBinding.RoutingContent[0].ShouldEqual(new RoutingContentValue("100"));
            messageBinding.RoutingContent[1].ShouldEqual(new RoutingContentValue("200"));
        }

        [Test]
        public void should_ignore_null_members()
        {
            var message = new FakeRoutableCommand(0, null);

            var messageBinding = MessageBinding.FromMessage(message);

            messageBinding.RoutingContent[0].ShouldEqual(new RoutingContentValue("0"));
            messageBinding.RoutingContent[1].ShouldEqual(new RoutingContentValue((string)null));
        }

        [Routable]
        public class FakeRoutableCommandWithProperties : ICommand
        {
            [RoutingPosition(1)]
            public int Id { get; set; }

            [RoutingPosition(2)]
            public int FeedId { get; set; }
        }
    }
}
