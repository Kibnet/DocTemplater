using System.Reflection;
using System.Threading;
using ReactiveUI;
using Velopack;
using Velopack.Sources;

namespace DocTemplater.Services;

public interface IApplicationUpdateAdapter
{
    bool IsInstalled { get; }
    string Version { get; }
    bool HasPendingUpdate { get; }
    Task<bool> CheckAsync();
    Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken);
    void ApplyAndRestart();
}

/// <summary>The only owner of Velopack objects; the UI never receives native package types.</summary>
public sealed class VelopackUpdateAdapter : IApplicationUpdateAdapter
{
    private readonly UpdateManager _manager;
    private UpdateInfo? _available;
    public VelopackUpdateAdapter()
    {
#if UPDATE_FIXTURE
        _manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(Environment.GetEnvironmentVariable("DOCTEMPLATER_FIXTURE_FEED")!)));
#else
        _manager = new UpdateManager(new GithubSource("https://github.com/Kibnet/DocTemplater", accessToken: null, prerelease: false));
#endif
    }
    public bool IsInstalled => _manager.IsInstalled;
    public string Version => _manager.CurrentVersion?.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
    public bool HasPendingUpdate => _manager.IsInstalled && _manager.UpdatePendingRestart != null;
    public async Task<bool> CheckAsync()
    {
        _available = await _manager.CheckForUpdatesAsync();
        return _available != null;
    }
    public Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken) =>
        _manager.DownloadUpdatesAsync(_available ?? throw new InvalidOperationException("Нет обновления."), progress, cancellationToken);
    public void ApplyAndRestart() => _manager.ApplyUpdatesAndRestart(_manager.UpdatePendingRestart
        ?? throw new InvalidOperationException("Готовый пакет не найден. Повторите проверку."));
}

public enum ApplicationUpdateState { Disabled, Idle, Checking, Downloading, Ready, Applying, Error }

public sealed class ApplicationUpdateService : ReactiveObject, IDisposable
{
    private readonly IApplicationUpdateAdapter _adapter;
    private readonly Action<Action> _dispatch;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private Task? _operation;
    private ApplicationUpdateState _state;
    private string _status;
    public ApplicationUpdateService(IApplicationUpdateAdapter adapter, Action<Action>? dispatch = null)
    {
        _adapter = adapter;
        _dispatch = dispatch ?? (action => action());
        _state = !adapter.IsInstalled ? ApplicationUpdateState.Disabled : adapter.HasPendingUpdate ? ApplicationUpdateState.Ready : ApplicationUpdateState.Idle;
        _status = _state switch
        {
            ApplicationUpdateState.Disabled => "Обновления доступны в установленной версии",
            ApplicationUpdateState.Ready => "Обновление готово к установке",
            _ => "Проверка обновлений при запуске"
        };
    }
    public string VersionLabel => "Версия " + _adapter.Version;
    public ApplicationUpdateState State => _state;
    public string Status => _status;
    public bool IsReady => _state == ApplicationUpdateState.Ready;
    public bool CanCheck => _state is ApplicationUpdateState.Idle or ApplicationUpdateState.Error;
    public bool IsBusy => _state is ApplicationUpdateState.Checking or ApplicationUpdateState.Downloading or ApplicationUpdateState.Applying;

    private void Set(ApplicationUpdateState state, string status) => _dispatch(() =>
    {
        _state = state; _status = status;
        this.RaisePropertyChanged(nameof(State)); this.RaisePropertyChanged(nameof(Status));
        this.RaisePropertyChanged(nameof(IsReady)); this.RaisePropertyChanged(nameof(CanCheck)); this.RaisePropertyChanged(nameof(IsBusy));
    });

    public Task CheckAndDownloadAsync()
    {
        lock (_gate)
        {
            if (_operation is { IsCompleted: false }) return _operation;
            if (!CanCheck) return Task.CompletedTask;
            return _operation = CheckCoreAsync();
        }
    }
    private async Task CheckCoreAsync()
    {
        try
        {
            Set(ApplicationUpdateState.Checking, "Проверка обновлений…");
            if (!await _adapter.CheckAsync()) { Set(ApplicationUpdateState.Idle, "Установлена последняя версия"); return; }
            Set(ApplicationUpdateState.Downloading, "Скачивание обновления: 0%");
            await _adapter.DownloadAsync(percent => Set(ApplicationUpdateState.Downloading, $"Скачивание обновления: {percent}%"), _lifetime.Token);
            Set(ApplicationUpdateState.Ready, "Обновление готово к установке");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Set(ApplicationUpdateState.Error, "Не удалось обновить: " + ex.Message + " Можно повторить проверку."); }
    }

    public void ReportBlocked(string message) => Set(ApplicationUpdateState.Ready, message);
    public void ApplyAndRestart()
    {
        if (!IsReady) return;
        Set(ApplicationUpdateState.Applying, "Установка и перезапуск…");
        try { _adapter.ApplyAndRestart(); }
        catch (Exception ex) { Set(ApplicationUpdateState.Ready, "Не удалось установить: " + ex.Message + " Повторите установку."); }
    }
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); }
}
