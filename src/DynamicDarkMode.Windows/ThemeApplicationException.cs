namespace DynamicDarkMode.Windows;

internal sealed class ThemeApplicationException : Exception
{
    public ThemeApplicationException(
        string message,
        Exception operationException,
        IReadOnlyCollection<Exception> recoveryExceptions)
        : base(
            message,
            recoveryExceptions.Count == 0
                ? operationException
                : new AggregateException(
                    "The theme operation failed and one or more recovery operations also failed.",
                    new[] { operationException }.Concat(recoveryExceptions)))
    {
        OperationException = operationException;
        RecoveryExceptions = recoveryExceptions;
    }

    public Exception OperationException { get; }

    public IReadOnlyCollection<Exception> RecoveryExceptions { get; }
}
