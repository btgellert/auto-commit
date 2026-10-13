using AutoCommitService;

namespace AutoCommitService.Tests;

public class CommitRunnerTests
{
    [Fact]
    public void BuildLogLine_FormatsUtcStamp()
    {
        var line = CommitRunner.BuildLogLine(new DateTimeOffset(2026, 10, 13, 18, 30, 0, TimeSpan.Zero));

        Assert.Equal("- 2026-10-13 18:30 UTC - automated activity commit", line);
    }

    [Fact]
    public void FormatCommitMessage_ReplacesTimestampToken()
    {
        var message = CommitRunner.FormatCommitMessage("chore: auto ({timestamp})", "2026-10-13 18:30 UTC");

        Assert.Equal("chore: auto (2026-10-13 18:30 UTC)", message);
    }

    [Fact]
    public void InsertLogLine_WithoutMarkers_AppendsLine()
    {
        var result = CommitRunner.InsertLogLine("# Title\n", "- 2026-10-13 18:30 UTC - x");

        Assert.Equal("# Title\n- 2026-10-13 18:30 UTC - x\n", result);
    }

    [Fact]
    public void InsertLogLine_WithoutTrailingNewline_AddsSeparator()
    {
        var result = CommitRunner.InsertLogLine("# Title", "- line");

        Assert.Equal("# Title\n- line\n", result);
    }

    [Fact]
    public void InsertLogLine_WithMarkers_InsertsBeforeEndMarker()
    {
        var content = "# Title\n<!-- AUTO-COMMIT-LOG:START -->\n- old\n<!-- AUTO-COMMIT-LOG:END -->\n";

        var result = CommitRunner.InsertLogLine(content, "- new");

        Assert.Equal(
            "# Title\n<!-- AUTO-COMMIT-LOG:START -->\n- old\n- new\n<!-- AUTO-COMMIT-LOG:END -->\n",
            result);
    }

    [Fact]
    public void InsertLogLine_WithMarkers_TrimsToNewestLines()
    {
        var content = "<!-- AUTO-COMMIT-LOG:START -->\n- 1\n- 2\n- 3\n<!-- AUTO-COMMIT-LOG:END -->\n";

        var result = CommitRunner.InsertLogLine(content, "- 4", maxLines: 3);

        Assert.Equal(
            "<!-- AUTO-COMMIT-LOG:START -->\n- 2\n- 3\n- 4\n<!-- AUTO-COMMIT-LOG:END -->\n",
            result);
    }
}
