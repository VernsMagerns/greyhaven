using GreyHaven.Media;

namespace GreyHaven.Tests.Media;

public class HelperProtocolTests
{
    private static HelperRequest Write => new()
    {
        Action = HelperAction.WriteUsb, Pipe = HelperRequest.NewPipeName(), Token = HelperRequest.NewToken(),
        Disk = 3, Model = "SanDisk Ultra", Size = 16_008_609_792, Image = OperatingSystem.IsWindows() ? @"C:\Users\x\Downloads\ubuntu.iso" : "/home/x/ubuntu.iso",
        Sha256 = new string('a', 64),
    };

    [Fact]
    public void Requests_round_trip_through_arguments()
    {
        var write = Write;
        Assert.Equal(write, HelperRequest.Parse(write.ToArgs()));
        var fast = new HelperRequest { Action = HelperAction.FastStartupOff, Pipe = HelperRequest.NewPipeName(), Token = HelperRequest.NewToken() };
        Assert.Equal(fast, HelperRequest.Parse(fast.ToArgs()));
        var large = write with { LargeDrive = true };
        Assert.Equal(large, HelperRequest.Parse(large.ToArgs()));
        Assert.DoesNotContain("--large-drive", write.ToArgs());
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse([.. write.ToArgs(), "--large-drive", "maybe"]));
    }

    [Theory]
    [InlineData("format-c")]                      // unknown action
    [InlineData("--disk")]                        // no action
    public void Unknown_actions_are_refused(string first) =>
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse([first]));

    [Fact]
    public void Anything_unexpected_is_refused()
    {
        var write = Write;
        var args = write.ToArgs().ToList();
        string[] With(string key, string value) { var a = args.ToList(); a[a.IndexOf(key) + 1] = value; return [.. a]; }
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse(With("--image", "relative.iso")));
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse(With("--image", write.Image!.Replace(".iso", ".exe"))));
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse(With("--disk", "-1")));
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse(With("--sha256", "xyz")));
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse(With("--pipe", @"\\server\pipe")));
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse(With("--token", "short")));
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse([.. args, "--extra", "x"]));
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse([.. args, "--disk", "4"]));                  // given twice
        // Disk options make no sense for other actions.
        Assert.Throws<ArgumentException>(() => HelperRequest.Parse(["fast-startup-off", "--pipe", write.Pipe, "--token", write.Token, "--disk", "3"]));
    }

    [Fact]
    public void Messages_round_trip_and_bad_lines_are_ignored()
    {
        var m = new HelperMessage(HelperMessage.Progress, ImageWriter.Writing, 10, 100);
        Assert.Equal(m, HelperMessage.FromLine(m.ToLine()));
        Assert.Null(HelperMessage.FromLine("not json"));
    }

    [Fact]
    public void Every_action_explains_the_permission_prompt_first()
    {
        Assert.Equal("Windows will ask for permission so GreyHaven can write to your USB stick.", HelperReasons.Why(HelperAction.WriteUsb));
        Assert.All(Enum.GetValues<HelperAction>(), a => Assert.StartsWith("Windows will ask for permission so GreyHaven can ", HelperReasons.Why(a)));
    }
}
