namespace ChanSight.Core.Season;

/// <summary>
/// 赛季字典的写入视图: 自学习候选的收集、确认(写入正式字典)与撤销。
/// 候选(Pending)与正式字典分开, 候选不参与正式查询。
/// </summary>
public interface ISeasonDictionaryWriter
{
    void AddCandidate(string seasonId, CandidateMeta candidate);

    void Confirm(string seasonId, string entity, ConfirmKind kind);

    void Revoke(string seasonId, string entity);

    IReadOnlyList<CandidateMeta> GetPendingCandidates(string seasonId);
}