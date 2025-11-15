namespace AIOS.Tools;

/// <summary>
/// 文件系统工具 - 文件和目录操作
/// </summary>
public class FileSystemTool : ITool
{
    public string Name => "filesystem";
    public string Description => "Perform file system operations";
    
    public IReadOnlyList<ToolParameter> Parameters => new[]
    {
        new ToolParameter
        {
            Name = "operation",
            Type = "string",
            Description = "File system operation to perform",
            Required = true,
            AllowedValues = new[] { "read", "write", "delete", "list", "exists", "mkdir", "rmdir" }
        },
        new ToolParameter
        {
            Name = "path",
            Type = "string",
            Description = "File or directory path",
            Required = true
        },
        new ToolParameter
        {
            Name = "content",
            Type = "string",
            Description = "Content to write (for write operation)",
            Required = false
        }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var operation = parameters["operation"]?.ToString()?.ToLower();
            var path = parameters["path"]?.ToString() ?? string.Empty;

            object? result = operation switch
            {
                "read" => ReadFile(path),
                "write" => WriteFile(path, parameters.GetValueOrDefault("content")?.ToString() ?? string.Empty),
                "delete" => DeleteFile(path),
                "list" => ListDirectory(path),
                "exists" => File.Exists(path) || Directory.Exists(path),
                "mkdir" => CreateDirectory(path),
                "rmdir" => DeleteDirectory(path),
                _ => throw new ArgumentException($"Unsupported operation: {operation}")
            };

            return Task.FromResult(new ToolResult
            {
                Success = true,
                Data = result
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private static string ReadFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        return File.ReadAllText(path);
    }

    private static bool WriteFile(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
        return true;
    }

    private static bool DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return true;
        }
        return false;
    }

    private static string[] ListDirectory(string path)
    {
        if (!Directory.Exists(path))
            return Array.Empty<string>();

        return Directory.GetFileSystemEntries(path);
    }

    private static bool CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return true;
    }

    private static bool DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            return true;
        }
        return false;
    }
}
