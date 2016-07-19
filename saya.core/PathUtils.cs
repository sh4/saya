using System.IO;

namespace saya.core
{
    static public class PathUtils
    {
        public static string NormalizePath(string path)
        {
            var nativePath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(nativePath))
            {
                return nativePath;
            }
            return Path.GetFullPath(nativePath).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        }

        public static bool EqualsPath(string a, string b)
        {
            if (a == null && b == null)
            {
                return true;
            }
            else if (a == null || b == null)
            {
                return false;
            }
            else
            {
                return NormalizePath(a) == NormalizePath(b);
            }
        }
    }
}
