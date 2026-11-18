using Vid2Sub.Infrastructure.Files;

namespace Vid2Sub.Tests;

public class InputFileCollectorTests
{
    [Fact]
    public void Directory_yields_its_media_files_sorted()
    {
        using var temp = new TempDirectory();
        var b = temp.File("b.MP4");
        var a = temp.File("a.mkv");
        temp.File("notes.txt");
        temp.File("nested/c.mp4");

        var (files, missing) = InputFileCollector.Collect([temp.Path]);

        Assert.Equal([a, b], files);
        Assert.Empty(missing);
    }

    [Fact]
    public void Explicit_file_is_taken_whatever_its_extension()
    {
        using var temp = new TempDirectory();
        var file = temp.File("recording.bin");

        var (files, _) = InputFileCollector.Collect([file]);

        Assert.Equal([file], files);
    }

    [Fact]
    public void Wildcard_matches_media_files_only()
    {
        using var temp = new TempDirectory();
        var one = temp.File("ep1.mp4");
        var two = temp.File("ep2.mp4");
        temp.File("ep3.txt");

        var (files, _) = InputFileCollector.Collect([Path.Combine(temp.Path, "ep*")]);

        Assert.Equal([one, two], files);
    }

    [Fact]
    public void Inputs_matching_nothing_are_reported_and_duplicates_dropped()
    {
        using var temp = new TempDirectory();
        var file = temp.File("a.mp4");
        var absent = Path.Combine(temp.Path, "absent.mp4");

        var (files, missing) = InputFileCollector.Collect([file, absent, temp.Path]);

        Assert.Equal([file], files);
        Assert.Equal([absent], missing);
    }
}
