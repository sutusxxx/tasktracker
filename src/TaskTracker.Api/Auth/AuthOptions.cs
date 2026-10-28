using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Api.Auth;

internal sealed class AuthOptions
{
    [Required] public string Authority { get; init; } = "";
    [Required] public string Audience { get; init; } = "";
    public bool RequireHttpsMetadata { get; init; } = true; 
}