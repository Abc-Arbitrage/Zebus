using System;
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
    public class AttributePipeSourceTests
    {
        [Test]
        public void should_create_pipe_from_attribute()
        {
            var pipe = new FakePipe();
            var containerMock = new Mock<IContainer>();
            containerMock.Setup(x => x.GetInstance(typeof(FakePipe))).Returns(pipe);

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
            public FakePipeAttribute() : base(typeof(FakePipe))
            {
            }
        }
    }
}