namespace Caique.Tests;

[SetUpFixture]
public class SetUp
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        if (Directory.Exists(binPath))
            Directory.Delete(binPath, recursive: true);
    }
}
