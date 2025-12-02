using System.IO;

namespace AnimeBingeDownloader.Services
{
    /// <summary>
    /// Provides an interface for accessing application-specific files 
    /// within the user's AppData/Local directory.
    /// </summary>
    public static class AppStorageService
    {
        // Define your application name. This creates the folder:
        // C:\Users\<Username>\AppData\Local\AnimeDownloader
        private const string ApplicationFolderName = "AnimeDownloader";

        /// <summary>
        /// Gets the root directory for the application's local data.
        /// </summary>
        public static string AppBaseDirectory { get; }

        static AppStorageService()
        {
            // 1. Get the path to the user's Local AppData folder (e.g., C:\Users\User\AppData\Local)
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // 2. Combine with the application's folder name
            AppBaseDirectory = Path.Combine(localAppData, ApplicationFolderName);

            // 3. Ensure the directory exists.
            EnsureDirectoryExists(AppBaseDirectory);
        }

        /// <summary>
        /// Gets the full path for a file or subdirectory within the application's base directory.
        /// </summary>
        /// <param name="paths">One or more path segments (e.g., "Settings", "config.json")</param>
        /// <returns>The full absolute path.</returns>
        public static string GetPath(params string[]? paths)
        {
            if (paths == null || paths.Length == 0)
            {
                return AppBaseDirectory;
            }

            // Start with the base directory

            // Append all path segments

            return paths.Aggregate(AppBaseDirectory, Path.Combine);
        }

        /// <summary>
        /// Ensures a specific directory path exists, creating it if necessary.
        /// </summary>
        /// <param name="path">The full path of the directory to create.</param>
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}