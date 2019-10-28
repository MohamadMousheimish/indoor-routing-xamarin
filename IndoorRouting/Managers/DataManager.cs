using Esri.ArcGISRuntime.Portal;
using IndoorRouting.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IndoorRouting.Managers
{
    public static class DataManager
    {

        /// <summary>
        /// Downloads a portal item and leaves markers to track download date.
        /// </summary>
        /// <param name="item">Portal item to be downloaded</param>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <returns></returns>
        private static async Task DownloadItem(PortalItem item, CancellationToken cancellationToken)
        {
            // Get Sample Data Directory
            var dataDir = Path.Combine(GetDataFolder(), item.ItemId);

            // Create Directory Matching item Id if it did not exist
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            // Get the download task
            var downloadTask = item.GetDataAsync(cancellationToken);

            // Get the path to the destination file
            var tempFile = Path.Combine(dataDir, item.Name);

            // Download the file
            using (var s = await downloadTask.ConfigureAwait(false))
            {
                using (var f = File.Create(tempFile))
                {
                    await s.CopyToAsync(f).WithCancellation(cancellationToken).ConfigureAwait(false);
                }
            }

            // Unzip the file if it is a zip archive
            if (tempFile.EndsWith(".zip"))
            {
                await UnpackData(tempFile, dataDir, cancellationToken);
            }

            // Write the __sample.config file. 
            // This is used to ensure that cached data did not go out-of-date.
            var configFilePath = Path.Combine(dataDir, "__sample.config");
            File.WriteAllText(configFilePath, $@"Data downloaded: {DateTime.Now}");
        }

        /// <summary>
        /// Determines if a portal item has been downloaded and is up-to-date.
        /// </summary>
        /// <param name="item">The portal item to check.</param>
        /// <returns><c>True</c> If data is avaiable and up-tp-date, false otherwise</returns>
        private static bool IsDataPresent(PortalItem item)
        {
            // Look for __sample.config file. Return false if not present
            var configPath = Path.Combine(GetDataFolder(item.ItemId), "__sample.config");
            if (!File.Exists(configPath))
            {
                return false;
            }

            // Get the last write date from the __sample.config file metadata
            var downloadDate = File.GetLastWriteTime(configPath);

            return downloadDate >= item.Modified;
        }

        public static async Task EnsureSampleDataPresent(SampleInfo info)
        {
            await EnsureSampleDataPresent(info, CancellationToken.None);
        }

        /// <summary>
        /// Ensures that data needed for a sample has been downloaded and is uo-to-date
        /// </summary>
        /// <param name="sample">The sample to ensure data is present for</param>
        /// <param name="token">Cancellation token</param>
        /// <returns></returns>
        public static async Task EnsureSampleDataPresent(SampleInfo sample, CancellationToken token)
        {
            // Return if there's nothing to do
            if(sample.OfflineDataItems == null || !sample.OfflineDataItems.Any())
            {
                return;
            }

            // Hold a list of download Tasks (to enable parallel download)
            var downloads = new List<Task>();
            foreach (var itemId in sample.OfflineDataItems)
            {
                // Create ArcGIS portal Item
                var portal = await ArcGISPortal.CreateAsync(token).ConfigureAwait(false);
                var item = await PortalItem.CreateAsync(portal, itemId, token).ConfigureAwait(false);

                // Download item if not already present
                if (!IsDataPresent(item))
                {
                    var downloadTask = DownloadItem(item, token);
                    downloads.Add(downloadTask);
                }

                // Wait for all downloads to complete
                await Task.WhenAll(downloads).WithCancellation(token);
            }
        }

        public static async Task WithCancellation(this Task baseTask, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            using(token.Register(s=>
            {
                ((TaskCompletionSource<bool>)s).TrySetResult(true);
            }, tcs))
            {
                if(baseTask != await Task.WhenAny(baseTask, tcs.Task))
                {
                    throw new OperationCanceledException(token);
                }
            }
        }

        /// <summary>
        ///Unzip the file at path defined by <paramref name="zipFile"/>
        ///into <paramref name="folder"/>
        /// </summary>
        /// <param name="zipFile">Path to the Zip archive to extract</param>
        /// <param name="folder">Destination folder</param>
        /// <param name="token">Cancelation token</param>
        /// <returns></returns>
        private static Task UnpackData(string zipFile, string folder, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using (var archive = ZipFile.OpenRead(zipFile))
                {
                    foreach (var entry in archive.Entries.Where(m=> !String.IsNullOrEmpty(m.Name)))
                    {
                        var path = Path.Combine(folder, entry.FullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        entry.ExtractToFile(path, true);
                    }
                }
            }, token).WithCancellation(token);
        }

        /// <summary>
        /// Gets the data folder where locally provisioned data is stored
        /// </summary>
        /// <returns></returns>
        internal static string GetDataFolder()
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string sampleDataFolder = Path.Combine(appDataFolder, "ArcGISRuntimeSampleData");
            if (!Directory.Exists(sampleDataFolder))
            {
                Directory.CreateDirectory(sampleDataFolder);
            }
            return sampleDataFolder;
        }

        /// <summary>
        /// Gets the path to an item on disk. 
        /// The item must have already been downloaded for the path to be valid.
        /// </summary>
        /// <param name="itemId">ID of the portal item</param>
        /// <returns></returns>
        internal static string GetDataFolder(string itemId) => Path.Combine(GetDataFolder(), itemId);

    }
}
