namespace ChanSight.Core.Season;

/// <summary>
/// 赛季正式字典的只读视图。只返回正式字典(候选不参与查询)。
/// </summary>
public interface ISeasonDictionaryReader
{
    SeasonDictionary? Get(string seasonId);

    bool TryGetHero(string seasonId, string name);

    bool TryGetItem(string seasonId, string name);
}