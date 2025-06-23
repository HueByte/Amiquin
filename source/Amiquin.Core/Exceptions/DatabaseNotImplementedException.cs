namespace Amiquin.Core.Exceptions;

public class DatabaseNotImplementedException : Exception
{
    public DatabaseNotImplementedException()
        : base("The database operation is not implemented for the current database mode.")
    {
    }

    public DatabaseNotImplementedException(string message)
        : base(message)
    {
    }

    public DatabaseNotImplementedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}