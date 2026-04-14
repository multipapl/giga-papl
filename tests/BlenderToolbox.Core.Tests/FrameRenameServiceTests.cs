using System.IO;
using BlenderToolbox.Tools.LazyFrameRename.Models;
using BlenderToolbox.Tools.LazyFrameRename.Services;

namespace BlenderToolbox.Core.Tests;

public sealed class FrameRenameServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"BlenderToolbox.Tests.{Guid.NewGuid():N}");
    private readonly FrameRenameService _service = new();

    [Fact]
    public void BuildRenamePlan_UsesCustomPrefixAndCompactsNumbering()
    {
        var plan = _service.BuildRenamePlan(
            ["frame0300.png", "frame0303.png", "frame0306.png"],
            @"Q:\renders",
            "myshot_");

        var renamedFiles = plan.Select(static item => item.NewName).ToArray();

        Assert.Equal(["myshot_0300.png", "myshot_0301.png", "myshot_0302.png"], renamedFiles);
    }

    [Fact]
    public void BuildRenamePlan_UsesManualDigitOverrideWhenRequested()
    {
        var plan = _service.BuildRenamePlan(
            ["beauty_v20007.exr", "beauty_v20015.exr"],
            @"Q:\renders",
            "shot_",
            digitsOverride: 4);

        Assert.Collection(
            plan,
            item =>
            {
                Assert.Equal("beauty_v20007.exr", item.OldName);
                Assert.Equal("shot_0007.exr", item.NewName);
            },
            item =>
            {
                Assert.Equal("beauty_v20015.exr", item.OldName);
                Assert.Equal("shot_0008.exr", item.NewName);
            });
    }

    [Fact]
    public void BuildRenamePlan_ThrowsWhenTrailingDigitsAreMissingInAutoMode()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _service.BuildRenamePlan(["render_final.png"], @"Q:\renders"));

        Assert.Contains("No trailing digits", exception.Message);
    }

    [Fact]
    public void RenameFiles_RenamesAllFilesInsideManualFolder()
    {
        var folder = CreateFolder("Manual");
        File.WriteAllText(Path.Combine(folder, "frame0300.png"), "a");
        File.WriteAllText(Path.Combine(folder, "frame0303.png"), "b");
        File.WriteAllText(Path.Combine(folder, "frame0306.png"), "c");

        var result = _service.RenameFiles(new RenameRequest
        {
            Mode = RenameMode.Manual,
            ManualFolders = [folder],
            CustomPrefix = "shot_",
        });

        var fileNames = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(static name => name).ToArray();

        Assert.Equal(3, result.TotalFilesRenamed);
        Assert.Equal(["shot_0300.png", "shot_0301.png", "shot_0302.png"], fileNames);
    }

    [Fact]
    public void RenameFiles_InSubfoldersModeProcessesImmediateChildren()
    {
        var parent = CreateFolder("Parent");
        var subA = Directory.CreateDirectory(Path.Combine(parent, "A")).FullName;
        var subB = Directory.CreateDirectory(Path.Combine(parent, "B")).FullName;

        File.WriteAllText(Path.Combine(subA, "frame0010.png"), "a");
        File.WriteAllText(Path.Combine(subA, "frame0012.png"), "b");
        File.WriteAllText(Path.Combine(subB, "clip0042.png"), "c");

        var result = _service.RenameFiles(new RenameRequest
        {
            Mode = RenameMode.Subfolders,
            ParentFolder = parent,
            CustomPrefix = string.Empty,
        });

        var subAFiles = Directory.GetFiles(subA).Select(Path.GetFileName).OrderBy(static name => name).ToArray();
        var subBFiles = Directory.GetFiles(subB).Select(Path.GetFileName).OrderBy(static name => name).ToArray();

        Assert.Equal(1, result.TotalFilesRenamed);
        Assert.Equal(["frame0010.png", "frame0011.png"], subAFiles);
        Assert.Equal(["clip0042.png"], subBFiles);
    }

    [Fact]
    public void RenameFiles_ReturnsZeroWhenNamesAlreadyMatchPlan()
    {
        var folder = CreateFolder("Stable");
        File.WriteAllText(Path.Combine(folder, "frame0001.png"), "a");
        File.WriteAllText(Path.Combine(folder, "frame0002.png"), "b");

        var result = _service.RenameFiles(new RenameRequest
        {
            Mode = RenameMode.Manual,
            ManualFolders = [folder],
        });

        Assert.Equal(0, result.TotalFilesRenamed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateFolder(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
