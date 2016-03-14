using System.Threading.Tasks;
using Abc.Zebus.Persistence.Initialization;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Testing.Extensions;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Initialization
{
    [TestFixture]
    public class MessageReplayerInitializerTests
    {
        private MessageReplayerInitializer _initializer;
        private Mock<IMessageReplayerRepository> _messageReplayerRepositoryMock;

        [SetUp]
        public void Setup()
        {
            var configurationMock = new Mock<IPersistenceConfiguration>();
            configurationMock.SetupGet(conf => conf.SafetyPhaseDuration).Returns(30.Seconds());

            _messageReplayerRepositoryMock = new Mock<IMessageReplayerRepository>();
            _initializer = new MessageReplayerInitializer(configurationMock.Object, _messageReplayerRepositoryMock.Object);
        }

        [Test]
        public void should_deactivate_replay_creation()
        {
            _initializer.BeforeStop();

            _messageReplayerRepositoryMock.Verify(x => x.DeactivateMessageReplayers());
        }

        [Test]
        public void should_wait_for_replayers()
        {
            var hasActiveMessageReplayers = true;
            _messageReplayerRepositoryMock.Setup(x => x.HasActiveMessageReplayers()).Returns(() => hasActiveMessageReplayers);

            var task = Task.Factory.StartNew(() => _initializer.BeforeStop());
            task.Wait(300.Milliseconds()).ShouldBeFalse();

            hasActiveMessageReplayers = false;
            task.Wait(300.Milliseconds()).ShouldBeTrue();
        }
    }
}