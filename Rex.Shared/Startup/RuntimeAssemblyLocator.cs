namespace Rex.Shared.Startup;

/// <summary>
/// Locates the game server binary next to a client build or through env override.
/// </summary>
public static class RuntimeAssemblyLocator
{
    /// <summary>
    /// Resolves a server assembly path from environment values or build output.
    /// </summary>
    /// <param name="environmentVariable">Optional absolute path override read from the environment.</param>
    /// <param name="serverAssemblyFileName">File name searched next to the client output.</param>
    /// <param name="clientProjectName">Expected client project folder name under build artifacts.</param>
    /// <param name="serverProjectName">Sibling server project folder name under the same build configuration.</param>
    /// <returns>Full path when a candidate exists. Null when nothing matched.</returns>
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
