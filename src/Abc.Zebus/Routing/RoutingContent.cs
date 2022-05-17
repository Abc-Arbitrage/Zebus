using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zebus.Routing
{
    /// <summary>
    /// Stores the routing members values of routable messages.
    /// </summary>
    public readonly struct RoutingContent : IEnumerable<RoutingContentValue>
    {
        public static readonly RoutingContent Empty = new RoutingContent();

        private readonly RoutingContentValue[]? _members;

        public RoutingContent(params RoutingContentValue[]? members)
        {
            _members = members;
        }

        private RoutingContentValue[] MembersOrEmpty => _members ?? Array.Empty<RoutingContentValue>();

        public int PartCount => MembersOrEmpty.Length;

        public bool IsEmpty => MembersOrEmpty.Length == 0;

        public RoutingContentValue this[int index] => MembersOrEmpty[index];

        public static RoutingContent FromValues(params string[] values) => new(values.Select(x => new RoutingContentValue(x)).ToArray());

        public IEnumerator<RoutingContentValue> GetEnumerator()
        {
            foreach (var value in MembersOrEmpty)
            {
                yield return value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
