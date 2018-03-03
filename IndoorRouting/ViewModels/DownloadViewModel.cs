// <copyright file="DownloadViewModel.cs" company="Esri, Inc">
//      Copyright 2017 Esri.
//
//      Licensed under the Apache License, Version 2.0 (the "License");
//      you may not use this file except in compliance with the License.
//      You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//      Unless required by applicable law or agreed to in writing, software
//      distributed under the License is distributed on an "AS IS" BASIS,
//      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//      See the License for the specific language governing permissions and
//      limitations under the License.
// </copyright>
// <author>Mara Stoica</author>
namespace IndoorRouting
{ 
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Esri.ArcGISRuntime.Portal;

    /// <summary>
    /// View model to handle common download logic between platforms
    /// </summary>
    internal class DownloadViewModel : INotifyPropertyChanged
    {
        private DownloadProvider _downloadProvider = new DownloadProvider();
        private ArcGISPortal portal;
        private PortalItem item;

        /// <summary>
        /// Gets set to true when the mmpk is downloading.
        /// </summary>
        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                _isDownloading = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Event handler property changed. 
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private string _targetFileName = Path.Combine(GetDataFolder(), AppSettings.CurrentSettings.PortalItemName);

        private double _downloadProgress;
        public double DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                _downloadProgress = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Checks whether previously downloaded ArcGIS Portal item data is up to date
        /// </summary>
        /// <returns>A value indicating whether the previously downloaded data is curren - true if so, false if not, or null if
        /// unable to determine.</returns>
        public async Task<bool?> IsLocalDataCurrentAsync()
        {
            bool? dataUpToDate;
            try
            {
                if (!Reachability.IsNetworkAvailable())
                {
                    dataUpToDate = null;
                }
                else if (!File.Exists(_targetFileName))
                {
                    dataUpToDate = false;
                }
                else
                {
                    await InitializePortalItem();

                    dataUpToDate = item.Modified.LocalDateTime > AppSettings.CurrentSettings.MmpkDownloadDate;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{ex.Message}\n{ex.StackTrace}");
                dataUpToDate = null;
            }

            return dataUpToDate;
        }

        private async Task InitializePortalItem()
        {
            portal = portal ?? await ArcGISPortal.CreateAsync().ConfigureAwait(false);
            item = item ?? await PortalItem.CreateAsync(portal, AppSettings.CurrentSettings.PortalItemID).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads the data for the map
        /// </summary>
        /// <returns>The map data.</returns>
        public async Task DownloadDataAsync()
        {
            // Test if device is online
            // If offline, test if mmpk exists and load it
            // If offline and no mmpk, show error
            // Show error message if unable to downoad the mmpk. This is usually when the device is online but signal isn't strong enough and connection to Portal times out
            if (Reachability.IsNetworkAvailable())
            {
                try
                {
                    await InitializePortalItem();
                    var downloadUri = new Uri($"{item.Url.AbsoluteUri}/data");
                    await _downloadProvider.DownloadAsync(downloadUri, _targetFileName,
                        new Progress<double>((progress) => DownloadProgress = progress));
                    AppSettings.CurrentSettings.MmpkDownloadDate = DateTime.Now;

                    // Save user settings
                    await Task.Run(() => AppSettings.SaveSettings(Path.Combine(GetDataFolder(), "AppSettings.xml")));
                }
                catch (Exception ex)
                {
                    // Should we swallow this?
                }
            }
            else
            {
                // TODO: what should happen if device is not connected?  Throw exception?  Return null?
            }
        }

        /// <summary>
        /// Gets the data folder where the mmpk and settings file are stored.
        /// </summary>
        /// <returns>The data folder.</returns>
        internal static string GetDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        /// <summary>
        /// Called when a property changes to trigger PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of property that changed.</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
