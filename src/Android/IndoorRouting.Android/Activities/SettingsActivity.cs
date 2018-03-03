
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Interop;

namespace IndoorRouting
{
    [Activity(Label = "Settings")]
    public class SettingsActivity : Activity
    {
        Switch _enableLocationSwitch;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Settings);

            _enableLocationSwitch = FindViewById<Switch>(Resource.Id.EnableLocationSwitch);
            _enableLocationSwitch.Checked = AppSettings.CurrentSettings.IsLocationServicesEnabled;
        }

        [Export("EnableLocation_Click")]
        public void EnableLocation_Click(View view)
        {
            AppSettings.CurrentSettings.IsLocationServicesEnabled = ((Switch)view).Checked;
            Task.Run(() => AppSettings.SaveSettings(Path.Combine(DownloadViewModel.GetDataFolder(), "AppSettings.xml")));
        }

        [Export("EnableRouting_Click")]
        public void EnableRouting_Click(View view)
        {
            AppSettings.CurrentSettings.IsRoutingEnabled = ((Switch)view).Checked;
            Task.Run(() => AppSettings.SaveSettings(Path.Combine(DownloadViewModel.GetDataFolder(), "AppSettings.xml")));
        }
    }
}
