namespace PackagingInspectionTools.Core.Network
{
    public sealed class OperationResult
    {
        private OperationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }

        public string Message { get; }

        public static OperationResult Success(string message)
        {
            return new OperationResult(true, message);
        }

        public static OperationResult Failure(string message)
        {
            return new OperationResult(false, message);
        }
    }
}
