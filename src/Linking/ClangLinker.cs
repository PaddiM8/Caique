using System.Diagnostics;

namespace Caique.Linking;

public class ClangLinker : ILinker
{
    public void Link(IEnumerable<string> objectFilePaths, string outputPath)
    {
        // TODO: Call ld.lld if it exists (uses lld with ld syntax, should be faster)
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "clang",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(outputPath);
        foreach (var objectFilePath in objectFilePaths)
            process.StartInfo.ArgumentList.Add(objectFilePath);

        process.Start();

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"Linking failed:");
            Console.Error.WriteLine(stdout);
            Console.Error.WriteLine(stderr);
        }
    }
}
