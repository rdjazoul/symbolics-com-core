using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Api.Settings;

namespace Symbolics.Com.Core.Api.Security;

public enum ApiKeyScope
{
    Admin,
    Service
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiKeyAttribute : Attribute, IAsyncActionFilter, IFilterFactory
{
    private const string AdminKeyHeader = "X-Admin-Key";
    private const string ServiceKeyHeader = "X-Service-Key";
    private readonly ApiKeyScope _scope;
    private readonly IOptionsMonitor<AdminSettings>? _adminOptions;
    private readonly IOptionsMonitor<ServiceSettings>? _serviceOptions;

    public ApiKeyAttribute(ApiKeyScope scope)
    {
        _scope = scope;
    }

    private ApiKeyAttribute(
        ApiKeyScope scope,
        IOptionsMonitor<AdminSettings> adminOptions,
        IOptionsMonitor<ServiceSettings> serviceOptions)
    {
        _scope = scope;
        _adminOptions = adminOptions;
        _serviceOptions = serviceOptions;
    }

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var adminOptions = serviceProvider.GetRequiredService<IOptionsMonitor<AdminSettings>>();
        var serviceOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceSettings>>();
        return new ApiKeyAttribute(_scope, adminOptions, serviceOptions);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var (headerName, configuredKey) = _scope switch
        {
            ApiKeyScope.Admin => (AdminKeyHeader, _adminOptions?.CurrentValue.AdminKey),
            ApiKeyScope.Service => (ServiceKeyHeader, _serviceOptions?.CurrentValue.ServiceKey),
            _ => throw new ArgumentOutOfRangeException(nameof(_scope), _scope, "Unsupported API key scope.")
        };

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(headerName, out var providedKey) ||
            !string.Equals(providedKey.ToString(), configuredKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
