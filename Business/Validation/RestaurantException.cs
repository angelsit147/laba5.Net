namespace Business.Validation;

public enum RestaurantErrorType
{
    Validation,
    NotFound,
    Conflict
}

public sealed class RestaurantException : Exception
{
    public RestaurantException(RestaurantErrorType errorType, string code, string message) : base(message)
    {
        ErrorType = errorType;
        Code = code;
    }

    public RestaurantErrorType ErrorType { get; }
    public string Code { get; }
}
