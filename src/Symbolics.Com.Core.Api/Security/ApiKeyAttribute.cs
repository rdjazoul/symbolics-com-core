using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ApiKeyAttribute>? _logger;

    public ApiKeyAttribute(ApiKeyScope scope)
    {
        _scope = scope;
    }

    private ApiKeyAttribute(
        ApiKeyScope scope,
        IOptionsMonitor<AdminSettings> adminOptions,
        IOptionsMonitor<ServiceSettings> serviceOptions,
        ILogger<ApiKeyAttribute> logger)
    {
        _scope = scope;
        _adminOptions = adminOptions;
        _serviceOptions = serviceOptions;
        _logger = logger;
    }

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var adminOptions = serviceProvider.GetRequiredService<IOptionsMonitor<AdminSettings>>();
        var serviceOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceSettings>>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApiKeyAttribute>>();
        return new ApiKeyAttribute(_scope, adminOptions, serviceOptions, logger);
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
            var isNullOrWhiteSpace = string.IsNullOrWhiteSpace(configuredKey);
            _logger?.LogWarning("API key not configured for scope {Scope}. IsNullOrWhiteSpace: {IsNullOrWhiteSpace}", _scope, isNullOrWhiteSpace);
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(headerName, out var providedKey) ||
            !string.Equals(providedKey.ToString(), configuredKey, StringComparison.Ordinal))
        {
            var maskedConfiguredKey = configuredKey.Length > 4 ? $"*****{configuredKey[^4..]}" : configuredKey;
            var providedKeyStr = providedKey.ToString();
            var maskedProvidedKey = providedKeyStr.Length > 4 ? $"*****{providedKeyStr[^4..]}" : providedKeyStr;
            
            _logger?.LogWarning("API key mismatch for scope {Scope}. Header: {HeaderName}, Configured: {MaskedConfiguredKey}, Provided: {MaskedProvidedKey}, HeaderExists: {HeaderExists}", 
                _scope, headerName, maskedConfiguredKey, maskedProvidedKey, context.HttpContext.Request.Headers.ContainsKey(headerName));
            
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
