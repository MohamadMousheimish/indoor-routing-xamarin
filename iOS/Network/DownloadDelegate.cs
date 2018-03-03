// <copyright file="DownloadDelegate.cs" company="Esri, Inc">
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
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Foundation;
using UIKit;
using CoreFoundation;
using System.Collections.Generic;

namespace IndoorRouting.iOS
{
    /// <summary>
    /// Download delegate handles logic of the background download.
    /// </summary>
    internal class DownloadDelegate : NSUrlSessionDownloadDelegate
    {
        private IProgress<double> _onDownloadProgress;
        private object _thisLock = new object();
        private Dictionary<string, Tuple<TaskCompletionSource<bool>, string>> _downloadTaskCompletionSources =
            new Dictionary<string, Tuple<TaskCompletionSource<bool>, string>>();

        private string SessionId { get => $"com.esri.DownloadDelegate-{DateTime.Now.Ticks}"; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:IndoorRouting.iOS.DownloadDelegate"/> class.
        /// </summary>
        /// <param name="controller">Download Controller.</param>
        public DownloadDelegate(IProgress<double> onDownloadProgress) : base()
        {
            _onDownloadProgress = onDownloadProgress;
            InitializeNSUrlSession();
        }

        public Task DownloadAsync(Uri downloadUri, string targetPath)
        {
            var downloadUrl = downloadUri.AbsoluteUri;

            lock (_thisLock) // Use a lock to access the shared dictionary in a thread-safe way
            {
                // Check whether there is already a download for this URL in progress by seeing whether there is a task
                // completion source for this URL
                if (_downloadTaskCompletionSources.ContainsKey(downloadUrl))
                {
                    // A download for this URL is already in progress.  Check whether the path to download the file to is
                    // different what's been specified for the in-progress operation.
                    if (_downloadTaskCompletionSources[downloadUrl].Item2 != targetPath)
                    {
                        throw new ArgumentException($"{downloadUri.AbsoluteUri} is already being downloaded to {targetPath}.  "
                            + "This class does not support downloading the same file to multiple locations simultaneously.");
                    }

                    // The target path matches, so return the async task for the download that's already in progress
                    return _downloadTaskCompletionSources[downloadUrl].Item1?.Task;
                }
                else
                {
                    // There's no download for this URL in progress, so create a task completion source to track completion
                    // of the download.
                    var downloadTcs = new TaskCompletionSource<bool>();
                    // Store the completion source along with the path to save the file to in a shared dictionary
                    _downloadTaskCompletionSources[downloadUrl] = Tuple.Create(downloadTcs, targetPath);
                    // Start the download
                    EnqueueDownload(downloadUri);
                    // Return the async task from the new completion source
                    return downloadTcs.Task;
                }
            }
        }

        /// <summary>
        /// Gets called as data is being received
        /// </summary>
        /// <param name="session">NSUrl Session.</param>
        /// <param name="downloadTask">Download task.</param>
        /// <param name="bytesWritten">Bytes written.</param>
        /// <param name="totalBytesWritten">Total bytes written.</param>
        /// <param name="totalBytesExpectedToWrite">Total bytes expected to write.</param>
        public override void DidWriteData(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, long bytesWritten, long totalBytesWritten, long totalBytesExpectedToWrite)
        {
            var localIdentifier = downloadTask.TaskIdentifier;
            var percentage = totalBytesWritten / (float)totalBytesExpectedToWrite;

            _onDownloadProgress?.Report(percentage);
        }

        /// <summary>
        /// Gets called when the download has been completed.
        /// </summary>
        /// <param name="session">NSUrl Session.</param>
        /// <param name="downloadTask">Download task.</param>
        /// <param name="location">NSUrl Location.</param>
        public override void DidFinishDownloading(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, NSUrl location)
        {
            // The download location is the location of the file
            var sourceFile = location.Path;

            // Copy over to documents folder. Note that we must use NSFileManager here! File.Copy() will not be able to access the source location.
            var fileManager = NSFileManager.DefaultManager;

            // Get the URL of the downloaded resource
            var url = downloadTask.OriginalRequest.Url.AbsoluteString;

            string targetPath = null;
            lock (_thisLock) // Use a lock to access the shared dictionary in a thread-safe way
            {
                if (_downloadTaskCompletionSources.ContainsKey(url))
                {
                    // Get the path to save the file
                    targetPath = _downloadTaskCompletionSources[url]?.Item2;
                }
            }

            // Check that there is a target path for saving the downloaded file
            if (!string.IsNullOrEmpty(targetPath))
            {
                NSError error;
                // Remove any existing files in our destination
                if (File.Exists(targetPath))
                {
                    var fileDeleted = fileManager.Remove(targetPath, out error);
                    if (!fileDeleted)
                    {
                        Console.WriteLine($"Error deleting the file from {targetPath}: {error.LocalizedDescription}");
                    }
                }

                var success = fileManager.Copy(sourceFile, targetPath, out error);
                if (!success)
                {
                    Console.WriteLine($"Error during the copy: {error.LocalizedDescription}");
                }
            }
            else
            {
                Console.WriteLine($"Target path for {url} not found");
            }
        }

        /// <summary>
        /// Gets called when a download is done. Does not necessarily indicate an error
        /// unless the NSError parameter is not null.
        /// </summary>
        /// <param name="session">NSUrl Session.</param>
        /// <param name="task">Session Task.</param>
        /// <param name="error">Error received.</param>
        public override void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError error)
        {
            var url = task.OriginalRequest.Url.AbsoluteString;

            TaskCompletionSource<bool> downloadTcs = null;
            lock (_thisLock) // Use a lock to access the shared dictionary in a thread-safe way
            {
                if (_downloadTaskCompletionSources.ContainsKey(url))
                {
                    // Get the task completion source for the download URL
                    downloadTcs = _downloadTaskCompletionSources[url]?.Item1;

                    // Download has finished, so remove the corresponding entry from the dictionary
                    _downloadTaskCompletionSources.Remove(url);
                }
            }

            if (error == null)
            {
                downloadTcs?.TrySetResult(true);
            }
            else
            {
                // If error indeed occured, cancel the task
                task.Cancel();

                downloadTcs.TrySetException(new NSErrorException(error));
            }
        }
        
