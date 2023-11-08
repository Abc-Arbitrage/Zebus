namespace Abc.Zebus;

internal class ErrorStatus
{
    public static readonly ErrorStatus NoError = new(0, null);
    public static readonly ErrorStatus UnknownError = new(1, null);

    public int Code { get; }
    public string? Message { get; }

    public ErrorStatus(int code, string? message)
    {
        Code = code;
        Message = message;
    }

    public override string ToString() => $"{Code}: {Message}";
}
