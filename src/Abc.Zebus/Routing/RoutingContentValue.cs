using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Abc.Zebus.Routing;

/// <summary>
/// Stores the routing member value of a routable message.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly struct RoutingContentValue : IEquatable<RoutingContentValue>
{
    [FieldOffset(0)]
    private readonly string? _value;
    [FieldOffset(0)]
    private readonly string?[] _values;
    [FieldOffset(8)]
    private readonly bool _isCollection;

    public RoutingContentValue(string? value)
    {
        _values = null!;
        _value = value;
        _isCollection = false;
    }

    public RoutingContentValue(string?[] values)
    {
        _value = null;
        _values = values;
        _isCollection = true;
    }

    public string?[] GetValues()
    {
        if (_isCollection)
            return _values;

        return new[] { _value };
    }

    public bool Matches(string? s)
    {
        return _isCollection ? _values.Contains(s) : _value == s;
    }

    public bool Equals(RoutingContentValue other)
    {
        return _isCollection == other._isCollection && GetValues().SequenceEqual(other.GetValues());
    }

    public override string? ToString()
    {
        return _isCollection
            ? "[" + string.Join(", ", _values) + "]"
            : _value;
    }

    internal bool IsSingle => !_isCollection;
    internal string? SingleValue => _value;
}
