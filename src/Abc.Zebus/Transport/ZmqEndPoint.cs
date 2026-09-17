using System;
using System.Text.RegularExpressions;

namespace Abc.Zebus.Transport;

internal readonly struct ZmqEndPoint
{
    private static readonly Regex _endpointRegex = new(@"^tcp://(?<host>\*|[0-9a-zA-Z_.-]+):(?<port>[^/]+)/?$", RegexOptions.IgnoreCase);

    private readonly string? _value;

    public ZmqEndPoint(string? value)
        => _value = value;

    public override string ToString()
        => _value ?? "tcp://*:*";

    public static (string host, ZmqPort port) Parse(string? endpoint)
    {
        var match = _endpointRegex.Match(endpoint ?? string.Empty);
        if (!match.Success)
            throw new InvalidOperationException($"Invalid endpoint: {endpoint}");

        try
        {
            var port = ZmqPort.Parse(match.Groups["port"].Value);
            return (match.Groups["host"].Value, port);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid endpoint: {endpoint}", ex);
        }
    }
}
