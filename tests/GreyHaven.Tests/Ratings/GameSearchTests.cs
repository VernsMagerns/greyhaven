using GreyHaven.Compat;
using GreyHaven.Core.Apps;
using GreyHaven.Ratings;

namespace GreyHaven.Tests.Ratings;

/// <summary>"Check a game": the bundled ProtonDB dump and anti-cheat list, searched on this PC.</summary>
public class GameSearchTests
{
    private static readonly Lazy<GameSearch> Search = new(() => new GameSearch(CompatibilityData.Load().Apps));

    [Fact]
    public void Exact_name_comes_first_with_its_protondb_page()
    {
        var first = Search.Value.Search("elden ring")[0];
        Assert.Equal("1245620", first.Rating.App.Id);
        Assert.Equal(AppVerdict.WorksWithLayer, first.Rating.Verdict);
        Assert.Equal("https://www.protondb.com/app/1245620", first.ProtonDbUrl);
    }

    [Fact]
    public void Anti_cheat_blocks_win_over_player_reports()
    {
        var apex = Search.Value.Search("Apex Legends")[0];
        Assert.Equal(AppVerdict.WontWork, apex.Rating.Verdict);
        Assert.Equal("anti-cheat", apex.Rating.Reason);
    }

    [Fact]
    public void Games_only_on_the_anti_cheat_list_are_found_without_a_protondb_link()
    {
        var fortnite = Search.Value.Search("fortnite").First(m => m.Rating.Name == "Fortnite");
        Assert.Equal(AppVerdict.WontWork, fortnite.Rating.Verdict);
        Assert.Null(fortnite.ProtonDbUrl);
    }

    [Fact]
    public void Every_word_matches_anywhere_and_short_searches_find_nothing()
    {
        Assert.Contains(Search.Value.Search("ring elden"), m => m.Rating.App.Id == "1245620");
        Assert.Empty(Search.Value.Search("e"));
        Assert.Empty(Search.Value.Search("zzqx no such game"));
        Assert.True(Search.Value.Search("the", max: 8).Count <= 8);
    }

    [Fact]
    public void Every_protondb_game_has_a_title() =>
        Assert.Equal(CompatibilityData.Load().Apps.ProtonDb.Count, CompatibilityData.Load().Apps.ProtonDbTitles.Count);
}
