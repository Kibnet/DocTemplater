using DocTemplater.Services;

namespace DocTemplater.Tests.Services;

public class ApplicationUpdateTests
{
    private sealed class Adapter : IApplicationUpdateAdapter
    {
        public bool IsInstalled { get; set; } = true;
        public string Version => "0.1.0";
        public bool HasPendingUpdate { get; set; }
        public int Checks, Downloads, Applies;
        public bool Fail;
        public TaskCompletionSource<bool> CheckResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> CheckAsync() { Checks++; return CheckResult.Task; }
        public Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken)
        {
            Downloads++;
            if (Fail) throw new IOException("checksum");
            progress(57); HasPendingUpdate = true; return Task.CompletedTask;
        }
        public void ApplyAndRestart() { Applies++; if (Fail) throw new IOException("locked"); }
    }

    [Fact]
    public async Task ConcurrentChecksShareOneDownloadAndApplyOnce()
    {
        var adapter = new Adapter();
        using var service = new ApplicationUpdateService(adapter);
        var first = service.CheckAndDownloadAsync();
        var second = service.CheckAndDownloadAsync();
        Assert.Same(first, second);
        adapter.CheckResult.SetResult(true);
        await first;
        Assert.Equal(1, adapter.Checks); Assert.Equal(1, adapter.Downloads);
        Assert.True(service.IsReady);
        service.ApplyAndRestart(); service.ApplyAndRestart();
        Assert.Equal(1, adapter.Applies);
    }

    [Fact]
    public async Task DownloadFailureIsVisibleAndRetryRecovers()
    {
        var adapter = new Adapter { Fail = true };
        adapter.CheckResult.SetResult(true);
        using var service = new ApplicationUpdateService(adapter);
        await service.CheckAndDownloadAsync();
        Assert.Equal(ApplicationUpdateState.Error, service.State);
        Assert.Contains("checksum", service.Status);
        adapter.Fail = false;
        await service.CheckAndDownloadAsync();
        Assert.True(service.IsReady);
        adapter.Fail = true;
        service.ApplyAndRestart();
        Assert.True(service.IsReady);
        Assert.Contains("locked", service.Status);
    }

    [Fact]
    public async Task DevelopmentBuildDoesNotCallNetwork()
    {
        var adapter = new Adapter { IsInstalled = false };
        using var service = new ApplicationUpdateService(adapter);
        await service.CheckAndDownloadAsync();
        Assert.Equal(ApplicationUpdateState.Disabled, service.State);
        Assert.Equal(0, adapter.Checks);
    }

    [Fact]
    public async Task PendingPackageSurvivesRestartWithoutNetwork()
    {
        var adapter = new Adapter { HasPendingUpdate = true };
        using var service = new ApplicationUpdateService(adapter);
        Assert.True(service.IsReady);
        await service.CheckAndDownloadAsync();
        Assert.Equal(0, adapter.Checks);
    }
}
