using Abc.Zebus.Routing;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [Routable]
    public class RoutableCommand : ICommand
    {
        [RoutingPosition(1)]
        public readonly string Key;

        public RoutableCommand(string key)
        {
            Key = key;
        }
    }
}