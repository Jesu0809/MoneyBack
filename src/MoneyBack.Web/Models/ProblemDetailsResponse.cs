namespace MoneyBack.Web.Models;

public record ProblemDetailsResponse(
    string? Title,
    int? Status,
    Dictionary<string, string[]>? Errors);
