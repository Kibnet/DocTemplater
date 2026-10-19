using Avalonia.Headless;
using Xunit;

// One Avalonia Application/dispatcher is shared by these native-style window tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

[assembly: AvaloniaTestApplication(typeof(DocTemplater.UITests.AppBuilderTestPlatformHelper))]
