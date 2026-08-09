namespace AxiomOps.Services.Models;

/// <summary>Basic installed-game record as stored in the database.</summary>
public class GameRecord
{
    public int ClientId { get; set; }
    public int ModuleId { get; set; }
    public string? InternalName { get; set; }
    public string? FriendlyName { get; set; }
    public string? DisplayName { get; set; }
    public string? SimpleName { get; set; }
    public string? ShortName { get; set; }
    public string? PackageName { get; set; }
    public string? GameId { get; set; }
    public int? GameProviderId { get; set; }
    public string? GameProvider { get; set; }
    public bool FreeGameSupport { get; set; }
    public bool Clone { get; set; }
    public bool Rtp { get; set; }
    public int? ClientTypeId { get; set; }
    public string? GamePath { get; set; }
}

/// <summary>Full installed-game record including engine artifacts and uploaded versions.</summary>
public class InstalledGameRecord : GameRecord
{
    public string? GameEngine { get; set; }
    public VeyronGameRecord? VeyronGameRecord { get; set; }
    public BanditGameRecord? BanditGameRecord { get; set; }
    public List<GameVersion>? Versions { get; set; }
}

public class VeyronGameRecord
{
    public List<string>? AdditionalPluginFiles { get; set; }
    public List<string>? AdditionalGameFiles { get; set; }
    public string? Architecture { get; set; }
    public string? DotVeyronGameFilePath { get; set; }
    public string? DotSignatureFilePath { get; set; }
    public string? ParentPath { get; set; }
    public string? VeyronPlugin { get; set; }
}

public class BanditGameRecord
{
    public List<BanditGameServiceRecord>? BanditGameServiceRecords { get; set; }
    public string? ParentPath { get; set; }
}

public class BanditGameServiceRecord
{
    public List<string>? AdditionalGameFiles { get; set; }
    public string? Version { get; set; }
}

public class GameVersion
{
    public string? ContentDirectory { get; set; }
    public DateTimeOffset? UploadDateTime { get; set; }
    public bool ProductionVersioned { get; set; }
    public string? ProductionVersion { get; set; }
    public string? Version { get; set; }
    public bool Titan { get; set; }
}

public class GameDependencyReport
{
    public int ClientId { get; set; }
    public int ModuleId { get; set; }
    public string? InternalName { get; set; }
    public string? SimpleName { get; set; }
    public List<GameDllDependency>? GameDllDependencies { get; set; }
}

public class GameDllDependency
{
    public string? DependentFile { get; set; }
    public bool Exists { get; set; }
    public List<GameDllDependency>? AdditionalPluginFiles { get; set; }
}
