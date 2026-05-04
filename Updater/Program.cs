using System.Diagnostics;
using System.IO.Compression;

namespace Updater;

internal static class Program
{
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "viewer.db",
        "viewer.settings.json",
        "Logs",
        "Exports",
        "Backups",
        "Updates"
    };

    private static int Main(string[] args)
    {
        try
        {
            var arguments = ParseArguments(args);
            var zipPath = RequireArgument(arguments, "zip");
            var appDirectory = RequireArgument(arguments, "app-dir");
            var exeName = RequireArgument(arguments, "exe");
            var processId = int.TryParse(arguments.GetValueOrDefault("pid"), out var parsedProcessId) ? parsedProcessId : 0;

            WaitForProcessExit(processId);

            var backupDirectory = CreateBackup(appDirectory);
            var tempDirectory = Path.Combine(Path.GetTempPath(), "ViewerUpdate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempDirectory, overwriteFiles: true);
                var packageRoot = FindPackageRoot(tempDirectory);
                CopyPackage(packageRoot, appDirectory);
            }
            catch
            {
                RestoreBackup(backupDirectory, appDirectory);
                throw;
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }

            var executablePath = Path.Combine(appDirectory, exeName);
            if (File.Exists(executablePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = appDirectory,
                    UseShellExecute = true
                });
            }

            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Viewer Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                break;
            }

            arguments[name[2..]] = args[++index];
        }

        return arguments;
    }

    private static string RequireArgument(Dictionary<string, string> arguments, string name)
    {
        if (arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing update argument: {name}");
    }

    private static void WaitForProcessExit(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit(60000);
        }
        catch
        {
            // The app may already be closed.
        }
    }

    private static string CreateBackup(string appDirectory)
    {
        var backupRoot = Path.Combine(appDirectory, "Backups");
        var backupDirectory = Path.Combine(backupRoot, "UpdateBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(backupDirectory);

        foreach (var path in Directory.GetFileSystemEntries(appDirectory))
        {
            var name = Path.GetFileName(path);
            if (ShouldExclude(name))
            {
                continue;
            }

            var destinationPath = Path.Combine(backupDirectory, name);
            if (Directory.Exists(path))
            {
                CopyDirectory(path, destinationPath);
            }
            else
            {
                File.Copy(path, destinationPath, overwrite: true);
            }
        }

        return backupDirectory;
    }

    private static string FindPackageRoot(string tempDirectory)
    {
        if (File.Exists(Path.Combine(tempDirectory, "Viewer.exe")))
        {
            return tempDirectory;
        }

        var childDirectories = Directory.GetDirectories(tempDirectory);
        var childFiles = Directory.GetFiles(tempDirectory);
        if (childFiles.Length == 0 && childDirectories.Length == 1 && File.Exists(Path.Combine(childDirectories[0], "Viewer.exe")))
        {
            return childDirectories[0];
        }

        return tempDirectory;
    }

    private static void CopyPackage(string sourceDirectory, string appDirectory)
    {
        foreach (var path in Directory.GetFileSystemEntries(sourceDirectory))
        {
            var name = Path.GetFileName(path);
            if (ShouldExclude(name))
            {
                continue;
            }

            var destinationPath = Path.Combine(appDirectory, name);
            if (Directory.Exists(path))
            {
                CopyDirectory(path, destinationPath);
            }
            else
            {
                File.Copy(path, destinationPath, overwrite: true);
            }
        }
    }

    private static void RestoreBackup(string backupDirectory, string appDirectory)
    {
        if (!Directory.Exists(backupDirectory))
        {
            return;
        }

        CopyDirectory(backupDirectory, appDirectory);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var filePath in Directory.GetFiles(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, overwrite: true);
        }

        foreach (var childDirectory in Directory.GetDirectories(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(childDirectory));
            CopyDirectory(childDirectory, destinationPath);
        }
    }

    private static bool ShouldExclude(string name)
    {
        return ExcludedNames.Contains(name);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Temporary cleanup failure should not block the updated app from starting.
        }
    }
}
