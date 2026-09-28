using System.IO;

namespace PiPrint.App.Services;

public class SpoolWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly HashSet<string> _processingFiles = new();
    private readonly object _lock = new();

    public string SpoolDirectory { get; }
    public string ArchiveDirectory { get; }

    public event Action<byte[], string>? JobReceived;

    public SpoolWatcherService(string? customSpoolDir = null)
    {
        SpoolDirectory = customSpoolDir ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PiPrint", "Spool");

        ArchiveDirectory = Path.Combine(SpoolDirectory, "Archive");

        EnsureDirectoryExists(SpoolDirectory);
        EnsureDirectoryExists(ArchiveDirectory);
    }

    public void Start()
    {
        if (_watcher != null) return;

        EnsureDirectoryExists(SpoolDirectory);
        EnsureDirectoryExists(ArchiveDirectory);

        _watcher = new FileSystemWatcher(SpoolDirectory)
        {
            Filter = "*.xps",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;

        // Process any existing files that arrived while PiPrint was not running
        Task.Run(async () =>
        {
            await Task.Delay(500);
            ProcessExistingPendingFiles();
        });
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileEvent;
            _watcher.Changed -= OnFileEvent;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void ProcessExistingPendingFiles()
    {
        try
        {
            if (!Directory.Exists(SpoolDirectory)) return;

            var files = Directory.GetFiles(SpoolDirectory, "*.xps");
            foreach (var f in files)
            {
                if (f.Contains("Archive", StringComparison.OrdinalIgnoreCase)) continue;
                ProcessFile(f);
            }
        }
        catch
        {
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        ProcessFile(e.FullPath);
    }

    private void ProcessFile(string filePath)
    {
        if (filePath.Contains("Archive", StringComparison.OrdinalIgnoreCase)) return;

        lock (_lock)
        {
            if (_processingFiles.Contains(filePath)) return;
            _processingFiles.Add(filePath);
        }

        Task.Run(async () =>
        {
            try
            {
                byte[]? bytes = await ReadBytesWhenReadyAsync(filePath, TimeSpan.FromSeconds(15));
                if (bytes != null && bytes.Length > 0)
                {
                    // Archive copy for history and direct opening
                    string archiveFileName = $"job_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.xps";
                    string archivePath = Path.Combine(ArchiveDirectory, archiveFileName);

                    try
                    {
                        await File.WriteAllBytesAsync(archivePath, bytes);
                        // Delete original spool file so it's fresh for the next job
                        try { File.Delete(filePath); } catch { }
                    }
                    catch
                    {
                    }

                    JobReceived?.Invoke(bytes, Path.GetFileName(filePath));
                }
            }
            finally
            {
                await Task.Delay(1000);
                lock (_lock)
                {
                    _processingFiles.Remove(filePath);
                }
            }
        });
    }

    private static async Task<byte[]?> ReadBytesWhenReadyAsync(string path, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        long lastLength = -1;
        int stableCounter = 0;

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    if (fi.Length > 0)
                    {
                        if (fi.Length == lastLength)
                        {
                            stableCounter++;
                        }
                        else
                        {
                            lastLength = fi.Length;
                            stableCounter = 0;
                        }

                        if (stableCounter >= 2)
                        {
                            try
                            {
                                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                                using var ms = new MemoryStream();
                                await fs.CopyToAsync(ms);
                                if (ms.Length > 0)
                                {
                                    // Verify that the ZIP container is fully closed and valid
                                    ms.Position = 0;
                                    using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read, true);
                                    if (zip.Entries.Count > 0)
                                    {
                                        return ms.ToArray();
                                    }
                                }
                            }
                            catch (InvalidDataException)
                            {
                                // Spooler still writing zip directory, continue loop
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            await Task.Delay(200);
        }

        return null;
    }

    private static void EnsureDirectoryExists(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
