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
using Esri.ArcGISRuntime;
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
        private ListView _floorsTableView;
        private ProgressBar _myProgressBar;

        /// <summary>
        /// Gets or sets the map view model containing the common logic for dealing with the map
        /// </summary>
        private MapViewModel ViewModel { get; set; }

        public IndoorTest() : base()
        {
            ViewModel = new MapViewModel();
        }

        protected override async void OnCreate(Bundle bundle)
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
            _mySearchBox = new AutoCompleteTextView(this) { Hint = "Search rooms or people..." };
            layout.AddView(_mySearchBox);

            // Disable multi-line search
            _mySearchBox.SetMaxLines(1);

            //Auto Complete Drop Down List View
            _searchListView = new ListView(this);
            layout.AddView(_searchListView);

            // Progress bar
            _myProgressBar = new ProgressBar(this) { Indeterminate = true, Visibility = ViewStates.Gone };
            layout.AddView(_myProgressBar);

            // Floors List View
            _floorsTableView = new ListView(this);
            _floorsTableView.LayoutParameters = new ViewGroup.LayoutParams(100, 200);
            _floorsTableView.TextAlignment = TextAlignment.Center;
            _floorsTableView.Visibility = ViewStates.Gone;

            layout.AddView(_floorsTableView);

            // Add a map view to the layout
            _myMapView = new MapView(this);
            layout.AddView(_myMapView);


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

            // Handle the user moving the map 
            _myMapView.NavigationCompleted += MapView_NavigationCompleted;

            // Add a graphics overlay to hold the pins and route graphics
            var pinsGraphicOverlay = new GraphicsOverlay
            {
                Id = "PinsGraphicsOverlay"
            };
            _myMapView.GraphicsOverlays.Add(pinsGraphicOverlay);

            var labelsGraphicOverlay = new GraphicsOverlay
            {
                Id = "LabelsGraphicsOverlay"
            };
            _myMapView.GraphicsOverlays.Add(labelsGraphicOverlay);
        }

        private async void MapView_NavigationCompleted(object sender, EventArgs e)
        {
            // Display floors and level if user is zoomed in
            // If user is zoomed out, only show the base layer
            if(_myMapView.MapScale <= AppSettings.CurrentSettings.RoomsLayerMinimumZoomLevel)
            {
                await DisplayFloorLevelsAsync();
            }
            else
            {
                // _floorsTableView.Visibility = ViewStates.Visible;
                ViewModel.SetFloorVisibility(false);
            }
        }

        /// <summary>
        /// Display the floor levels based on which building the current viewpoint is over
        /// </summary>
        /// <returns>The floor levels.</returns>
        private async Task DisplayFloorLevelsAsync()
        {
            if(_myMapView.Map.LoadStatus == LoadStatus.Loaded)
            {
                try
                {
                    var floorsViewModel = new FloorSelectorViewModel();
                    var tableItems = await floorsViewModel.GetFloorsInVisibleAreaAsync(_myMapView);

                    // Only show the floors tableview if the buildings in view have more than one floor
                    if(tableItems.Count() > 1)
                    {

                        var floors = new List<string>(tableItems.ToList());
                        var adapter = new ArrayAdapter(this, Resource.Layout.SimpleSpinnerItem, floors);
                        _floorsTableView.Adapter = adapter;

                        // Show the table view and populate it
                        _floorsTableView.Visibility = ViewStates.Visible;
                        _floorsTableView.ItemClick += FloorListView_TableRowSelected;

                        if(string.IsNullOrEmpty(ViewModel.SelectedFloorLevel) || !tableItems.Contains(ViewModel.SelectedFloorLevel))
                        {
                            ViewModel.SelectedFloorLevel = MapViewModel.DefaultFloorLevel;
                        }
                        // Turn layers on. If there is no floor selected, first floor will be displayed by default
                        ViewModel.SetFloorVisibility(true);
                    }
                    else if(tableItems.Count() == 1)
                    {
                        DismissFloorsTableView();
                        ViewModel.SelectedFloorLevel = tableItems[0];
                        // Turn layers on. If there is no floor selected, first floor will be displayed by default
                        ViewModel.SetFloorVisibility(true);
                    }
                }
                catch
                {
                    DismissFloorsTableView();
                }
            }
        }

        private void FloorListView_TableRowSelected(object sender, ItemClickEventArgs e)
        {
            var selectedItem = (string)_floorsTableView.GetItemAtPosition(e.Position);
            ViewModel.SelectedFloorLevel = selectedItem;
            ViewModel.SetFloorVisibility(true);
        }

        /// <summary>
        /// When called, clears all values and hide table view
        /// </summary>
        private void DismissFloorsTableView()
        {
            _floorsTableView.Visibility = ViewStates.Gone;
        }

        private async Task GetSuggestionsFromLocatorAsync()
        {
            var suggestions = await LocationViewModel.Instance.GetLocationSuggestionsAsync(_mySearchBox.Text);
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
                    _searchListView.ItemClick += SearchItemSelected;
                }
                else
                {
                    // Create an array adapter to provide autocomplete suggestions
                    var adapter = new ArrayAdapter(this, Resource.Layout.SimpleSpinnerItem, new List<string>());
                    _searchListView.Adapter = adapter;
                    _searchListView.ItemClick += SearchItemSelected;
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

        private async void SearchItemSelected(object sender, ItemClickEventArgs e)
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

         
            var geocodeResult = await LocationViewModel.Instance.GetSearchedLocationAsync(searchText);
            var floorLevel = await LocationViewModel.Instance.GetFloorLevelFromQueryAsync(searchText);
           
            ViewModel.SelectedFloorLevel = floorLevel;
        
            if (geocodeResult != null)
            {

                var pinGraphic = await GraphicForPoint(geocodeResult.DisplayLocation);
                // Add pin to map
                var graphicsOverlay = _myMapView.GraphicsOverlays["PinsGraphicsOverlay"];
                graphicsOverlay.Graphics.Clear();
                graphicsOverlay.Graphics.Add(pinGraphic);
                graphicsOverlay.IsVisible = true;
                ViewModel.Viewpoint = new Viewpoint(geocodeResult.DisplayLocation, 150);

                // Get the feature to populate the Contact Card
                var roomFeature = await LocationViewModel.Instance.GetRoomFeatureAsync(searchText);
                if(roomFeature != null)
                {
                    // Get room attribute from the settings. First attribute should be set as the searcheable one
                    var roomAttribute = AppSettings.CurrentSettings.ContactCardDisplayFields[0];
                    var roomNumber = roomFeature.Attributes[roomAttribute];
                    var roomNumberLabel = roomNumber ?? string.Empty;
                    var employeeNameLabel = string.Empty;
                    if(AppSettings.CurrentSettings.ContactCardDisplayFields.Count > 1)
                    {
                        var employeeNameAttribute = AppSettings.CurrentSettings.ContactCardDisplayFields[1];
                        var employeeName = roomFeature.Attributes[employeeNameAttribute];
                        employeeNameLabel = employeeName as string ?? string.Empty;
                    }
                }
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

            ViewModel.PropertyChanged += ViewModelPropertyChanged;
            await ViewModel.InitializeAndroidMapViewAsync();
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
