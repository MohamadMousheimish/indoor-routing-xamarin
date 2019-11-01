using Android;
using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Graphics.Drawables.Shapes;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Text;
using Android.Util;
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

namespace IndoorRouting.MapViewActivity
{
    [Activity(ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    [Sample("Open Mobile Map (Map Package)",
            "Map",
            "This sample demonstrate how to open a map from a mobile map package",
            "The map package will be downloaded from an ArcGIS online portal automatically")]
    [OfflineData("52346d5fc4c348589f976b6a279ec3e6")]
    public partial class MapViewActivity : Activity
    {

        // Hold references to the UI Controls
        private MapView _myMapView;
        private AutoCompleteTextView _mySearchBox;
        private ListView _searchListView;
        private ListView _floorsTableView;
        private LinearLayout _informationLayout;
        private TextView _mainTextView;
        private TextView _secondaryTextView;

        /// <summary>
        /// Gets or sets the map view model containing the common logic for dealing with the map
        /// </summary>
        private MapViewModel ViewModel { get; set; }

        public MapViewActivity() : base()
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

        private async void Initialize()
        {
            // When the application has finished loading, bring in the settings
            var settingsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            AppSettings.CurrentSettings = await AppSettings.CreateAsync(System.IO.Path.Combine(settingsPath, "AppSettings.xml")).ConfigureAwait(false);

            ViewModel.PropertyChanged += ViewModelPropertyChanged;
            await ViewModel.InitializeAndroidMapViewAsync();
            _mySearchBox.Enabled = true;
        }

        private void CreateLayout()
        {
            var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var relativeLayout = new RelativeLayout(this);
            var floorsLayout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var informationParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            var floorLayoutParams = new RelativeLayout.LayoutParams(250, ViewGroup.LayoutParams.WrapContent);
            var infoLayoutParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            var searchBoxParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            var searchViewParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            var mapParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);


            _mainTextView = new TextView(this);
            _secondaryTextView = new TextView(this);

            _informationLayout = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
            _informationLayout.AddView(_mainTextView, informationParams);
            _informationLayout.AddView(_secondaryTextView, informationParams);


            _floorsTableView = new ListView(this)
            {
                TextAlignment = TextAlignment.Center,
                Visibility = ViewStates.Gone
            };
            var shape = new ShapeDrawable(new RectShape());
            shape.Paint.Color = Color.Blue;
            shape.Paint.StrokeWidth = 1;
            shape.Paint.SetStyle(Paint.Style.Stroke);
            _floorsTableView.SetBackgroundDrawable(shape);
            _floorsTableView.SetBackgroundColor(Color.FloralWhite);
            floorLayoutParams.AddRule(LayoutRules.CenterInParent);
            floorLayoutParams.AddRule(LayoutRules.AlignParentLeft);
            floorsLayout.AddView(_floorsTableView);
            floorsLayout.SetPadding(75, 0, 0, 0);

            infoLayoutParams.AddRule(LayoutRules.AlignParentBottom);
            _informationLayout.SetBackgroundColor(Color.LightGray);

            _mySearchBox = new AutoCompleteTextView(this) { Hint = "Search rooms or people..." };

            // Disable multi-line search
            _mySearchBox.SetMaxLines(1);

            // Disable the buttons and search bar until geocoder is ready
            _mySearchBox.Enabled = false;
            searchBoxParams.AddRule(LayoutRules.AlignParentTop);

            //Auto Complete Drop Down List View
            _searchListView = new ListView(this);
            searchViewParams.AddRule(LayoutRules.AlignParentBottom);

            // Hook up the UI event handlers for suggestion & search
            _mySearchBox.TextChanged += async (sender, e) =>
            {
                // Call to populate autosuggestions
                await GetSuggestionsFromLocatorAsync();
            };

            _myMapView = new MapView(this);

            // Handle the user moving the map 
            _myMapView.NavigationCompleted += MapView_NavigationCompleted;

            // Handle the user tapping on the map
            _myMapView.GeoViewTapped += MapView_GeoViewTapped;

            // Add a graphics overlay to hold the pins and route graphics
            var pinsGraphicOverlay = new GraphicsOverlay
            {
                Id = "PinsGraphicsOverlay"
            };
            _myMapView.GraphicsOverlays.Add(pinsGraphicOverlay);

            relativeLayout.AddView(_myMapView, mapParams);
            relativeLayout.AddView(floorsLayout, floorLayoutParams);
            relativeLayout.AddView(_informationLayout, infoLayoutParams);

            root.AddView(_mySearchBox);
            root.AddView(_searchListView);
            root.AddView(relativeLayout);

            SetContentView(root);
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
                _floorsTableView.Visibility = ViewStates.Gone;
                ViewModel.SetFloorVisibility(false);
            }
        }

        /// <summary>
        /// When map is tapped, clear the map of selection, close keyboard and bottom sheet
        /// </summary>
        /// <param name="sender">Sender element</param>
        /// <param name="e">Event Args</param>
        private async void MapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            try
            {
                // Wait for double tap to fire
                await Task.Delay(500);

                var tapScreenPoint = e.Position;
                var layer = _myMapView.Map.OperationalLayers[AppSettings.CurrentSettings.RoomsLayerIndex];
                var pixelTolerance = 10;
                var returnPopusOnly = false;
                var maxResults = 1;

                //Identify a layer using MapView, passing in the layer, the tap point, tolerance, types to return and max result
                var idResult = await _myMapView.IdentifyLayerAsync(layer, tapScreenPoint, pixelTolerance, returnPopusOnly, maxResults);
                var identifiedResult = idResult.GeoElements.First();
                var pinGraphic = await GraphicForPoint(identifiedResult.Geometry.Extent.GetCenter());

                // Add pin to mapview
                var graphicsOverlay = _myMapView.GraphicsOverlays["PinsGraphicsOverlay"];
                graphicsOverlay.Graphics.Clear();
                graphicsOverlay.Graphics.Add(pinGraphic);

                // Get room attribute from the settings. First attribute should be set as the searchable one
                var roomAttribute = AppSettings.CurrentSettings.ContactCardDisplayFields[0];
                var roomNumber = identifiedResult.Attributes[roomAttribute];
                if(roomNumber != null)
                {
                    var employeeNameLabel = string.Empty;
                    if(AppSettings.CurrentSettings.ContactCardDisplayFields.Count > 1)
                    {
                        var employeeNameAttribute = AppSettings.CurrentSettings.ContactCardDisplayFields[1];
                        var employeeName = identifiedResult.Attributes[employeeNameAttribute];
                        employeeNameLabel = employeeName as string ?? string.Empty;
                    }

                    ShowInformationCard(roomNumber.ToString(), employeeNameLabel.ToString());
                }

            }
            catch
            {
                _myMapView.GraphicsOverlays["PinsGraphicsOverlay"].Graphics.Clear();
                _informationLayout.Visibility = ViewStates.Gone;
            }
        }

