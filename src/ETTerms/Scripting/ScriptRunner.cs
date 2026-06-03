using ETTerms.Sessions;

namespace ETTerms.Scripting;

/// <summary>
/// 非同步驅動 <see cref="TTLInterpreter"/>：背景執行緒執行腳本，
/// 轉發 StatusChanged / Output，並在結束時觸發 Finished。可隨時 <see cref="Cancel"/>。
/// 事件可能在背景執行緒觸發，UI 訂閱者需自行 marshal 回 UI thread。
/// </summary>
public sealed class ScriptRunner
{
    private TTLInterpreter? _interp;

    /// <summary>(檔名, 行號, 指令) 進度。</summary>
    public event Action<string, int, string>? StatusChanged;

    /// <summary>輸出訊息（log / 提示 / 錯誤）。</summary>
    public event Action<string>? Output;

    /// <summary>結束：(成功, 訊息)。</summary>
    public event Action<bool, string>? Finished;

    public bool IsRunning { get; private set; }

    public async Task RunAsync(string content, string fileName, ISessionChannel channel)
    {
        if (IsRunning) return;
        IsRunning = true;

        var interp = new TTLInterpreter(channel);
        interp.StatusChanged += (f, l, c) => StatusChanged?.Invoke(f, l, c);
        interp.Output += m => Output?.Invoke(m);
        _interp = interp;

        try
        {
            await Task.Run(() => interp.ExecuteScriptContent(content, fileName));
            Finished?.Invoke(true, "Completed");
        }
        catch (OperationCanceledException)
        {
            Finished?.Invoke(false, "Cancelled");
        }
        catch (Exception ex)
        {
            Output?.Invoke($"[error] {ex.Message}");
            Finished?.Invoke(false, ex.Message);
        }
        finally
        {
            interp.Dispose();
            _interp = null;
            IsRunning = false;
        }
    }

    public async Task RunGroupAsync(string content, string fileName, ISessionChannel channel, GroupSyncContext sync, string memberLabel = "")
    {
        if (IsRunning) return;
        IsRunning = true;

        var interp = new TTLInterpreter(channel, sync, memberLabel);
        interp.StatusChanged += (f, l, c) => StatusChanged?.Invoke(f, l, c);
        interp.Output += m => Output?.Invoke(m);
        _interp = interp;

        try
        {
            await Task.Run(() => interp.ExecuteScriptContent(content, fileName));
            Finished?.Invoke(true, "Completed");
        }
        catch (OperationCanceledException)
        {
            Finished?.Invoke(false, "Cancelled");
        }
        catch (Exception ex)
        {
            Output?.Invoke($"[error] {ex.Message}");
            Finished?.Invoke(false, ex.Message);
        }
        finally
        {
            interp.Dispose();
            _interp = null;
            IsRunning = false;
        }
    }

    public void Cancel() => _interp?.Cancel();
}
