using DocTemplater.Domain;
using DocTemplater.Services;

namespace DocTemplater.Tests.Services;

[CollectionDefinition("CurrentDirectory", DisableParallelization = true)]
public class CurrentDirectoryCollection { }

[Collection("CurrentDirectory")]
public class RelativeDirectoryMigrationTests
{
    [Fact]
    public async Task ExistingExternalDotTemplatesIsResolvedAndPreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), "DocTemplater-relative-" + Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.CurrentDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Templates"));
        try
        {
            Environment.CurrentDirectory = root;
            var data = Path.Combine(root, "data");
            var persistence = new ValuePersistenceService(Path.Combine(data, "settings.json"));
            await persistence.SaveSettingsAsync(new AppSettings { TemplatesFolderPath = "./Templates" });
            var safety = new ApplicationDataSafety(data, new[] { Path.Combine(root, "install") }, Path.Combine(root, "install"), () => { });
            var result = await safety.BootstrapAsync(persistence);
            Assert.Equal(Path.Combine(root, "Templates"), result.TemplatesFolderPath);
        }
        finally { Environment.CurrentDirectory = previousDirectory; Directory.Delete(root, true); }
    }
}
