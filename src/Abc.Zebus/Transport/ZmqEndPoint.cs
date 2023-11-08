namespace Abc.Zebus.Transport;

internal readonly struct ZmqEndPoint
{
    private readonly string? _value;

    public ZmqEndPoint(string? value)
        => _value = value;

    public override string ToString()
        => _value ?? "tcp://*:*";
}
