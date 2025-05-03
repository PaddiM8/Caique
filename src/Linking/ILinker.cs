namespace Caique.Linking;

public interface ILinker
{
    void Link(IEnumerable<string> objectFilePaths, string outputPath);
}
