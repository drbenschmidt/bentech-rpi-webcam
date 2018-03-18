using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using System.IO;
using RaspberryPi.Camera.Serialization;

namespace RaspberryPi.Camera.Configuration
{
    public static class OptionsHelper
    {
        public static readonly string FILE_NAME = "configuration.json";

        public static async Task<Options> LoadAsync()
        {
            StorageFile configFile;
            Options result;
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

            try
            {
                configFile = await localFolder.GetFileAsync(FILE_NAME).AsTask().ConfigureAwait(false);
            }
            catch
            {
                configFile = await localFolder.CreateFileAsync(FILE_NAME).AsTask().ConfigureAwait(false);

                using (var fileWriter = await configFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    JsonSerializer.Serialize(new Options(), fileWriter.AsStream());

                    await fileWriter.FlushAsync().AsTask().ConfigureAwait(false);
                }

                return new Options();
            }

            using (var reader = await configFile.OpenReadAsync().AsTask().ConfigureAwait(false))
            {
                result = JsonSerializer.Deserialize<Options>(reader.AsStream());

                return result;
            }
        }

        public static async Task SaveAsync()
        {
            throw new NotImplementedException();
        }
    }
}
