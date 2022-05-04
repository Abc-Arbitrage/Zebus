using System;
using System.Linq;

namespace Abc.Zebus.Routing
{
    /// <summary>
    /// Stores the routing members values of routable messages.
    /// </summary>
    public readonly struct RoutingContent
    {
        public static readonly RoutingContent Empty = new RoutingContent();

        private readonly RoutingContentValue[]? _members;

        public RoutingContent(params RoutingContentValue[]? members)
        {
            _members = members;
        }

        public int PartCount
            => _members?.Length ?? 0;

        public bool IsEmpty
            => _members == null || _members.Length == 0;

        public RoutingContentValue this[int index]
            => _members != null ? _members[index] : throw new ArgumentOutOfRangeException();

        public static RoutingContent FromValues(params string[] values)
            => new RoutingContent(values.Select(x => new RoutingContentValue(x)).ToArray());
    }
}
