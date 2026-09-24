using System.Globalization;
using DesktopBoard.Core.Interfaces;

namespace DesktopBoard.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public SettingsService(ISettingsRepository repository) => _repository = repository;

    public event EventHandler<string>? SettingChanged;

    public async Task LoadAsync()
    {
        var all = await _repository.GetAllAsync().ConfigureAwait(false);
        lock (_gate)
        {
            _cache.Clear();
            foreach (var s in all) _cache[s.Key] = s.Value;
        }
    }

    public string? GetString(string key, string? defaultValue = null)
    {
        lock (_gate) return _cache.TryGetValue(key, out var v) && v is not null ? v : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        var v = GetString(key);
        return v is null ? defaultValue : v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
    }

    public double GetDouble(string key, double defaultValue = 0)
    {
        var v = GetString(key);
        return v is not null && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : defaultValue;
    }

    public async Task SetAsync(string key, string? value)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var existing) && existing == value) return;
            _cache[key] = value;
        }
        await _repository.SetAsync(key, value).ConfigureAwait(false);
        SettingChanged?.Invoke(this, key);
    }

    public Task SetAsync(string key, bool value) => SetAsync(key, value ? "true" : "false");

    public Task SetAsync(string key, double value) => SetAsync(key, value.ToString("R", CultureInfo.InvariantCulture));
}
