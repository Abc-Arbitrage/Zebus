namespace Abc.Zebus
{
    public class CommandResult
    {
        public CommandResult(int errorCode, object response)
        {
            ErrorCode = errorCode;
            Response = response;
        }

        public int ErrorCode { get; private set; }
        public object Response { get; private set; }

        public bool IsSuccess
        {
            get { return ErrorCode == 0; }
        }
    }
}