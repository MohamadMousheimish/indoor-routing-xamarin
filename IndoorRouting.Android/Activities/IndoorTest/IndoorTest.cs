using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Text;
using Android.Views;
using Android.Widget;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using IndoorRouting.Attributes;
using IndoorRouting.Managers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Android.Widget.AdapterView;

namespace IndoorRouting.IndoorTest
{
    [Activity(ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    [Sample("Open Mobile Map (Map Package)",
            "Map",
            "This sample demonstrate how to open a map from a mobile map package",
            "The map package will be downloaded from an ArcGIS online portal automatically")]
    [OfflineData("52346d5fc4c348589f976b6a279ec3e6")]
    public partial class IndoorTest : Activity
    {
        // The LocatorTask provides geocoding services via a service.
        private LocatorTask _geocoder;

        // Hold references to the UI Controls
        private MapView _myMapView;
        private AutoCompleteTextView _mySearchBox;
        private ListView _searchListView;
        private ProgressBar _myProgressBar;
        private LocationViewModel _locationViewModel;
        private GraphicsOverlay _waypointOverlay;

        /// <summary>
        /// Gets or sets the map view model containing the common logic for dealing with the map
        /// </summary>
        private MapViewModel ViewModel { get; set; }

        public IndoorTest() : base()
        {
            this.ViewModel = new MapViewModel();
        }

        protected override async void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            Title = "Open Mobile Map (map package)";
            CreateLayout();
            Initialize();
            //await ViewModel.InitializeAndroidMapViewAsync();
            ViewModel.PropertyChanged += ViewModelPropertyChanged;
        }

        private void CreateLayout()
        {
            // Create a new vertical layout for the app
            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };

            // Search Bar
            _mySearchBox = new AutoCompleteTextView(this) { Hint = "Search rooms or people..." };
            layout.AddView(_mySearchBox);

            // Disable multi-line search
            _mySearchBox.SetMaxLines(1);

            //List View
            _searchListView = new ListView(this);
            layout.AddView(_searchListView);

            // Progress bar
            _myProgressBar = new ProgressBar(this) { Indeterminate = true, Visibility = ViewStates.Gone };
            layout.AddView(_myProgressBar);

            // Add a map view to the layout
            _myMapView = new MapView(this);
            layout.AddView(_myMapView);

            // Create and add an overlay for showing waypoints/stops.
            _waypointOverlay = new GraphicsOverlay();
            _myMapView.GraphicsOverlays.Add(_waypointOverlay);

            // Show the layout in the app
            SetContentView(layout);

            // Disable the buttons and search bar until geocoder is ready
            _mySearchBox.Enabled = false;

            // Hook up the UI event handlers for suggestion & search
            _mySearchBox.TextChanged += async (sender, e) =>
            {
                // Call to populate autosuggestions
                await GetSuggestionsFromLocatorAsync();
            };
        }

        private async Task GetSuggestionsFromLocatorAsync()
        {
            var suggestions = await _locationViewModel.GetLocationSuggestionsAsync(_mySearchBox.Text);
            if (suggestions != null && suggestions.Count > 0)
            {
                if(!(suggestions.Count == 1 && suggestions.FirstOrDefault().Label == _mySearchBox.Text))
                {
                    // Dismiss callout, if any.
                    UserInteracted();

                    // Convert the list into a usable format for the suggest box
                    var results = suggestions.Select(s => s.Label).ToList();

                    // Quit if there are no results
                    if (!results.Any())
                    {
                        return;
                    }

                    // Create an array adapter to provide autocomplete suggestions
                    var adapter = new ArrayAdapter(this, Resource.Layout.SimpleSpinnerItem, results);
                    _searchListView.Adapter = adapter;
                    _searchListView.ItemClick += AutoCompleteSearchSelected;
                }
                else
                {
                    // Create an array adapter to provide autocomplete suggestions
                    var adapter = new ArrayAdapter(this, Resource.Layout.SimpleSpinnerItem, new List<string>());
                    _searchListView.Adapter = adapter;
                    _searchListView.ItemClick += AutoCompleteSearchSelected;
                }
            }
            else
            {
                // Create an empty array adapter
                var adapter = new ArrayAdapter(this, Resource.Layout.SimpleSpinnerItem, new List<string>());
                _searchListView.Adapter = adapter;

                // Apply the adapter
                _mySearchBox.Adapter = adapter;
            }
        }

        private async void AutoCompleteSearchSelected(object sender, ItemClickEventArgs e)
       {
            var selectedFromList = (string)_searchListView.GetItemAtPosition(e.Position);
            if(selectedFromList != _mySearchBox.Text)
            {
                _mySearchBox.Text = selectedFromList;
                _mySearchBox.Adapter = new ArrayAdapter(this, Resource.Layout.SimpleSpinnerItem);
                await GetSearchedFeatureAsync(_mySearchBox.Text);
            }
        }

        private async Task GetSearchedFeatureAsync(string searchText)
        {
            var geocodeResult = await _locationViewModel.GetSearchedLocationAsync(searchText);
            //var floorLevel = await _locationViewModel.GetFloorLevelFromQueryAsync(searchText);
            //ViewModel.SelectedFloorLevel = floorLevel;
            if (geocodeResult != null)
            {
                // Create a picture marker symbol
                if (_waypointOverlay.Graphics.Count == 1)
                {
                    _waypointOverlay.Graphics.Remove(_waypointOverlay.Graphics.FirstOrDefault());
                }
                _waypointOverlay.Graphics.Add(await GraphicForPoint(geocodeResult.DisplayLocation));
                _myMapView.SetViewpoint(new Viewpoint(geocodeResult.DisplayLocation, 500));
            }
        }

        private async Task<Graphic> GraphicForPoint(MapPoint point)
        {
            // Get current assembly that contains the image.
            var currentAssembly = Assembly.GetExecutingAssembly();
            // var allRessources = Assembly.GetExecutingAssembly().GetManifestResourceNames();

            // Get image as a stream from the resources.
            // Picture is defined as EmbeddedResource and DoNotCopy.
            var resourceStream = currentAssembly.GetManifestResourceStream("IndoorRouting.Android.Resources.drawable.pin_blue.png");

            if (resourceStream != null)
            {
                // Create new symbol using asynchronous factory method from stream.
                var pinSymbol = await PictureMarkerSymbol.CreateAsync(resourceStream);
                pinSymbol.Width = 60;
                pinSymbol.Height = 60;
                // The image is a pin; offset the image so that the pinpoint
                //     is on the point rather than the image's true center.
                pinSymbol.LeaderOffsetX = 30;
                pinSymbol.OffsetY = 14;
                return new Graphic(point, pinSymbol); 
            }

            return null;
        }

        private async void Initialize()
        {
            // When the application has finished loading, bring in the settings
            var settingsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            AppSettings.CurrentSettings = await AppSettings.CreateAsync(Path.Combine(settingsPath, "AppSettings.xml")).ConfigureAwait(false);

            // Get the path to the mobile map package
            var filePath = GetMmpkPath();
            try
            {
                // Open the map package
                var package = await MobileMapPackage.OpenAsync(Path.Combine(filePath));
                InitializeMapSetting(package);
            }
            catch (Exception e)
            {
                ShowMessage(e.ToString());
            }
        }

        private void InitializeMapSetting(MobileMapPackage mapPackage)
        {
            // Show the first map
            _myMapView.Map = mapPackage.Maps.First();

            // Ask for location permissions. Events wired up in OnRequestPermissionResult
            // AskForLocationPermission();

            // Initialize the LocatorTask with the provided service Uri
            _geocoder = mapPackage.LocatorTask;
            if (_locationViewModel == null)
            {
                _locationViewModel = LocationViewModel.Create(_myMapView.Map, _geocoder);                    
            }
            _mySearchBox.Enabled = true;
        }

        /// <summary>
        /// Fires when properties change in the MapViewModel
        /// </summary>
        /// <param name="sender">Sender element.</param>
        /// <param name="e">Eevent args.</param>
        private async void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Map":
                    if (ViewModel.Map != null)
                    {
                        // Add the map to the MapView to be displayedd
                        _myMapView.Map = ViewModel.Map;
                    }

                    break;
                case "Viewpoint":
                    if (ViewModel.Viewpoint != null)
                    {
                        await _myMapView.SetViewpointAsync(ViewModel.Viewpoint);
                    }

                    break;
            }
        }

        private void UserInteracted()
        {
            // Hide the callout.
            _myMapView.DismissCallout();
        }

        private string GetMmpkPath()
        {
            return DownloadViewModel.GetDataFolder("52346d5fc4c348589f976b6a279ec3e6", "RedlandsCampus.mmpk");
        }

        private void ShowMessage(string message, string title = "Error")
        {
            using (AlertDialog.Builder builder = new AlertDialog.Builder(this))
            {
                builder.SetTitle(title).SetMessage(message).Show();
            }
        }

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
}
