namespace BreedersTestTask.Exceptions;

public class DomainException : AppException
{
    public DomainException(string message) : base(message)
    {
    }
}