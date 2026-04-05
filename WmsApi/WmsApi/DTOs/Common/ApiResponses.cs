namespace WmsApi.DTOs;

public record ApiError(string Error, string? Detail = null);

public record ApiSuccess(bool Success, string Message);
