using System;
using System.Linq;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch.Pipes
{
    [TestFixture]
    public class AttributePipeSourceTests
    {
        [Test]
        public void should_create_pipe_from_attribute()
        {
            var pipe = new TestPipe();
            var containerMock = new Mock<IDependencyInjectionContainer>();
            containerMock.Setup(x => x.GetInstance(typeof(TestPipe))).Returns(pipe);

            var source = new AttributePipeSource(containerMock.Object);

            var pipes = source.GetPipes(typeof(FakeMessageHandler));

            pipes.Single().ShouldEqual(pipe);
        }

        [FakePipe, Serializable]
        public class FakeMessageHandler : IMessageHandler<FakeCommand>
        {
            public void Handle(FakeCommand message)
            {
            }
        }

        public class FakePipeAttribute : PipeAttribute
        {
            public FakePipeAttribute() : base(typeof(TestPipe))
            {
            }
        }
    }
}
