using System;
using System.IO;
using System.Text;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// Locates the <c>GeoTestData</c> folder — the large, Git-LFS-tracked spatial fixtures that are
    /// deliberately NOT embedded into the test assembly. It probes the folder next to the test assembly
    /// and then up to five parent folders (<c>../GeoTestData</c>, <c>../../GeoTestData</c>, …), which
    /// finds it in the source tree during a normal build/run.
    /// </summary>
    public static class GeoTestData
    {
        private const string FolderName = "GeoTestData";
        private const int MaxParentLevels = 5;
        private static readonly Lazy<string> mRoot = new Lazy<string>(Locate);

        /// <summary>The resolved <c>GeoTestData</c> folder. Throws when it cannot be found.</summary>
        public static string Root
            => mRoot.Value ?? throw new DirectoryNotFoundException(
                $"The '{FolderName}' folder was not found next to the test assembly or within {MaxParentLevels} parent folders. " +
                "Ensure the repository is checked out and Git LFS content is pulled ('git lfs pull').");

        /// <summary>Tries to locate the folder without throwing.</summary>
        /// <param name="root">The resolved folder, or <c>null</c> when not found.</param>
        public static bool TryGetRoot(out string root)
        {
            root = mRoot.Value;
            return root != null;
        }

        /// <summary>Builds an absolute path to a file or subfolder inside <c>GeoTestData</c>.</summary>
        /// <param name="relativeParts">Path segments relative to the folder root.</param>
        public static string GetPath(params string[] relativeParts)
        {
            string path = Root;
            if (relativeParts != null)
                for (int i = 0; i < relativeParts.Length; i++)
                    path = Path.Combine(path, relativeParts[i]);
            return path;
        }

        /// <summary>Reads a file inside <c>GeoTestData</c> as raw bytes.</summary>
        /// <param name="relativeParts">Path segments relative to the folder root.</param>
        public static byte[] ReadAllBytes(params string[] relativeParts)
        {
            string path = GetPath(relativeParts);
            byte[] bytes = File.ReadAllBytes(path);
            GuardAgainstLfsPointer(path, bytes);
            return bytes;
        }

        /// <summary>Reads a file inside <c>GeoTestData</c> as UTF-8 text (trimmed).</summary>
        /// <param name="relativeParts">Path segments relative to the folder root.</param>
        public static string ReadAllText(params string[] relativeParts)
        {
            string path = GetPath(relativeParts);
            byte[] bytes = File.ReadAllBytes(path);
            GuardAgainstLfsPointer(path, bytes);
            return Encoding.UTF8.GetString(bytes).Trim();
        }

        private static string Locate()
        {
            // Level 0 = the folder next to the assembly; levels 1..MaxParentLevels = parent folders.
            string current = AppContext.BaseDirectory;
            for (int level = 0; level <= MaxParentLevels && current != null; level++)
            {
                string candidate = Path.Combine(current, FolderName);
                if (Directory.Exists(candidate))
                    return candidate;
                current = Directory.GetParent(current)?.FullName;
            }
            return null;
        }

        private static void GuardAgainstLfsPointer(string path, byte[] bytes)
        {
            // A not-yet-pulled Git LFS file is a tiny text pointer beginning with this marker.
            const string marker = "version https://git-lfs";
            if (bytes.Length < 512)
            {
                int probe = Math.Min(bytes.Length, marker.Length);
                if (probe == marker.Length && Encoding.ASCII.GetString(bytes, 0, probe) == marker)
                    throw new InvalidOperationException(
                        $"'{path}' is a Git LFS pointer, not its content. Run 'git lfs pull' to fetch the test data.");
            }
        }
    }
}
