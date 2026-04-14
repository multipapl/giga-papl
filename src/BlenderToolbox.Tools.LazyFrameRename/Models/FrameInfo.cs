namespace BlenderToolbox.Tools.LazyFrameRename.Models;

public readonly record struct FrameInfo(
    string BaseName,
    string Extension,
    int StartNumber,
    int Padding);
