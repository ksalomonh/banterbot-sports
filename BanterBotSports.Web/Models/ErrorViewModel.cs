using Microsoft.AspNetCore.Http;

namespace BanterBotSports.Web.Models;

public record ErrorViewModel
{
    public string? RequestId { get; init; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int StatusCode { get; init; } = StatusCodes.Status500InternalServerError;
}
