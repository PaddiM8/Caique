namespace Caique.Tests;

[SetUpFixture]
public class SetUp
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testProject");
        Directory.Delete(binPath, recursive: true);
    }
}
