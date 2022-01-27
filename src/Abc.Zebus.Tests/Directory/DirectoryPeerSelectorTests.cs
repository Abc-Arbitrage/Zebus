using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Directory
{
    [TestFixture]
    public class DirectoryPeerSelectorTests
    {
        private BusConfiguration _configuration;
        private DirectoryPeerSelector _selector;

        [SetUp]
        public void SetUp()
        {
            _configuration = new BusConfiguration(new[] { "tcp://dir1:129", "tcp://dir2:129" });
            _selector = new DirectoryPeerSelector(_configuration);
        }

        [Test]
        public void should_get_peers_randomly()
        {
            // Arrange
            var results = new List<Peer[]>();

            // Act
            for (int i = 0; i < 25; i++)
            {
                results.Add(_selector.GetPeers().ToArray());
            }

            // Assert
            var firstResult = results[0];
            results.Any(x => x[0] != firstResult[0]).ShouldBeTrue();
        }
    }
}
