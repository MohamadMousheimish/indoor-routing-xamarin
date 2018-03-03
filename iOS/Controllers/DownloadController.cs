// <copyright file="DownloadController.cs" company="Esri, Inc">
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
namespace IndoorRouting.iOS
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using Foundation;
    using UIKit;
    using System.Threading.Tasks;

    /// <summary>
    /// Download controller contains the UI and logic for the download screen.
    /// </summary>
    internal partial class DownloadController : UIViewController
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:IndoorRouting.iOS.DownloadController"/> class.
        /// </summary>
        /// <param name="handle">Controller Handle.</param>
        private DownloadController(IntPtr handle) : base(handle)
        {
        }

        /// <summary>
        /// Gets or sets  the download view model containing the common logic for setting up the download
        /// </summary>
        public DownloadViewModel ViewModel { get; set; }

        /// <summary>
        /// Overrides the behavior when view is loaded
        /// </summary>
        public override async void ViewDidLoad()
        {
            base.ViewDidLoad();

            // When the application has finished loading, bring in the settings
            string settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            AppSettings.CurrentSettings = await AppSettings.CreateAsync(Path.Combine(settingsPath, "AppSettings.xml")).ConfigureAwait(false);

            this.ViewModel = new DownloadViewModel();
            this.ViewModel.PropertyChanged += this.ViewModelPropertyChanged;

            var isMapCurrent = await ViewModel.IsLocalDataCurrentAsync();
            if (isMapCurrent == true)
                LoadMapView();
            else
                await DownloadAndDisplayMapAsync();
        }

        /// <summary>
        /// Gets called by the delegate and will update the progress bar as the download runs.
        /// </summary>
        /// <param name="percentage">Percentage progress.</param>
        internal void UpdateProgress(float percentage)
        {
            InvokeOnMainThread(() =>
            {
                this.progressView.SetProgress(percentage, true);
            });
        }

        /// <summary>
        /// Gets called by the delegate and tells the controller to load the map controller
        /// </summary>
        private void LoadMapView()
        {
            InvokeOnMainThread(() =>
            {
                var navController = Storyboard.InstantiateViewController("NavController");

                // KeyWindow only works if the application loaded fully. If key window is null, use the first available windowo
                try
                {
                    UIApplication.SharedApplication.KeyWindow.RootViewController = navController;
                }
                catch (NullReferenceException)
                {
                    UIApplication.SharedApplication.Windows[0].RootViewController = navController;
                }
            });
        }

        /// <summary>
        /// Handles button to reload the view 
        /// </summary>
        /// <param name="sender">Sender element.</param>
        partial void RetryButton_TouchUpInside(UIButton sender)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            DownloadAndDisplayMapAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        /// <summary>
        /// Fires when properties change in the DownloadViewModel
        /// </summary>
        /// <param name="sender">Sender element.</param>
        /// <param name="e">Event Args.</param>
        private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DownloadViewModel.DownloadProgress):
                {
                    UpdateProgress((float)ViewModel.DownloadProgress);
                }

                break;
            }
        }

        private async Task<bool> DownloadAndDisplayMapAsync()
        {
            var downloadSucceeded = false;
            InvokeOnMainThread(() =>
            {
                progressView.Hidden = false;
                RetryButton.Hidden = true;
                statusLabel.Text = "Downloading Map...";
            });
            try
            {
                // Call GetData to download or load the mmpk
                await this.ViewModel.DownloadDataAsync().ConfigureAwait(false);
                LoadMapView();
                downloadSucceeded = true;
            }
            catch (Exception ex)
            {
                InvokeOnMainThread(() =>
                {
                    statusLabel.Text = "An error occurred downloading the map.  Please check your internet connection and try again.";
                    progressView.Hidden = true;
                    RetryButton.Hidden = false;
                });
            }
            return downloadSucceeded;
        }
    }
}