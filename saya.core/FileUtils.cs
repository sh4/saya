using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace saya.core
{
    public static partial class FileUtils
    {
        public static string GetShortcutPath(string lnkFilePath)
        {
            return new Shortcut(lnkFilePath).TargetPath;
        }

        public static IEnumerable<string> EnumerateFileEntries(string path)
        {
            var directories = Enumerable.Empty<string>();
            try
            {
                directories = Directory.EnumerateDirectories(path);
            }
            catch (UnauthorizedAccessException) { }

            var files = Enumerable.Empty<string>();
            try
            {
                files = Directory.EnumerateFiles(path);
            }
            catch (UnauthorizedAccessException) { }

            foreach (var filePath in files)
            {
                yield return filePath;
            }

            foreach (var directoryPath in directories)
            {
                foreach (var filePath in EnumerateFileEntries(directoryPath))
                {
                    yield return filePath;
                }
            }
        }
    }
}
