using System;
using System.Text.RegularExpressions;

namespace Abc.Zebus.Transport;

internal abstract record ZmqPort
{
    private static readonly Regex _regex = new(@"^(?<port>\*|(?<single>[0-9]+)|\[(?<rangeStart>[0-9]+)\.\.(?<rangeEnd>[0-9]+)\])/?$", RegexOptions.IgnoreCase);

    private ZmqPort()
    {
    }

    public abstract override string ToString();

    internal sealed record Wildcard : ZmqPort
    {
        public override string ToString() => "*";
    }

    internal sealed record Single : ZmqPort
    {
        public Single(ushort value)
        {
            Value = value;
        }

        public ushort Value { get; }

        public override string ToString() => Value.ToString();
    }

    internal sealed record Range : ZmqPort
    {
        public Range(ushort start, ushort end)
        {
            if (start > end)
                throw new ArgumentException("The start port must not be greater than the end port.", nameof(start));

            Start = start;
            End = end;
        }

        public ushort Start { get; }
        public ushort End { get; }

        public override string ToString() => $"[{Start}..{End}]";
    }

    internal static ZmqPort Parse(string str)
    {
        var match = _regex.Match(str);
        if (!match.Success)
            throw new ArgumentException($"Invalid port {str}");

        if (match.Groups["port"].Value == "*")
            return new Wildcard();

        if (match.Groups["single"].Success)
            return new Single(ParsePort(match.Groups["single"].Value));

        var rangeStart = match.Groups["rangeStart"].Value;
        var rangeEnd = match.Groups["rangeEnd"].Value;

        return new Range(ParsePort(rangeStart), ParsePort(rangeEnd));

        static ushort ParsePort(string portStr)
        {
            if (!ushort.TryParse(portStr, out var port))
                throw new ArgumentException($"Invalid port {portStr}, expected integer in the range [{ushort.MinValue}..{ushort.MaxValue}]");

            return port;
        }
    }
}
