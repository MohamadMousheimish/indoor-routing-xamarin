using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.UI.Controls;
using IndoorRouting.Attributes;
using IndoorRouting.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorRouting.Android.IndoorTest
{
    [Activity(ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    [Sample("Open Mobile Map (Map Package)", 
            "Map",
            "This sample demonstrate how to open a map from a mobile map package",
            "The map package will be downloaded from an ArcGIS online portal automatically")]
    [OfflineData("52346d5fc4c348589f976b6a279ec3e6")]
    public class IndoorTest: Activity
    {
        // Hold a reference to tha MapView
        private MapView _myMapView;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            Title = "Open Mobile Map (map package)";
            CreateLayout();
            Initialize();
        }

        private void CreateLayout()
        {
            // Create a new vertical layout for the app
            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };

            // Add a map view to the layout
            _myMapView = new MapView(this);
            layout.AddView(_myMapView);

            // Show the layout in the app
            SetContentView(layout);
        }

        private async void Initialize()
        {
            // Get the path to the mobile map package
            var filePath = GetMmpkPath();
            try
            {
                if(await MobileMapPackage.IsDirectReadSupportedAsync(filePath))
                {
                    // Open the map package
                    var myMapPackage = await MobileMapPackage.OpenAsync(filePath);

                    // Display the first map in the package
                    _myMapView.Map = myMapPackage.Maps.First();
                }
                else
                {
                    // Create a path for the unpacked package
                    var unpackedPath = $"{filePath}unpacked";

                    // Unpack the package
                    await MobileMapPackage.UnpackAsync(filePath, unpackedPath);

                    // Open the package
                    var package = await MobileMapPackage.OpenAsync(unpackedPath);

                    // Load the package
                    await package.LoadAsync();

                    // Show the first map
                    _myMapView.Map = package.Maps.First();
                }
            }
            catch (Exception e) 
            {
                new AlertDialog.Builder(this).SetMessage(e.ToString()).SetTitle("Error").Show();
            }
        }

        private string GetMmpkPath()
        {
            return DataManager.GetDataFolder("52346d5fc4c348589f976b6a279ec3e6", "RedlandsCampus.mmpk");
        }

    }
}
