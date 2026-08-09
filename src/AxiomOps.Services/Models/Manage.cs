namespace AxiomOps.Services.Models;

/// <summary>File payload returned by Get File Content; the same shape is sent to Set File Content.</summary>
public class FileContent
{
    public string? Content { get; set; }
    public string? DisplayName { get; set; }
    public string? Path { get; set; }
    public bool? Schema { get; set; }
    public string? SchemaPath { get; set; }
    public string? SchemaContent { get; set; }
}

public class FileFolderNode
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? ObjectType { get; set; }
    public DateTimeOffset? DateModified { get; set; }
    public List<string>? Tags { get; set; }
    public List<FileFolderNode>? Children { get; set; }
}

public class WebsiteInfo
{
    public string? Name { get; set; }
    public string? State { get; set; }
}

public class WindowsServiceInfo
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? State { get; set; }
}

public class ServiceActionResult
{
    public bool Result { get; set; }
    public int Code { get; set; }
    public string? Message { get; set; }
}
