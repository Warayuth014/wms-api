using Microsoft.AspNetCore.Http;

namespace WmsApi.Services.Common;

public sealed class ServiceResult
{
    public int StatusCode { get; }
    public object Payload { get; }

    private ServiceResult(int statusCode, object payload)
    {
        StatusCode = statusCode;
        Payload = payload;
    }

    public static ServiceResult Ok(object payload) =>
        new(StatusCodes.Status200OK, payload);

    public static ServiceResult BadRequest(object payload) =>
        new(StatusCodes.Status400BadRequest, payload);

    public static ServiceResult NotFound(object payload) =>
        new(StatusCodes.Status404NotFound, payload);
}
