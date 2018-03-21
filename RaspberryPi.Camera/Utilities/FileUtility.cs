using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace RaspberryPi.Camera.Utilities
{
    public class FileUtility
    {
        public static async Task<StorageFile> EnsureFileCreatedAsync(string path)
        {
            StorageFile sf = null;

            try
            {
                sf = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
            }
            catch
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(GetDirectoryOfPath(path));
                sf = await folder.CreateFileAsync(Path.GetFileName(path)).AsTask().ConfigureAwait(false);
            }

            return sf;
        }

        public static string GetDirectoryOfPath(string fullPath)
        {
            string fileName = Path.GetFileName(fullPath);
            return fullPath.Replace(fileName, "");
        }
    }
}
