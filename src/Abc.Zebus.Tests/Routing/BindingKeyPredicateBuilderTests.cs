using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Routing
{
    [TestFixture]
    public class BindingKeyPredicateBuilderTests
    {
        private BindingKeyPredicateBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _builder = new BindingKeyPredicateBuilder();
        }

        [TestCaseSource(nameof(TestSources))]
        public void should_match_valid_message(ExpectedResult expectedResult)
        {
            // Act
            var predicate = _builder.GetPredicate(typeof(FakeRoutableCommand), expectedResult.BindingKey);

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
    }

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
}
