namespace Nexus.Shell.Services;

public enum FileSelectionKind { Browse, OpenFile, OpenFiles, Folder, SaveFile }
public sealed record FileSelectionRequest(FileSelectionKind Kind, string Title = "My files", string[]? Extensions = null, string SuggestedName = "");
