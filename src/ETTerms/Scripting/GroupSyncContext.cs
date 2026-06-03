namespace ETTerms.Scripting;

/// <summary>
/// Group 成員間的同步上下文。waitall 會等所有成員都到達同一個 barrier 後才繼續。
/// sendlnall 會等所有成員到達後，每個成員各自對自己的 channel 送出指令再繼續。
/// </summary>
public sealed class GroupSyncContext
{
    private readonly int _memberCount;
    private readonly Barrier _barrier;

    public GroupSyncContext(int memberCount)
    {
        _memberCount = memberCount;
        _barrier = new Barrier(memberCount);
    }

    /// <summary>等待所有 group 成員到達此同步點。</summary>
    public void WaitAll(CancellationToken ct)
    {
        _barrier.SignalAndWait(ct);
    }
}
