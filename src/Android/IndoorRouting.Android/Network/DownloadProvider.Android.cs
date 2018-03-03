using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;

namespace IndoorRouting
{
    public partial class DownloadProvider
    {
        private async Task DownloadAsyncImpl(Uri downloadUri, string targetPath, IProgress<double> onDownloadProgress)
        {
            var androidUri = Android.Net.Uri.Parse(downloadUri.OriginalString);
            var downloadRequest = new DownloadManager.Request(androidUri);

            var targetFile = new Java.IO.File(targetPath);
            var javaUri = targetFile.ToURI();
            var targetUri = Android.Net.Uri.Parse(javaUri.ToString());

            downloadRequest.AllowScanningByMediaScanner();
            downloadRequest.SetDestinationUri(targetUri);
            downloadRequest.SetNotificationVisibility(DownloadVisibility.VisibleNotifyOnlyCompletion);

            var downloadManager = Application.Context?.GetSystemService(Context.DownloadService) as DownloadManager;
            var downloadId = downloadManager.Enqueue(downloadRequest);

            var downloading = true;

            while (downloading)
            {
                await Task.Delay(500);

                DownloadManager.Query downloadQuery = new DownloadManager.Query();
                downloadQuery.SetFilterById(downloadId);

                var cursor = downloadManager.InvokeQuery(downloadQuery);
                cursor.MoveToFirst();

                if ((DownloadStatus)cursor.GetInt(cursor.GetColumnIndex(DownloadManager.ColumnStatus)) == DownloadManager.StatusSuccessful)
                {
                    downloading = false;
                    onDownloadProgress?.Report(100);
                }
                else
                {
                    var downloadedBytes = cursor.GetInt(cursor.GetColumnIndex(DownloadManager.ColumnBytesDownloadedSoFar));
                    var totalBytes = cursor.GetInt(cursor.GetColumnIndex(DownloadManager.ColumnTotalSizeBytes));

                    var downloadProgress = ((double)downloadedBytes / totalBytes) * 100;
                    onDownloadProgress?.Report(downloadProgress);
                }
            }
        }
    }
}