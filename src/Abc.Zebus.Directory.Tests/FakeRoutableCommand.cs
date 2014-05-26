using Abc.Zebus.Routing;

namespace Abc.Zebus.Directory.Tests
{
    [Routable]
    public class FakeRoutableCommand : ICommand
    {
        [RoutingPosition(1)]
        public readonly int Id;

        [RoutingPosition(2)]
        public readonly string Name;
        
        FakeRoutableCommand()
        {
        }

        public FakeRoutableCommand(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}