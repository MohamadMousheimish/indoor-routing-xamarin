using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Foundation;
using UIKit;
using System.Threading.Tasks;
using IndoorRouting.iOS;

namespace IndoorRouting
{
    public partial class DownloadProvider
    {
        private IProgress<double> _onDownloadProgress;
        private DownloadDelegate _downloadDelegate;

        private Task DownloadAsyncImpl(Uri downloadUri, string targetPath, IProgress<double> onDownloadProgress)
        {
            _onDownloadProgress = onDownloadProgress;
            _downloadDelegate = _downloadDelegate ?? new DownloadDelegate(onDownloadProgress);
            return _downloadDelegate.DownloadAsync(downloadUri, targetPath);
        }

    }
}