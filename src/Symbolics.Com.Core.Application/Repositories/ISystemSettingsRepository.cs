namespace Symbolics.Com.Core.Application.Repositories;

public interface ISystemSettingsRepository
{
    Task<int> GetIntSettingAsync(string key, int defaultValue, CancellationToken cancellationToken = default);
}
