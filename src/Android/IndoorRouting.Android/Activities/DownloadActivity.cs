using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using System.ComponentModel;
using System.Threading.Tasks;
using System.IO;

namespace IndoorRouting
{
    [Activity(Label = "@string/ApplicationName", MainLauncher = true, Icon = "@drawable/icon", ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class DownloadActivity : Activity
    {
        private readonly Handler _uiThreadHandler = new Handler(Looper.MainLooper);
        private ProgressBar _checkMapUpdateProgressBar;
        private ProgressBar _progressView;
        private TextView _statusLabel;
        private DownloadViewModel _downloadViewModel;

        protected async override void OnCreate(Bundle savedInstanceState)
        {
            RequestWindowFeature(WindowFeatures.NoTitle);

            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Download);

            _progressView = FindViewById<ProgressBar>(Resource.Id.DownloadProgressBar);
            _checkMapUpdateProgressBar = FindViewById<ProgressBar>(Resource.Id.CheckMapUpdateProgressBar);
            _statusLabel = FindViewById<TextView>(Resource.Id.DownloadProgressText);

            // Initialize app settings
            string settingsPath =
#if __ANDROID__
                Android.App.Application.Context?.GetExternalFilesDir(Android.OS.Environment.DirectoryDownloads).Path;
#else
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
#endif
            AppSettings.CurrentSettings = await AppSettings.CreateAsync(Path.Combine(settingsPath, "AppSettings.xml")).ConfigureAwait(false);

            _downloadViewModel = new DownloadViewModel();
            _downloadViewModel.PropertyChanged += ViewModelPropertyChanged;

            //var isMapCurrent = await _downloadViewModel.IsLocalDataCurrentAsync();
            //if (isMapCurrent == true)
                LoadMapView();
            //else
                //await DownloadAndDisplayMapAsync();


            // Create your application here
        }

        /// <summary>
        /// Gets called by the delegate and tells the controller to load the map controller
        /// </summary>
        private void LoadMapView()
        {
            StartActivity(typeof(MapActivity));
        }

//        /// <summary>
//        /// Handles button to reload the view 
//        /// </summary>
//        /// <param name="sender">Sender element.</param>
//        partial void RetryButton_TouchUpInside(UIButton sender)
//        {
//#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
//            DownloadAndDisplayMapAsync();
//#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        //}

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
                    _progressView.SetProgress((int)_downloadViewModel.DownloadProgress, animate: true);
                    break;
            }
        }

        private async Task<bool> DownloadAndDisplayMapAsync()
        {
            var downloadSucceeded = false;

            _uiThreadHandler.Post(() =>
            {
                _checkMapUpdateProgressBar.Visibility = ViewStates.Invisible;
                _progressView.Visibility = ViewStates.Visible;

                //RetryButton.Hidden = true;
                _statusLabel.Text = "Downloading Map...";
            });
            try
            {
                // Call GetData to download or load the mmpk
                await _downloadViewModel.DownloadDataAsync().ConfigureAwait(false);
                LoadMapView();
                downloadSucceeded = true;
            }
            catch
            {
                _uiThreadHandler.Post(() =>
                {
                    _statusLabel.Text = "An error occurred downloading the map.  Please check your internet connection and try again.";
                    _progressView.Visibility = ViewStates.Invisible;
                    //RetryButton.Hidden = false;
                });
            }
            return downloadSucceeded;
        }
    }
}