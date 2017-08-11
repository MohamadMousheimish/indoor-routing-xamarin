using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IndoorRouting
{
    public partial class DownloadProvider
    {
        public Task DownloadAsync(Uri downloadUri, string targetPath, IProgress<double> onDownloadProgress)
        {
            return DownloadAsyncImpl(downloadUri, targetPath, onDownloadProgress);
        }
    }
}
