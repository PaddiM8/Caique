using System;
using System.IO;
using Microsoft.Extensions.FileProviders;

namespace Caique.CLI
{
    static class FileProvider
    {
        private static readonly ManifestEmbeddedFileProvider _embeddedProvider =
            new ManifestEmbeddedFileProvider(typeof(Program).Assembly);

        public static string GetFileContent(string fileName)
        {
            var stream = _embeddedProvider.GetFileInfo("Resources/" + fileName)
                .CreateReadStream();
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}