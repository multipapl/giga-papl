using BlenderToolbox.Tools.SplitByContext.Services;

namespace BlenderToolbox.Core.Tests;

public sealed class SplitByContextServicesTests
{
    [Fact]
    public void ScriptBuilder_IncludesContextLoopAndSaveMarker()
    {
        var script = new SplitByContextScriptBuilder().BuildScript();

        Assert.Contains("renderset_contexts", script);
        Assert.Contains("SAVING::", script);
        Assert.Contains("save_as_mainfile", script);
    }

    [Fact]
    public void OutputParser_ReturnsCreatedFilesFromStdout()
    {
        const string output = """
            Starting context split...
            SAVING::Q:\shots\scene_Cam_A.blend
            SAVING::Q:\shots\scene_Cam_B.blend
            Context split completed.
            """;

        var files = new SplitByContextOutputParser().ParseCreatedFiles(output);

        Assert.Equal(
            [
                @"Q:\shots\scene_Cam_A.blend",
                @"Q:\shots\scene_Cam_B.blend",
            ],
            files);
    }
}
