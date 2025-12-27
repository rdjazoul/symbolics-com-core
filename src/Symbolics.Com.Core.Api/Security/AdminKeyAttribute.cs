using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Api.Settings;

namespace Symbolics.Com.Core.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminKeyAttribute : Attribute, IAsyncActionFilter, IFilterFactory
{
    private const string AdminKeyHeader = "X-Admin-Key";
    private readonly IOptionsMonitor<AdminSettings>? _optionsMonitor;

    public AdminKeyAttribute()
    {
    }

    private AdminKeyAttribute(IOptionsMonitor<AdminSettings> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<AdminSettings>>();
        return new AdminKeyAttribute(optionsMonitor);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuredKey = _optionsMonitor?.CurrentValue.AdminKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(AdminKeyHeader, out var providedKey) ||
            !string.Equals(providedKey.ToString(), configuredKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
