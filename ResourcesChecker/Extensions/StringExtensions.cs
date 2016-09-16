using System;

namespace ResourcesChecker.Extensions
{
    public static class StringExtensions
    {
        public static bool IsCshtml(this string fileName)
        {
            return fileName.IndexOf(".cshtml", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsCs(this string fileName)
        {
            return fileName.IndexOf(".cs", StringComparison.OrdinalIgnoreCase) >= 0 &&
                fileName.IndexOf(".generated.cs", StringComparison.OrdinalIgnoreCase) == -1 &&
                fileName.IndexOf(".designer.cs", StringComparison.OrdinalIgnoreCase) == -1;
        }

        public static bool IsJs(this string fileName)
        {
            return fileName.IndexOf(".js", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
