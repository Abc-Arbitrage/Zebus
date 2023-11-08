using System;
using System.Text.RegularExpressions;

namespace Abc.Zebus.Transport;

internal readonly struct ZmqEndPoint
{
    private static readonly Regex _endpointRegex = new(@"^tcp://(?<host>\*|[0-9a-zA-Z_.-]+):(?<port>\*|[0-9]+)/?$", RegexOptions.IgnoreCase);

    private readonly string? _value;

    public ZmqEndPoint(string? value)
        => _value = value;

    public override string ToString()
        => _value ?? "tcp://*:*";

    public static (string host, string port) Parse(string? endpoint)
    {
        var match = _endpointRegex.Match(endpoint ?? string.Empty);
        return match.Success
            ? (match.Groups["host"].Value, match.Groups["port"].Value)
            : throw new InvalidOperationException($"Invalid endpoint: {endpoint}");
    }
}
