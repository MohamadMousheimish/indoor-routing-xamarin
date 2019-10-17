using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Views;
using Android.Widget;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.UI;
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
    public partial class IndoorTest: Activity
    {
        // The LocatorTask provides geocoding services via a service.
        private LocatorTask _geocoder;

        //Service Uri to be provided to the LocatorTask (geocoder).
        private Uri _serviceUri = new Uri("https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer");

        // Hold references to the UI Controls
        private MapView _myMapView;
        private AutoCompleteTextView _mySearchBox;
        private Button _mySearchButton;
        private ProgressBar _myProgressBar;
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

            // Search Bar
            _mySearchBox = new AutoCompleteTextView(this) { Text = "Search rooms or people..." };
            layout.AddView(_mySearchBox);

            // Disable multi-line search
            _mySearchBox.SetMaxLines(1);

            // Search buttons; horizontal layout
            var param = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent,
                1.0f
            );
            var searchButtonLayout = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            _mySearchButton = new Button(this) { Text = "Search All", LayoutParameters = param };

            // Add the buttons to the layout
            searchButtonLayout.AddView(_mySearchButton);

            // Progress bar
            _myProgressBar = new ProgressBar(this) { Indeterminate = true, Visibility = ViewStates.Gone };
            layout.AddView(_myProgressBar);

            // Add the layout to the view
            layout.AddView(searchButtonLayout);

            // Add a map view to the layout
            _myMapView = new MapView(this);
            layout.AddView(_myMapView);

            // Show the layout in the app
            SetContentView(layout);

            // Disable the buttons and search bar until geocoder is ready
            _mySearchBox.Enabled = false;
            _mySearchButton.Enabled = false;

            // Hook up the UI event handlers for suggestion & search
            _mySearchBox.TextChanged += MySearchBoxTextChanged;
            _mySearchButton.Click += MySearchButtonClicked;
        }

        private async void Initialize()
        {
            // Get the path to the mobile map package
            var filePath = GetMmpkPath();
            try
            {
                MobileMapPackage package = null;
                if(await MobileMapPackage.IsDirectReadSupportedAsync(filePath))
                {
                    // Open the map package
                    package = await MobileMapPackage.OpenAsync(filePath);
                }
                else
                {
                    // Create a path for the unpacked package
                    var unpackedPath = $"{filePath}unpacked";

                    // Unpack the package
                    await MobileMapPackage.UnpackAsync(filePath, unpackedPath);

                    // Open the package
                    package = await MobileMapPackage.OpenAsync(unpackedPath);

                    // Load the package
                    await package.LoadAsync();

                }
                await InitializeMapSetting(package); 
            }
            catch (Exception e) 
            {
                new AlertDialog.Builder(this).SetMessage(e.ToString()).SetTitle("Error").Show();
            }
        }
         
        private async Task InitializeMapSetting(MobileMapPackage mapPackage)
        {
            // Show the first map
            _myMapView.Map = mapPackage.Maps.First();

            // Ask for location permissions. Events wired up in OnRequestPermissionResult
            AskForLocationPermission();

            // Initialize the LocatorTask with the provided service Uri
            _geocoder = await LocatorTask.CreateAsync(_serviceUri);

            _mySearchBox.Enabled = true;
            _mySearchButton.Enabled = true;
        }

        private string GetMmpkPath()
        {
            return DataManager.GetDataFolder("52346d5fc4c348589f976b6a279ec3e6", "RedlandsCampus.mmpk");
        }

        private void ShowMessage(string message, string title = "Error") => new AlertDialog.Builder(this).SetTitle(title).SetMessage(message).Show();

        private void LocationDisplayChanged(object sender, Location e)
        {
            // Return if no location
            if(e.Position == null)
            {
                return;
            }

            // Unsbscribe; only want to zoom to location once
            ((LocationDisplay)sender).LocationChanged += LocationDisplayChanged;
            RunOnUiThread(() => { _myMapView.SetViewpoint(new Viewpoint(e.Position, 10000)); });
        }

    }

    public partial class IndoorTest: ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private const int LocationPermissionRequestCode = 99;

        private async void AskForLocationPermission()
        {
            // Only check if permission hasn't been granted yet
            if(ContextCompat.CheckSelfPermission(this, LocationService) != Permission.Granted)
            {
                // The fine location permission will be requested
                var requiredPermissions = new[] { Manifest.Permission.AccessFineLocation };

                // Only prompt the user first if the system sasys to
                if(ActivityCompat.ShouldShowRequestPermissionRationale(this, Manifest.Permission.AccessFineLocation))
                {
                    // A snackbar is a small notice that shows on the bottom of the view
                    Snackbar.Make(_myMapView, "Location permission is needed to display location on the map", Snackbar.LengthIndefinite)
                            .SetAction("Ok", delegate
                            {
                                // When the user presses 'Ok', the system will show the standard permission dialog.
                                // Once the user has accepted or denied, OnRequestPermissionsResult is called with the result.
                                ActivityCompat.RequestPermissions(this, requiredPermissions, LocationPermissionRequestCode);
                            })
                            .Show();
                }
                else
                {
                    // When the user presses 'Ok', the system will show the standard permission dialog
                    // Once the user has accepted or denied, OnRequestPermissionResult is called with the result
                    RequestPermissions(requiredPermissions, LocationPermissionRequestCode);
                }
            }
            else
            {
                try
                {
                    // Explicit Datasource. LoadAsync call is used to surface any errors that may arise
                    await _myMapView.LocationDisplay.DataSource.StartAsync();
                    _myMapView.LocationDisplay.IsEnabled = true;
                    _myMapView.LocationDisplay.LocationChanged += LocationDisplayChanged;
                }
                catch(Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                    ShowMessage(e.Message, "Failed to start location display");
                }
            }
        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            // Ignore other location requests
            if(requestCode != LocationPermissionRequestCode)
            {
                return;
            }

            // If the permissions were granted, enable location
            if(grantResults.Length == 1 && grantResults[0] == Permission.Granted)
            {
                System.Diagnostics.Debug.WriteLine("User affirmatively gave permission to use location. Enabling location");
                try
                {
                    // Explicit DataSource. LoadAsync call is used to surface any errors that may arrise
                    await _myMapView.LocationDisplay.DataSource.StartAsync();
                    _myMapView.LocationDisplay.IsEnabled = true;
                    _myMapView.LocationDisplay.LocationChanged += LocationDisplayChanged;
                }
                catch(Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                    ShowMessage(e.Message, "Failed to start location display");
                }
            }
            else
            {
                ShowMessage("Location permissions not granted.", "Failed to start location display");
            }
        }
    }
}
