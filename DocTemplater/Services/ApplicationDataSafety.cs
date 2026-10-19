using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DocTemplater.Domain;

namespace DocTemplater.Services;

/// <summary>Resolves every existing ancestor, including junctions above a not-yet-created directory.</summary>
public static class CanonicalPath
{
    public static string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new IOException("Не указан каталог.");
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var resolved = root;
        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, segment);
            if (!Directory.Exists(resolved) && !File.Exists(resolved))
            {
                // GetAttributes distinguishes a genuinely missing component from inaccessible/broken links.
                try { _ = File.GetAttributes(resolved); }
                catch (FileNotFoundException) { continue; }
                catch (DirectoryNotFoundException) { continue; }
            }
            if ((File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
                resolved = new DirectoryInfo(resolved).ResolveLinkTarget(true)?.FullName
                    ?? throw new IOException($"Не удалось разрешить ссылку: {resolved}");
        }
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolved));
    }

    public static bool IsWithin(string path, string root)
    {
        var candidate = Resolve(path);
        var parent = Resolve(root);
        return candidate.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ApplicationInstanceCoordinator : IDisposable
{
    private readonly FileStream _lease;
    private ApplicationInstanceCoordinator(FileStream lease) => _lease = lease;

    public static void EnsureNoOtherProcess()
    {
        var name = Path.GetFileNameWithoutExtension(Environment.ProcessPath)!;
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                try { if (process.HasExited) continue; }
                catch (InvalidOperationException) { continue; }
                throw new IOException("Закройте другую копию DocTemplater и повторите запуск/обновление.");
            }
        }
    }

    public static ApplicationInstanceCoordinator Acquire(string dataRoot)
    {
        EnsureNoOtherProcess();
        var root = CanonicalPath.Resolve(dataRoot);
        Directory.CreateDirectory(root);
        try { return new(new FileStream(Path.Combine(root, ".instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
        catch (IOException ex) { throw new IOException("Этот профиль уже открыт другой копией DocTemplater. Закройте её и повторите запуск.", ex); }
    }
    public void Dispose() => _lease.Dispose();
}

public sealed class ApplicationDataSafety(string dataRoot, IReadOnlyList<string> managedRoots, string legacyBaseDirectory,
    Action? ensureNoOtherProcess = null)
{
    public string DataRoot { get; } = CanonicalPath.Resolve(dataRoot);
    public string SettingsPath => Path.Combine(DataRoot, "settings.json");
    public string TemplatesPath => Path.Combine(DataRoot, "Templates");

    private bool IsManaged(string path) => managedRoots.Any(root => CanonicalPath.IsWithin(path, root));
    private sealed class BackupManifest
    {
        public List<string> UserDirectories { get; set; } = new();
        public List<BackupFile> Files { get; set; } = new();
    }
    private sealed record BackupFile(string Source, string Backup, string Sha256, long Size);
    private BackupManifest _backups = new();
    private string BackupManifestPath => Path.Combine(DataRoot, "UpdateBackups", "manifest.json");

    private void LoadBackupManifest()
    {
        _backups = File.Exists(BackupManifestPath)
            ? JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(BackupManifestPath)) ?? throw new IOException("Повреждён manifest резервных копий.")
            : new BackupManifest();
        if (_backups.Files == null || _backups.UserDirectories == null) throw new IOException("Повреждён manifest резервных копий.");
    }

    public void EnsureSafeUserDirectory(string path)
    {
        if (IsManaged(path)) throw new IOException($"Выберите каталог вне установки DocTemplater: {path}");
    }

    private IEnumerable<string> ManagedDocuments()
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documents = new List<string>();
        void Visit(string directory)
        {
            if (!visited.Add(CanonicalPath.Resolve(directory))) return;
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Проверка данных остановлена: ссылка {directory}");
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException($"Проверка данных остановлена: ссылка {entry}");
                if ((attributes & FileAttributes.Directory) != 0) Visit(entry);
                else if (Path.GetExtension(entry).Equals(".docx", StringComparison.OrdinalIgnoreCase)
                    || _backups.UserDirectories.Any(root => CanonicalPath.IsWithin(entry, root))) documents.Add(entry);
            }
        }
        foreach (var root in managedRoots) if (Directory.Exists(root)) Visit(root);
        return documents;
    }

    private string DocumentBackupPath(string document) => Path.Combine(DataRoot, "UpdateBackups",
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(document))), Path.GetFileName(document));

    private void SnapshotManagedDocuments()
    {
        foreach (var document in ManagedDocuments())
        {
            var backup = DocumentBackupPath(document);
            if (!CanonicalPath.IsWithin(backup, DataRoot)) throw new IOException($"Небезопасный каталог резервной копии: {backup}");
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            if (!File.Exists(backup)) File.Copy(document, backup);
            if (!SHA256.HashData(File.ReadAllBytes(document)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(backup))))
                throw new IOException($"Не удалось проверить резервную копию {document}");
            var source = CanonicalPath.Resolve(document);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(document)));
            if (!_backups.Files.Any(file => file.Source.Equals(source, StringComparison.OrdinalIgnoreCase) && file.Sha256 == hash))
                _backups.Files.Add(new(source, backup, hash, new FileInfo(document).Length));
        }
        SaveBackupManifest();
    }

    private void SaveBackupManifest()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BackupManifestPath)!);
        var temporary = BackupManifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(_backups, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, BackupManifestPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task PreserveCurrentUserDirectoriesAsync(IValuePersistenceService persistence)
    {
        (ensureNoOtherProcess ?? ApplicationInstanceCoordinator.EnsureNoOtherProcess)();
        EnsureSafeUserDirectory(DataRoot);
        var settings = await persistence.LoadSettingsAsync();
        LoadBackupManifest();
        var userDirectories = new[] { Path.Combine(legacyBaseDirectory, "Templates"), settings.TemplatesFolderPath, settings.DefaultOutputPath }
            .Concat(settings.Templates.Select(t => t.LastSaveDirectory));
        foreach (var directory in userDirectories.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var canonical = CanonicalPath.Resolve(directory!);
            if (Directory.Exists(canonical) && IsManaged(canonical)
                && !managedRoots.Any(root => CanonicalPath.Resolve(root).Equals(canonical, StringComparison.OrdinalIgnoreCase))
                && !_backups.UserDirectories.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                _backups.UserDirectories.Add(canonical);
        }
        // Commit identity before reading any user file: recovery must retain even an unreadable old tree.
        SaveBackupManifest();
    }

    public async Task<AppSettings> BootstrapAsync(IValuePersistenceService persistence, bool importAdjacentTemplates = true)
    {
        // A failed settings read or context commit must stop bootstrap before copying or patching anything.
        await PreserveCurrentUserDirectoriesAsync(persistence);
        var settings = await persistence.LoadSettingsAsync();
        SnapshotManagedDocuments();
        var changed = false;
        var configured = settings.TemplatesFolderPath;
        var oldDefault = string.IsNullOrWhiteSpace(configured) || configured is "./Templates" or ".\\Templates";
        var absolute = Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? "./Templates" : configured);
        var adjacent = Path.Combine(legacyBaseDirectory, "Templates");
        var migrationMarker = Path.Combine(DataRoot, ".paths-bootstrap-v1");
        if (importAdjacentTemplates && !File.Exists(migrationMarker) && Directory.Exists(adjacent) && IsManaged(adjacent) && !CanonicalPath.IsWithin(adjacent, TemplatesPath))
            CopyTree(adjacent, TemplatesPath);
        if (oldDefault && !Directory.Exists(absolute) && Directory.Exists(adjacent)) absolute = adjacent;

        if ((oldDefault && !Directory.Exists(absolute)) || IsManaged(absolute))
        {
            if (Directory.Exists(absolute) && !CanonicalPath.IsWithin(absolute, TemplatesPath))
                CopyTree(absolute, TemplatesPath);
            Directory.CreateDirectory(TemplatesPath);
            settings.TemplatesFolderPath = TemplatesPath;
            changed = configured != TemplatesPath;
        }
        else
        {
            // Keep an explicitly chosen external path, even if temporarily disconnected.
            settings.TemplatesFolderPath = absolute;
            changed = configured != absolute;
        }

        string MigrateOutput(string output)
        {
            var source = Path.GetFullPath(output);
            if (!IsManaged(source)) { changed |= source != output; return source; }
            var destination = Path.Combine(DataRoot, "Documents", Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source.ToUpperInvariant())))[..12]);
            if (Directory.Exists(source))
            {
                if (CanonicalPath.Resolve(source).Equals(CanonicalPath.Resolve(legacyBaseDirectory), StringComparison.OrdinalIgnoreCase)
                    || managedRoots.Any(root => CanonicalPath.Resolve(source).Equals(CanonicalPath.Resolve(root), StringComparison.OrdinalIgnoreCase)))
                    CopyTree(source, destination, docxOnly: true);
                else CopyTree(source, destination);
            }
            Directory.CreateDirectory(destination);
            changed = true;
            return destination;
        }
        settings.DefaultOutputPath = MigrateOutput(settings.DefaultOutputPath);
        foreach (var template in settings.Templates)
            if (!string.IsNullOrWhiteSpace(template.LastSaveDirectory))
                template.LastSaveDirectory = MigrateOutput(template.LastSaveDirectory);

        if (changed)
        {
            if (File.Exists(SettingsPath))
                File.Copy(SettingsPath, SettingsPath + ".before-path-migration-" + Guid.NewGuid().ToString("N") + ".bak");
            await persistence.SaveSettingsAsync(settings);
            var reloaded = await new ValuePersistenceService(SettingsPath).LoadSettingsAsync();
            if (reloaded.TemplatesFolderPath != settings.TemplatesFolderPath)
                throw new IOException("Не удалось проверить сохранённые пути.");
        }
        EnsureSafeForUpdate(settings, null);
        if (!File.Exists(migrationMarker)) await File.WriteAllTextAsync(migrationMarker, "Completed");
        return settings;
    }

    public void EnsureSafeForUpdate(AppSettings settings, string? currentOutput)
    {
        LoadBackupManifest();
        var paths = new[] { DataRoot, settings.TemplatesFolderPath, settings.DefaultOutputPath, currentOutput }
            .Concat(settings.Templates.Select(template => template.LastSaveDirectory));
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
            if (IsManaged(path!))
                throw new IOException($"Каталог данных находится внутри установки: {path}. Выберите внешний каталог перед обновлением.");
        foreach (var document in ManagedDocuments())
        {
            var backup = DocumentBackupPath(document);
            var source = CanonicalPath.Resolve(document);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(document)));
            if (!CanonicalPath.IsWithin(backup, DataRoot) || !File.Exists(backup)
                || !_backups.Files.Any(file => file.Source.Equals(source, StringComparison.OrdinalIgnoreCase) && file.Sha256 == hash && file.Backup == backup && file.Size == new FileInfo(document).Length)
                || !SHA256.HashData(File.ReadAllBytes(document)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(backup))))
                throw new IOException($"Документ внутри установки не имеет проверенной копии: {document}. Перезапустите приложение для резервного копирования перед обновлением.");
        }
    }

    private static void CopyTree(string source, string destination, bool docxOnly = false)
    {
        // Enumerate and validate everything first. Never traverse reparse points or overwrite collisions.
        var files = new List<(string Source, string Destination)>();
        void Visit(string directory)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Перенос остановлен: ссылка/соединение {directory}. Скопируйте данные в обычный каталог.");
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException($"Перенос остановлен: ссылка {entry}");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!docxOnly) Visit(entry);
                    continue;
                }
                if (docxOnly && !Path.GetExtension(entry).Equals(".docx", StringComparison.OrdinalIgnoreCase)) continue;
                var target = Path.Combine(destination, Path.GetRelativePath(source, entry));
                if (File.Exists(target) && !SHA256.HashData(File.ReadAllBytes(entry)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(target))))
                    throw new IOException($"Различающиеся файлы при переносе: {entry} и {target}. Исходные файлы сохранены.");
                files.Add((entry, target));
            }
        }
        Visit(source);
        foreach (var file in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.Destination)!);
            if (!File.Exists(file.Destination)) File.Copy(file.Source, file.Destination);
            if (!SHA256.HashData(File.ReadAllBytes(file.Source)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(file.Destination))))
                throw new IOException($"Не удалось проверить копию {file.Destination}");
        }
    }
}
