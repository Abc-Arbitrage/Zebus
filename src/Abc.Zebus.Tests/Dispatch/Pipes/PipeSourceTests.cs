using System.Linq;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Moq;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Dispatch.Pipes
{
    [TestFixture]
    public class PipeSourceTests
    {
        [Test]
        public void should_create_pipe()
        {
            var pipe = new TestPipe();
            var containerMock = new Mock<IContainer>();
            containerMock.Setup(x => x.GetInstance<TestPipe>()).Returns(pipe);

            var source = new PipeSource<TestPipe>(containerMock.Object);

            var pipes = source.GetPipes(typeof(FakeMessageHandler));

            pipes.Single().ShouldEqual(pipe);
        }


        public class FakeMessageHandler : IMessageHandler<FakeCommand>
        {
            public void Handle(FakeCommand message)
            {
            }
        }
    }
}