        private void ShowInformationCard(string mainLabel, string secondaryLabel)
        {
            _mainTextView.Text = mainLabel;
            _secondaryTextView.Text = secondaryLabel;
            _informationLayout.Visibility = ViewStates.Visible;
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

                        // Show the table view and populate it
                        var floors = new List<string>(tableItems.ToList());
                        var adapter = new ArrayAdapter(this, Resource.Layout.SimpleSpinnerItem, floors);
                        _floorsTableView.Adapter = adapter;
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
                    else
                    {
                        DismissFloorsTableView();
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
            // Remove pins from map
            _myMapView.GraphicsOverlays["PinsGraphicsOverlay"].Graphics.Clear();
            _mySearchBox.Text = string.Empty;
            _informationLayout.Visibility = ViewStates.Gone;

            var selectedItem = (string)_floorsTableView.GetItemAtPosition(e.Position);
            var listViewElement = (ListView)sender;
            listViewElement.SetSelector(Resource.Color.HoloBlueLight); 
            ViewModel.SelectedFloorLevel = selectedItem;
            ViewModel.SetFloorVisibility(true);
        }

        /// <summary>
        /// When called, clears all values and hide table view
        /// </summary>
        private void DismissFloorsTableView()
        {
            _floorsTableView.Visibility = ViewStates.Gone;
            _informationLayout.Visibility = ViewStates.Gone;
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
                _informationLayout.Visibility = ViewStates.Gone;

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

        private int DpToPx(int dp)
        {
            return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, Resources.DisplayMetrics);
        }
    }
}
