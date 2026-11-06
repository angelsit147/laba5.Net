using Business.Validation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace WebApi.ErrorHandling;

public class ApiExceptionHandler : IExceptionHandler
{
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;
    private const int SqliteConstraintForeignKey = 787;
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintTrigger = 1811;
    private const int SqliteConstraintUnique = 2067;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var status = StatusCodes.Status500InternalServerError;
        var code = "internal_error";
        var detail = "An unexpected error occurred.";
        if (exception is RestaurantException restaurant)
        {
            status = restaurant.ErrorType switch
            {
                RestaurantErrorType.Validation => StatusCodes.Status400BadRequest,
                RestaurantErrorType.NotFound => StatusCodes.Status404NotFound,
                RestaurantErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };
            code = restaurant.Code;
            detail = restaurant.Message;
        }
        else
        {
            var sqlite = exception as SqliteException ?? (exception as DbUpdateException)?.InnerException as SqliteException;
            if (sqlite?.SqliteErrorCode is SqliteBusy or SqliteLocked)
            {
                status = StatusCodes.Status409Conflict;
                code = "storage_busy";
                detail = "Storage is busy. Try the operation again later.";
            }
            else if (sqlite?.SqliteExtendedErrorCode is SqliteConstraintForeignKey or SqliteConstraintUnique or SqliteConstraintPrimaryKey
                || IsRestrictForeignKey(sqlite))
            {
                status = StatusCodes.Status409Conflict;
                code = sqlite!.SqliteExtendedErrorCode is SqliteConstraintForeignKey or SqliteConstraintTrigger
                    ? "foreign_key_conflict" : "unique_conflict";
                detail = "The operation conflicts with related or existing data.";
            }
        }

        await Results.Problem(statusCode: status, title: "Request failed", detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(httpContext);
        return true;
    }

    private static bool IsRestrictForeignKey(SqliteException? exception) =>

        exception?.SqliteExtendedErrorCode == SqliteConstraintTrigger
        && exception.Message.Equals("SQLite Error 19: 'FOREIGN KEY constraint failed'.", StringComparison.Ordinal);
}
