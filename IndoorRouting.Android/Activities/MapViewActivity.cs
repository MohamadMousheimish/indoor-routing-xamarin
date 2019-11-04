using Android;
using Android.App;
using Android.Content;
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
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using IndoorRouting.Android.Activities;
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

        #region UI Elements
        
        private MapView _myMapView;
        private AutoCompleteTextView _mySearchBox;
        private ListView _searchListView;
        private ListView _floorsTableView;
        private LinearLayout _firstInformationLayout;
        private LinearLayout _secondInformationLayout;
        private TextView _firstTabMainTextView;
        private TextView _firstTabSecondaryTextView;
        private TextView _secondTabMainTextView;
        private TextView _secondTabSecondaryTextView;
        private ImageView _settingImageView;

        #endregion

        #region Activity Properties

        /// <summary>
        /// Gets or sets the map view model containing the common logic for dealing with the map
        /// </summary>
        private MapViewModel ViewModel { get; set; }

        /// <summary>
        /// Gets or sets from location feature
        /// </summary>
        public Feature FromLocationFeature { get; set; }

        /// <summary>
        /// Gets or sets to location feature
        /// </summary>
        public Feature ToLocationFeature { get; set; }

        /// <summary>
        /// The route.
        /// </summary>
        private RouteResult route;

        /// <summary>
        /// Gets or sets the route
        /// </summary>
        public RouteResult Route { get{ return route; } set { route = value; OnRouteChangedAsync(); } }

        public string FromLocationString { get; set; }

        public string ToLocationString { get; set; }

        #endregion

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
            var topBarLayout = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            var informationLayout = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            var firstInformationLayout = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            var secondInformationLayout = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            var floorLayoutParams = new RelativeLayout.LayoutParams(250, ViewGroup.LayoutParams.WrapContent);
            var infoLayoutParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            var searchViewParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            var mapParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);

            _firstTabMainTextView = new TextView(this);
            _firstTabSecondaryTextView = new TextView(this);
            _secondTabMainTextView = new TextView(this);
            _secondTabSecondaryTextView= new TextView(this);

            _firstInformationLayout = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
            _firstInformationLayout.AddView(_firstTabMainTextView, infoLayoutParams);
            _firstInformationLayout.AddView(_firstTabSecondaryTextView, infoLayoutParams);

            _secondInformationLayout = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
            _secondInformationLayout.AddView(_secondTabMainTextView, infoLayoutParams);
            _secondInformationLayout.AddView(_secondTabSecondaryTextView, infoLayoutParams);

            _settingImageView = new ImageView(this);
            _settingImageView.SetImageResource(Resource.Drawable.IcMenuManage);

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
            _firstInformationLayout.SetBackgroundColor(Color.FloralWhite);
            _firstInformationLayout.SetPadding(0, 0, 50, 0);
            _secondInformationLayout.SetBackgroundColor(Color.FloralWhite);
            _secondInformationLayout.SetPadding(50, 0, 0, 0);
            _mySearchBox = new AutoCompleteTextView(this) { Hint = "Search rooms or people..." };

            // Disable multi-line search
            _mySearchBox.SetMaxLines(1);

            // Disable the buttons and search bar until geocoder is ready
            _mySearchBox.Enabled = false;

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
            var routeGraphicOverlay = new GraphicsOverlay
            {
                Id = "RouteGraphicsOverlay"
            };
            
            _myMapView.GraphicsOverlays.Add(pinsGraphicOverlay);
            _myMapView.GraphicsOverlays.Add(routeGraphicOverlay);

            _settingImageView.SetPadding(0, DpToPx(10), 0, 0);
            _settingImageView.Clickable = true;
            _settingImageView.Click += SettingImage_Clicked;
            topBarLayout.AddView(_settingImageView, new ViewGroup.LayoutParams(DpToPx(40), DpToPx(40)));
            topBarLayout.AddView(_mySearchBox, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
            relativeLayout.AddView(_myMapView, mapParams);
            relativeLayout.AddView(floorsLayout, floorLayoutParams);
            firstInformationLayout.AddView(_firstInformationLayout);
            secondInformationLayout.AddView(_secondInformationLayout);
            informationLayout.SetPadding(75, 0, 0, 250);
            informationLayout.AddView(firstInformationLayout);
            informationLayout.AddView(secondInformationLayout);
            relativeLayout.AddView(informationLayout, infoLayoutParams);

            root.AddView(topBarLayout);
            root.AddView(_searchListView);
            root.AddView(relativeLayout);

            SetContentView(root);
        }

        private void SettingImage_Clicked(object sender, EventArgs e)
        {
            var settingIntent = new Intent(this, typeof(SettingsActivity));
            StartActivity(settingIntent);
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
                var pin = await GraphicForPoint(identifiedResult.Geometry.Extent.GetCenter(), "pin_blue.png");

                // Add pin to mapview
                var graphicsOverlay = _myMapView.GraphicsOverlays["PinsGraphicsOverlay"];
                if (AppSettings.CurrentSettings != null && AppSettings.CurrentSettings.IsRoutingEnabled)
                {
                    if (graphicsOverlay.Graphics.Any())
                    {
                        if(graphicsOverlay.Graphics.Count == 2)
                        {
                            graphicsOverlay.Graphics.Remove(graphicsOverlay.Graphics.Last());
                            var endPin = await GraphicForPoint(identifiedResult.Geometry.Extent.GetCenter(), "pin_red.png");
                            graphicsOverlay.Graphics.Add(endPin);
                        }
                        else
                        {
                            var startPin = await GraphicForPoint(identifiedResult.Geometry.Extent.GetCenter(), "pin_green.png");
                            var endPin = await GraphicForPoint(identifiedResult.Geometry.Extent.GetCenter(), "pin_red.png");
                            graphicsOverlay.Graphics.First().Symbol = startPin.Symbol;
                            graphicsOverlay.Graphics.Add(endPin);
                        }
                    }
                    else
                    {
                        var startPin = await GraphicForPoint(identifiedResult.Geometry.Extent.GetCenter(), "pin_green.png");
                        graphicsOverlay.Graphics.Add(startPin);
                    }
                }
                else
                {
                    graphicsOverlay.Graphics.Clear();
                    graphicsOverlay.Graphics.Add(pin);
                }

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
                    if(!string.IsNullOrEmpty(_firstTabMainTextView.Text) && !string.IsNullOrWhiteSpace(_secondTabMainTextView.Text))
                    {
                        try
                        {
                            FromLocationFeature = await LocationViewModel.Instance.GetRoomFeatureAsync(FromLocationString);
                            ToLocationFeature = await LocationViewModel.Instance.GetRoomFeatureAsync(ToLocationString);

                            var fromLocationPoint = FromLocationFeature.Geometry.Extent.GetCenter();
                            var toLocationPoint = ToLocationFeature.Geometry.Extent.GetCenter();

                            var route = await LocationViewModel.Instance.GetRequestedRouteAsync(fromLocationPoint, toLocationPoint);
                            Route = route;
                        }
                        catch
                        {
                            Route = null;
                        }
                    }
                }
            }
            catch
            {
                _myMapView.GraphicsOverlays["RouteGraphicsOverlay"].Graphics.Clear();
                _myMapView.GraphicsOverlays["PinsGraphicsOverlay"].Graphics.Clear();
                _firstInformationLayout.Visibility = ViewStates.Gone;
                _secondInformationLayout.Visibility = ViewStates.Gone;
                _firstTabMainTextView.Text = null;
                _secondTabMainTextView.Text = null;
                _firstTabSecondaryTextView.Text = null;
                _secondTabSecondaryTextView.Text = null;
            }
        }

        private void ShowInformationCard(string mainLabel, string secondaryLabel)
        {
            var graphicsOverlay = _myMapView.GraphicsOverlays["PinsGraphicsOverlay"];
            if(graphicsOverlay != null && graphicsOverlay.Graphics != null)
            {
                if(graphicsOverlay.Graphics.Count > 1)
                {
                    ToLocationString = mainLabel;
                    if (!_firstTabMainTextView.Text.Contains("From:"))
                    {
                        _firstTabMainTextView.Text = $"From: {_firstTabMainTextView.Text}";
                    }
                    _secondTabMainTextView.Text = $"To: {mainLabel}";
                    _secondTabSecondaryTextView.Text = secondaryLabel;
                    _secondInformationLayout.Visibility = ViewStates.Visible;
                }
                else
                {
                    FromLocationString = mainLabel;
                    if (AppSettings.CurrentSettings.IsRoutingEnabled)
                    {
                        _firstTabMainTextView.Text = $"From: {mainLabel}";
                    }
                    else
                    {
                        _firstTabMainTextView.Text = mainLabel;
                    }
                    _firstTabSecondaryTextView.Text = secondaryLabel;
                    _firstInformationLayout.Visibility = ViewStates.Visible;
                }
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
            if (AppSettings.CurrentSettings.IsRoutingEnabled)
            {
                _mySearchBox.Text = string.Empty;
                var selectedItem = (string)_floorsTableView.GetItemAtPosition(e.Position);
                var listViewElement = (ListView)sender;
                listViewElement.SetSelector(Resource.Color.HoloBlueLight);
                ViewModel.SelectedFloorLevel = selectedItem;
                ViewModel.SetFloorVisibility(true);
            }
            else
            {
                // Remove pins from map
                _myMapView.GraphicsOverlays["PinsGraphicsOverlay"].Graphics.Clear();
                _mySearchBox.Text = string.Empty;
                _firstInformationLayout.Visibility = ViewStates.Gone;

                var selectedItem = (string)_floorsTableView.GetItemAtPosition(e.Position);
                var listViewElement = (ListView)sender;
                listViewElement.SetSelector(Resource.Color.HoloBlueLight);
                ViewModel.SelectedFloorLevel = selectedItem;
                ViewModel.SetFloorVisibility(true);
            }
        }

        /// <summary>
        /// When called, clears all values and hide table view
        /// </summary>
        private void DismissFloorsTableView()
        {
            _floorsTableView.Visibility = ViewStates.Gone;
            _firstInformationLayout.Visibility = ViewStates.Gone;
            _secondInformationLayout.Visibility = ViewStates.Gone;
            _firstTabMainTextView.Text = null;
            _secondTabMainTextView.Text = null;
            _firstTabSecondaryTextView.Text = null;
            _secondTabSecondaryTextView.Text = null;
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
                _firstInformationLayout.Visibility = ViewStates.Gone;

            }
        }

        private async Task GetSearchedFeatureAsync(string searchText)
        {
            var geocodeResult = await LocationViewModel.Instance.GetSearchedLocationAsync(searchText);
            var floorLevel = await LocationViewModel.Instance.GetFloorLevelFromQueryAsync(searchText);
           
            ViewModel.SelectedFloorLevel = floorLevel;
        
            if (geocodeResult != null)
            {

                var pinGraphic = await GraphicForPoint(geocodeResult.DisplayLocation, "pin_blue.png");
                // Add pin to map
                var graphicsOverlay = _myMapView.GraphicsOverlays["PinsGraphicsOverlay"];
                graphicsOverlay.Graphics.Clear();
                graphicsOverlay.Graphics.Add(pinGraphic);
                graphicsOverlay.IsVisible = true;
                ViewModel.Viewpoint = new Viewpoint(geocodeResult.DisplayLocation, 150);
            }
        }

        private async Task<Graphic> GraphicForPoint(MapPoint point, string imageName)
        {
            // Get current assembly that contains the image.
            var currentAssembly = Assembly.GetExecutingAssembly();
            // var allRessources = Assembly.GetExecutingAssembly().GetManifestResourceNames();

            // Get image as a stream from the resources.
            // Picture is defined as EmbeddedResource and DoNotCopy.
            var resourceStream = currentAssembly.GetManifestResourceStream($"IndoorRouting.Android.Resources.drawable.{imageName}");

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

        private async Task<PictureMarkerSymbol> CreatePictureMarkerSymbol(string imageName)
        {

            // Get current assembly that contains the image.
            var currentAssembly = Assembly.GetExecutingAssembly();
            // Get image as a stream from the resources.
            // Picture is defined as EmbeddedResource and DoNotCopy.
            var resourceStream = currentAssembly.GetManifestResourceStream($"IndoorRouting.Android.Resources.drawable.{imageName}");
            if (resourceStream != null)
            {
                // Create new symbol using asynchronous factory method from stream.
                var pinSymbol = await PictureMarkerSymbol.CreateAsync(resourceStream);
                pinSymbol.Width = 35;
                pinSymbol.Height = 35;
                return pinSymbol;
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

        /// <summary>
        /// Fires when a new route is generated
        /// </summary>
        /// <returns></returns>
        private async Task OnRouteChangedAsync()
        {
            if (Route != null)
            {
                // Get the route from the results
                var newRoute = Route.Routes.FirstOrDefault();

                // Create a picture marker symbol for start pin
                var startMarker = await CreatePictureMarkerSymbol("circle_green.png");
                var endMarker = await CreatePictureMarkerSymbol("circle_red.png");

                if (newRoute != null)
                {
                    var walkTimeStringBuilder = new StringBuilder();

                    // Add walk time and distance label
                    if (newRoute.TotalTime.Hours > 0)
                    {
                        walkTimeStringBuilder.Append($"{newRoute.TotalTime.Hours} h {newRoute.TotalTime.Minutes} m");
                    }
                    else
                    {
                        walkTimeStringBuilder.Append($"{newRoute.TotalTime.Minutes + 1} m");
                    }

                    var tableSource = new List<Feature>() { FromLocationFeature, ToLocationFeature };

                    // Create point graphics
                    var startGraphic = new Graphic(newRoute.RouteGeometry.Parts.First().Points.First(), startMarker);
                    var endGraphic = new Graphic(newRoute.RouteGeometry.Parts.Last().Points.Last(), endMarker);

                    // Create a graphic to represent the route
                    var routeSymbol = new SimpleLineSymbol()
                    {
                        Width = 5,
                        Style = SimpleLineSymbolStyle.Solid,
                        Color = System.Drawing.Color.FromArgb(127, 18, 121, 193)
                    };

                    var routeGraphic = new Graphic(newRoute.RouteGeometry, routeSymbol);

                    // Add graphics to overlay
                    _myMapView.GraphicsOverlays["RouteGraphicsOverlay"].Graphics.Clear();
                    _myMapView.GraphicsOverlays["RouteGraphicsOverlay"].Graphics.Add(routeGraphic);
                    _myMapView.GraphicsOverlays["RouteGraphicsOverlay"].Graphics.Add(startGraphic);
                    _myMapView.GraphicsOverlays["RouteGraphicsOverlay"].Graphics.Add(endGraphic);

                    // Hide the pins graphic overlay
                    _myMapView.GraphicsOverlays["PinsGraphicsOverlay"].IsVisible = false;

                    try
                    {
                        await _myMapView.SetViewpointGeometryAsync(newRoute.RouteGeometry, 30);
                    }
                    catch
                    {

                    }
                }
                else
                {
                    _firstTabMainTextView.Text = "Routing Error";
                    _firstTabSecondaryTextView.Text = "Please retry route";
                    _secondInformationLayout.Visibility = ViewStates.Gone;
                    _secondTabMainTextView.Text = null;
                    _secondTabSecondaryTextView.Text = null;
                }
            }
            else
            {
                _firstTabMainTextView.Text = "Routing Error";
                _firstTabSecondaryTextView.Text = "Please retry route";
                _secondInformationLayout.Visibility = ViewStates.Gone;
                _secondTabMainTextView.Text = null;
                _secondTabSecondaryTextView.Text = null;
            }
        }

        /// <summary>
        /// Clears the route and hides route card.
        /// </summary>
        private void ClearRoute()
        {
            _myMapView.GraphicsOverlays["RouteGraphicsOverlay"].Graphics.Clear();
            _myMapView.GraphicsOverlays["PinsGraphicsOverlay"].IsVisible = true;
        }
    }
}
