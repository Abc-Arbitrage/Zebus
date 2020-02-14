using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Routing
{
    [TestFixture]
    public class BindingKeyUtilTests
    {
        [TestCaseSource(nameof(TestSources))]
        public void should_match_valid_message(ExpectedResult expectedResult)
        {
            // Arrange
            var messageTypeId = MessageUtil.TypeId<FakeRoutableCommand>();

            // Act
            var predicate = BindingKeyUtil.BuildPredicate(messageTypeId, expectedResult.BindingKey);

            // Assert
            predicate(expectedResult.Message).ShouldEqual(expectedResult.Result);
        }

        public static ExpectedResult[] TestSources { get; } = {
            new ExpectedResult(new FakeRoutableCommand(1, "toto"), new BindingKey("1", "toto", "*"), true),
            new ExpectedResult(new FakeRoutableCommand(1, "toto"), new BindingKey("*", "toto", "*"), true),
            new ExpectedResult(new FakeRoutableCommand(1, "toto"), new BindingKey("1", "*", "*"), true),
            new ExpectedResult(new FakeRoutableCommand(1, "toto"), new BindingKey("#"), true),
            new ExpectedResult(new FakeRoutableCommand(1, "toto"), new BindingKey("1", "#"), true),
            new ExpectedResult(new FakeRoutableCommand(1, "toto"), new BindingKey("2", "#"), false),
            new ExpectedResult(new FakeRoutableCommand(1, "toto"), new BindingKey("2", "toto", "*"), false),
            new ExpectedResult(new FakeRoutableCommand(1, "toto"), new BindingKey("1", "tota", "*"), false),
        };

        public class ExpectedResult
        {
            public ExpectedResult(FakeRoutableCommand message, BindingKey bindingKey, bool result)
            {
                Message = message;
                BindingKey = bindingKey;
                Result = result;
            }

            public FakeRoutableCommand Message { get; set; }
            public BindingKey BindingKey { get; set; }
            public bool Result { get; set; }

            public override string ToString()
            {
                return $"BindingKey: {BindingKey}";
            }
        }

        [Test]
        public void should_get_binding_key_parts_for_member()
        {
            // Arrange
            var bindingKeys = new[]
            {
                Subscription.Matching<StrangeRoutableMessage>(x => x.Id == 123).BindingKey,
                Subscription.Matching<StrangeRoutableMessage>(x => x.Code == "456").BindingKey,
                Subscription.Matching<StrangeRoutableMessage>(x => x.Id == 123 && x.Code == "456").BindingKey,
                Subscription.Any<StrangeRoutableMessage>().BindingKey,
            };

            // Act
            var partsForId = BindingKeyUtil.GetPartsForMember(MessageUtil.TypeId<StrangeRoutableMessage>(), nameof(StrangeRoutableMessage.Id), bindingKeys);
            var partsForCode = BindingKeyUtil.GetPartsForMember(MessageUtil.TypeId<StrangeRoutableMessage>(), nameof(StrangeRoutableMessage.Code), bindingKeys);

            // Assert
            partsForId.ShouldBeEquivalentTo(BindingKeyPart.Parse("123"), BindingKeyPart.Star, BindingKeyPart.Parse("123"), BindingKeyPart.Star);
            partsForCode.ShouldBeEquivalentTo(BindingKeyPart.Star, BindingKeyPart.Parse("456"), BindingKeyPart.Parse("456"), BindingKeyPart.Star);
        }

        [Routable]
        public class StrangeRoutableMessage : IMessage
        {
            [RoutingPosition(2)]
            public int Id { get; }

            [RoutingPosition(4)]
            public string Code { get; }

            public string Value { get; }

            public StrangeRoutableMessage(int id, string code, string value)
            {
                Id = id;
                Code = code;
                Value = value;
            }
        }
    }
}
