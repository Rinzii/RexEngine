namespace Rex.Shared.Startup;

/// <summary>
/// Finds a server assembly for local startup.
/// </summary>
public static class RuntimeAssemblyLocator
{
    /// <summary>
    /// Resolves a server assembly path from environment values or build output.
    /// </summary>
    public static string? ResolveServerAssemblyPath(
        string environmentVariable,
        string serverAssemblyFileName,
        string clientProjectName,
        string serverProjectName)
    {
        var configuredPath = Environment.GetEnvironmentVariable(environmentVariable)?.Trim();
        if (!string.IsNullOrEmpty(configuredPath))
        {
            try
            {
                var fullConfigured = Path.GetFullPath(configuredPath);
                return File.Exists(fullConfigured) ? fullConfigured : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(baseDir))
        {
            return null;
        }

        var siblingPath = Path.Combine(baseDir, serverAssemblyFileName);
        if (File.Exists(siblingPath))
        {
            try
            {
                return Path.GetFullPath(siblingPath);
            }
            catch (Exception)
            {
                return null;
            }
        }

        DirectoryInfo outputDir;
        try
        {
            outputDir = new DirectoryInfo(baseDir);
        }
        catch (Exception)
        {
            return null;
        }

        var projectDir = outputDir.Parent;
        var binDir = projectDir?.Parent;
        var buildRoot = binDir?.Parent;
        if (projectDir != null && binDir != null && buildRoot != null
            && string.Equals(binDir.Name, "bin", StringComparison.OrdinalIgnoreCase)
            && string.Equals(buildRoot.Name, "build", StringComparison.OrdinalIgnoreCase)
            && string.Equals(projectDir.Name, clientProjectName, StringComparison.OrdinalIgnoreCase))
        {
            string artifactsServer;
            try
            {
                artifactsServer = Path.Combine(
                    binDir.FullName,
                    serverProjectName,
                    outputDir.Name,
                    serverAssemblyFileName);
            }
            catch (Exception)
            {
                return null;
            }

            if (File.Exists(artifactsServer))
            {
                try
                {
                    return Path.GetFullPath(artifactsServer);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        return null;
    }
}
