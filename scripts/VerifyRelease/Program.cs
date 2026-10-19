using System.Security.Cryptography;
using System.Text.Json;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

// Hosting acceptance only: never calls apply and never touches the real installed application/profile.
var output = Path.GetFullPath(args[0]);
if (Directory.Exists(output)) throw new IOException("Use a fresh probe directory.");
Directory.CreateDirectory(output);
var packages = Path.Combine(output, "packages");
Directory.CreateDirectory(packages);
var locator = new TestVelopackLocator("DocTemplater", "0.0.1", packages,
    Path.Combine(output, "app"), output, Path.Combine(output, "Update.exe"), channel: "win");
var manager = new UpdateManager(new GithubSource("https://github.com/Kibnet/DocTemplater", accessToken: null, prerelease: false), locator: locator);
var update = await manager.CheckForUpdatesAsync() ?? throw new IOException("Published update was not found.");
if (update.TargetFullRelease.Version.ToString() != "0.1.0") throw new IOException("Unexpected published version.");
await manager.DownloadUpdatesAsync(update);
var file = Directory.GetFiles(packages, "*-full.nupkg").Single();
var receipt = new { Source = "https://github.com/Kibnet/DocTemplater", Channel = "win", Version = update.TargetFullRelease.Version.ToString(),
    Package = file, Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file))), Size = new FileInfo(file).Length, Applied = false };
await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Live GitHubSource check/download PASS; no installation/apply performed by probe.");