        private NSUrlSession session;

        /// <summary>
        /// Initializes the NSUrl session.
        /// </summary>
        private void InitializeNSUrlSession()
        {
            // Initialize session config. Use a background session to enabled out of process uploads/downloads.
            using (var sessionConfig = UIDevice.CurrentDevice.CheckSystemVersion(8, 0)
                ? NSUrlSessionConfiguration.CreateBackgroundSessionConfiguration(SessionId)
                : NSUrlSessionConfiguration.BackgroundSessionConfiguration(SessionId))
            {
                // Allow downloads over cellular network
                sessionConfig.AllowsCellularAccess = true;

                // Give the OS a hint about what we are downloading. This helps iOS to prioritize. For example "Background" is used to download data that was not requested by the user and
                // should be ready if the app gets activated.
                sessionConfig.NetworkServiceType = NSUrlRequestNetworkServiceType.Default;

                // Configure how many downloads to allow at the same time. Set to 1 since we only meed to download one file
                sessionConfig.HttpMaximumConnectionsPerHost = 1;

                // Create a session delegate and the session itself
                // Initialize the session itself with the configuration and a session delegate.
                this.session = NSUrlSession.FromConfiguration(sessionConfig, (INSUrlSessionDelegate)this, null);
            }
        }

        /// <summary>
        /// Adds the download to the session.
        /// </summary>
        /// <param name="downloadUri">Download URI for the mmpk.</param>
        private void EnqueueDownload(Uri downloadUri)
        {
            // Create a new download task.
            var downloadTask = this.session.CreateDownloadTask(NSUrl.FromString(downloadUri.AbsoluteUri));
            // Alert user if download fails
            if (downloadTask == null)
            {
                DispatchQueue.MainQueue.DispatchAsync(() =>
                {
                    var okAlertController = UIAlertController.Create("Download Error", "Failed to create download task, please retry", UIAlertControllerStyle.Alert);
                    okAlertController.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(okAlertController, true, null);
                });
                return;
            }

            // Resume / start the download.
            downloadTask.Resume();
        }
    }
}
