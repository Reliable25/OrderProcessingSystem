namespace OrderProcessing.Application.Common;

/// <summary>
/// Discriminated union result type to avoid exception-driven flow for expected failures.
/// Provides explicit success/failure states with strongly-typed error information.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    private Result(string errorMessage, string errorCode)
    {
        IsSuccess = false;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string errorMessage, string errorCode = "GENERAL_ERROR") => new(errorMessage, errorCode);
}

public static class ErrorCodes
{
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string DuplicateOrder = "DUPLICATE_ORDER";
    public const string InvalidOperation = "INVALID_OPERATION";
}