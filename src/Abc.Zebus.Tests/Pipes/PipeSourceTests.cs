using System.Linq;
using Abc.Zebus.Scan.Pipes;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Moq;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Pipes
{
    [TestFixture]
    public class PipeSourceTests
    {
        [Test]
        public void should_create_pipe()
        {
            var pipe = new FakePipe();
            var containerMock = new Mock<IContainer>();
            containerMock.Setup(x => x.GetInstance<FakePipe>()).Returns(pipe);

            var source = new PipeSource<FakePipe>(containerMock.Object);

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