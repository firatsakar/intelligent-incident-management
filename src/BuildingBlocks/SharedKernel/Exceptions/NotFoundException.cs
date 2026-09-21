namespace BuildingBlocks.SharedKernel.Exceptions;

// Base for "the thing you asked for does not exist". Services derive their own exceptions from it
// so the shared exception handler can map them to 404 without knowing any service's types.
public abstract class NotFoundException : Exception
{
    protected NotFoundException(string message)
        : base(message) { }
}
