using System.Linq;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch.Pipes
{
    [TestFixture]
    public class PipeManagerTests
    {
        private PipeManager _pipeManager;
        private FakePipeSource _pipeSource;

        [SetUp]
        public void SetUp()
        {
            _pipeSource = new FakePipeSource();
            _pipeManager = new PipeManager(new[] { _pipeSource });
        }

        [Test]
        public void should_build_invocation_with_pipe()
        {
            var pipe = new FakePipe { IsAutoEnabled = true };
            _pipeSource.Pipes.Add(pipe);

            var message = new FakeCommand(123);
            var messageContext = MessageContext.CreateTest("u.name");
            var invoker = new TestMessageHandlerInvoker(typeof(FakeCommandHandler), typeof(FakeCommand));
            var invocation = _pipeManager.BuildPipeInvocation(invoker, message, messageContext);

            invocation.Pipes.Single().ShouldEqual(pipe);
        }

        [Test]
        public void should_load_pipes()
        {
            var pipe = new FakePipe { IsAutoEnabled = false };
            _pipeSource.Pipes.Add(pipe);

            _pipeManager.EnablePipe(pipe.Name);
            var enabledPipes = _pipeManager.GetEnabledPipes(typeof(FakeCommandHandler));
            enabledPipes.ShouldBeEquivalentTo(new [] { pipe });
        }

        [Test]
        public void should_sort_pipes()
        {
            _pipeSource.Pipes.Add(new FakePipe { Name = "Fake1", Priority = 1, IsAutoEnabled = true});
            _pipeSource.Pipes.Add(new FakePipe { Name = "Fake2", Priority = 100, IsAutoEnabled = true });
            _pipeSource.Pipes.Add(new FakePipe { Name = "Fake3", Priority = 50, IsAutoEnabled = true });

            var enabledPipes = _pipeManager.GetEnabledPipes(typeof(FakeCommandHandler)).AsList();
            enabledPipes.Count.ShouldEqual(3);
            enabledPipes.Select(x => x.Priority).Reverse().ShouldBeOrdered();
        }

        [Test]
        public void should_disable_pipe()
        {
            var pipe = new FakePipe { IsAutoEnabled = true };
            _pipeSource.Pipes.Add(pipe);

            var enabledPipes = _pipeManager.GetEnabledPipes(typeof(FakeCommandHandler));
            enabledPipes.ShouldContain(pipe);

            _pipeManager.DisablePipe(pipe.Name);
            enabledPipes = _pipeManager.GetEnabledPipes(typeof(FakeCommandHandler));
            enabledPipes.ShouldNotContain(pipe);
        }

        [Test]
        public void should_enable_pipe()
        {
            var pipe = new FakePipe { IsAutoEnabled = false };
            _pipeSource.Pipes.Add(pipe);

            var pipesBeforeEnable = _pipeManager.GetEnabledPipes(typeof(FakeCommandHandler));
            pipesBeforeEnable.ShouldNotContain(pipe);

            _pipeManager.EnablePipe(pipe.Name);
            var pipesAfterEnable = _pipeManager.GetEnabledPipes(typeof(FakeCommandHandler));

            pipesAfterEnable.ShouldContain(pipe);
        }

        private class FakeCommandHandler : IMessageHandler<FakeCommand>
        {
            public void Handle(FakeCommand message)
            {
            }
        }
    }
}