
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.Geometry;
using Java.Interop;
using Esri.ArcGISRuntime.Data;

namespace IndoorRouting
{
    [Activity(Label = "Indoor Routing")]
    public class MapActivity : Activity
    {
        MapView _mapView;
        MapViewModel _mapViewModel;
        LocationViewModel _locationViewModel;
        AutoCompleteTextView _searchTextView;
        ArrayAdapter<string> _suggestAdapter;
        TextView _mainTextView;
        TextView _secondaryTextView;
        CardView _contactCardView;
        ImageButton _routeButton;
        GraphicsOverlay _pinsGraphicsOverlay;
        GraphicsOverlay _routeGraphicsOverlay;
        PictureMarkerSymbol _roomMarker;
        PictureMarkerSymbol _startMarker;
        PictureMarkerSymbol _endMarker;
        SimpleLineSymbol _routeSymbol;
        CardView _routeCardView;
        TextView _startLocationTextView;
        TextView _startFloorTextView;
        TextView _endLocationTextView;
        TextView _endFloorTextView;
        TextView _walkTimeTextView;
        string _savedExtent;

        protected async override void OnCreate(Bundle savedInstanceState)
        {
            RequestWindowFeature(WindowFeatures.NoTitle);

            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Map);
        }

        protected async override void OnStart()
        {
            base.OnStart();

            await InitializeMembersAsync();

            if (AppSettings.CurrentSettings.IsLocationServicesEnabled == true)
            {
                _mapView.LocationDisplay.IsEnabled = true;

                // TODO: Set floor when available in the API (Update 2?)
            }
            else
            {
                _mapView.LocationDisplay.IsEnabled = false;
            }

            // Check whether route display data was received
            var routeDisplayInfo = IntentBroker.GetExtra(this, IntentNames.RouteInfo) as RouteDisplayInfo;
            if (routeDisplayInfo?.Route != null && routeDisplayInfo?.ToFeature != null)
            {
                // Got route data, so show it
                await ShowRouteAsync(routeDisplayInfo);
            }
        }

        private async Task InitializeMembersAsync()
        {
            _mapView = FindViewById<MapView>(Resource.Id.MainMapView);
            _searchTextView = FindViewById<AutoCompleteTextView>(Resource.Id.SearchTextView);
            _searchTextView.TextChanged += SearchTextView_TextChanged;
            _searchTextView.ItemClick += SearchTextView_ItemClick;

            _mapViewModel = new MapViewModel();
            _mapViewModel.PropertyChanged += ViewModelPropertyChanged;
            await _mapViewModel.InitializeAsync();

            _mainTextView = FindViewById<TextView>(Resource.Id.MainTextView);
            _secondaryTextView = FindViewById<TextView>(Resource.Id.SecondaryTextView);
            _contactCardView = FindViewById<CardView>(Resource.Id.ContactCardView);
            _routeButton = FindViewById<ImageButton>(Resource.Id.RouteButton);

            // Route result card
            _routeCardView = FindViewById<CardView>(Resource.Id.RouteCardView);
            _startLocationTextView = FindViewById<TextView>(Resource.Id.StartLocationTextView);
            _startFloorTextView = FindViewById<TextView>(Resource.Id.StartFloorTextView);
            _endLocationTextView = FindViewById<TextView>(Resource.Id.EndLocationTextView);
            _endFloorTextView = FindViewById<TextView>(Resource.Id.EndFloorTextView);
            _walkTimeTextView = FindViewById<TextView>(Resource.Id.WalkTimeTextView);

            _locationViewModel = LocationViewModel.Instance;

            // Remove mapview grid and set its background
            _mapView.BackgroundGrid.GridLineWidth = 0;
            _mapView.BackgroundGrid.Color = System.Drawing.Color.WhiteSmoke;

            // Add a graphics overlay to hold the pins and route graphics
            _pinsGraphicsOverlay = new GraphicsOverlay
            {
                Id = "PinsGraphicsOverlay"
            };
            _mapView.GraphicsOverlays.Add(_pinsGraphicsOverlay);

            _routeGraphicsOverlay = new GraphicsOverlay
            {
                Id = "RouteGraphicsOverlay"
            };
            _mapView.GraphicsOverlays.Add(_routeGraphicsOverlay);

            // Initialize point symbols
            _roomMarker = await CreateSymbolFromAssetAsync("iOS8_MapPin_End36.png", offsetX: null, offsetY: 0.65);
            _startMarker = await CreateSymbolFromAssetAsync("GreenDot36.png");
            _endMarker = await CreateSymbolFromAssetAsync("RedDot36.png");

            // Initialize route (line) symbol
            _routeSymbol = new SimpleLineSymbol
            {
                Width = 5,
                Style = SimpleLineSymbolStyle.Solid,
                Color = System.Drawing.Color.FromArgb(127, 18, 121, 193)
            };


            //// Handle the user moving the map 
            //this.MapView.NavigationCompleted += this.MapView_NavigationCompleted;

            //// Handle the user tapping on the map
            //this.MapView.GeoViewTapped += this.MapView_GeoViewTapped;

            //// Handle the user double tapping on the map
            //this.MapView.GeoViewDoubleTapped += this.MapView_GeoViewDoubleTapped;

            //// Handle the user holding tap on the map
            //this.MapView.GeoViewHolding += this.MapView_GeoViewHolding;

            //this.MapView.LocationDisplay.LocationChanged += this.MapView_LocationChanged;
        }

        private async Task<PictureMarkerSymbol> CreateSymbolFromAssetAsync(string assetName, double? offsetX = null, double? offsetY = null)
        {
            using (var symbolStream = Assets.Open(assetName))
            {
                using (var symbolMemoryStream = new MemoryStream())
                {
                    await symbolStream.CopyToAsync(symbolMemoryStream);
                    var symbolBytes = symbolMemoryStream.ToArray();
                    var symbol = new PictureMarkerSymbol(new RuntimeImage(symbolBytes));

                    // Set pin size to match image size
                    var options = new BitmapFactory.Options() { InJustDecodeBounds = true };
                    await BitmapFactory.DecodeByteArrayAsync(symbolBytes, 0, symbolBytes.Length, options);
                    symbol.Width = options.OutWidth;
                    symbol.Height = options.OutHeight;

                    // Set offset, if specified
                    if (offsetX.HasValue)
                    {
                        symbol.OffsetX = options.OutWidth * (double)offsetX;
                    }
                    if (offsetY.HasValue)
                    {
                        symbol.OffsetY = options.OutHeight * (double)offsetY;
                    }

                    return symbol;
                }
            }
        }

        private async Task ShowRouteAsync(RouteDisplayInfo routeDisplayInfo)
        {
            // get the route from the results
            var newRoute = routeDisplayInfo.Route.Routes.FirstOrDefault();

            if (newRoute != null)
            {
                StringBuilder walkTimeStringBuilder = new StringBuilder();

                // Add walk time and distance label
                if (newRoute.TotalTime.Hours > 0)
                {
                    walkTimeStringBuilder.Append(string.Format("{0} h {1} m", newRoute.TotalTime.Hours, newRoute.TotalTime.Minutes));
                }
                else
                {
                    walkTimeStringBuilder.Append(string.Format("{0} min", newRoute.TotalTime.Minutes + 1));
                }

                //var tableSource = new List<Feature>() { this.FromLocationFeature, this.ToLocationFeature };
                ShowRouteCard(routeDisplayInfo.FromFeature, routeDisplayInfo.ToFeature, walkTimeStringBuilder.ToString());

                // Create point graphics
                var startGraphic = new Graphic(newRoute.RouteGeometry.Parts.First().Points.First(), _startMarker);
                var endGraphic = new Graphic(newRoute.RouteGeometry.Parts.Last().Points.Last(), _endMarker);

                var routeGraphic = new Graphic(newRoute.RouteGeometry, _routeSymbol);

                // Add graphics to overlay
                _routeGraphicsOverlay.Graphics.Clear();
                _routeGraphicsOverlay.Graphics.Add(routeGraphic);
                _routeGraphicsOverlay.Graphics.Add(startGraphic);
                _routeGraphicsOverlay.Graphics.Add(endGraphic);

                // Hide the pins graphics overlay
                _pinsGraphicsOverlay.IsVisible = false;

                try
                {
                    await _mapView.SetViewpointGeometryAsync(newRoute.RouteGeometry, 30);
                }
                catch
                {
                    // If panning to the new route fails, just move on
                }
            }
            else
            {
                ShowContactCard("Routing Error", "Please retry route", true);
            }
        }

        private async void SearchTextView_TextChanged(object sender, Android.Text.TextChangedEventArgs e)
        {
            var suggestions = await _locationViewModel.GetLocationSuggestionsAsync(_searchTextView.Text);
            var suggestionStrings = !string.IsNullOrEmpty(_searchTextView.Text) ?
                    suggestions.Select(s => s.Label).ToList() : new List<string>();

            RunOnUiThread(() =>
            {
                // Apply the suggestions to the auto-complete textbox
                if (_suggestAdapter == null)
                {
                    _suggestAdapter = new ArrayAdapter<string>(
                        this, Android.Resource.Layout.SimpleDropDownItem1Line, suggestionStrings);
                    _suggestAdapter.SetNotifyOnChange(true);
                    _searchTextView.Adapter = _suggestAdapter;
                }
                else
                {
                    _suggestAdapter.Clear();
                    _suggestAdapter.AddAll((System.Collections.ICollection)suggestionStrings);
                    //_suggestAdapter.NotifyDataSetChanged();
                }
            });
        }

        private async void SearchTextView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            try
            {
                var suggestion = _suggestAdapter.GetItem(e.Position);
                _searchTextView.Text = suggestion;
                await GetSearchedFeatureAsync(suggestion);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
            }
        }


        /// <summary>
        /// Zooms to geocode result of the searched feature
        /// </summary>
        /// <param name="searchText">Search text entered by user.</param>
        /// <returns>The searched feature</returns>
        private async Task GetSearchedFeatureAsync(string searchText)
        {
            var geocodeResult = await LocationViewModel.Instance.GetSearchedLocationAsync(searchText);
            this._mapViewModel.SelectedFloorLevel = await LocationViewModel.Instance.GetFloorLevelFromQueryAsync(searchText);

            if (geocodeResult != null)
            {
                // Create graphic
                var mapPinGraphic = new Graphic(geocodeResult.DisplayLocation, _roomMarker);

                // Add pin to map
                var graphicsOverlay = this._mapView.GraphicsOverlays["PinsGraphicsOverlay"];
                _pinsGraphicsOverlay.Graphics.Clear();
                _pinsGraphicsOverlay.Graphics.Add(mapPinGraphic);
                _pinsGraphicsOverlay.IsVisible = true;

                this._mapViewModel.Viewpoint = new Viewpoint(geocodeResult.DisplayLocation, 150);

                // Get the feature to populate the Contact Card
                var roomFeature = await LocationViewModel.Instance.GetRoomFeatureAsync(searchText);

                if (roomFeature != null)
                {
                    // Get room attribute from the settings. First attribute should be set as the searcheable one
                    var roomAttribute = AppSettings.CurrentSettings.ContactCardDisplayFields[0];
                    var roomNumber = roomFeature.Attributes[roomAttribute];
                    var roomNumberLabel = roomNumber ?? string.Empty;

                    var employeeNameLabel = string.Empty;
                    if (AppSettings.CurrentSettings.ContactCardDisplayFields.Count > 1)
                    {
                        var employeeNameAttribute = AppSettings.CurrentSettings.ContactCardDisplayFields[1];
                        var employeeName = roomFeature.Attributes[employeeNameAttribute];
                        employeeNameLabel = employeeName as string ?? string.Empty;
                    }

                    ShowContactCard(roomNumberLabel.ToString(), employeeNameLabel.ToString(), false);
                }
            }
            else
            {
                ShowContactCard(searchText, "Location not found", true);
            }
        }

        private void ShowContactCard(string mainLabel, string secondaryLabel, bool isRoute)
        {
            _mainTextView.Text = mainLabel;
            _secondaryTextView.Text = secondaryLabel;
            _routeButton.Visibility = isRoute ? ViewStates.Invisible : ViewStates.Visible;

            _contactCardView.Animate().Alpha(1).SetDuration(200);

            _mapView.IsAttributionTextVisible = false;

            var cardXY = new int[2];
            _contactCardView.GetLocationOnScreen(cardXY);
            var size = new Point();
            WindowManager.DefaultDisplay.GetSize(size);
            var bottomPadding = size.Y - cardXY[1];
            _mapView.SetViewInsets(0, 0, 0, bottomPadding);
        }

        private void HideGeocodeResult()
        {
            _pinsGraphicsOverlay.Graphics.Clear();
            _contactCardView.Animate().Alpha(0).SetDuration(200);

            _mapView.IsAttributionTextVisible = true;

            _mapView.SetViewInsets(0, 0, 0, 0);
        }

        private void ShowRouteCard(Feature fromFeature, Feature toFeature, string walkTime)
        {
            HideGeocodeResult();

            var locationField = AppSettings.CurrentSettings.LocatorFields[0];

            // Retrieve and display the location name of the start and end points
            _startLocationTextView.Text = (string)fromFeature.Attributes[locationField];
            _endLocationTextView.Text = (string)toFeature.Attributes[locationField];

            // Get the floor of the start and end points and format it for display
            var floorField = AppSettings.CurrentSettings.RoomsLayerFloorColumnName;
            var fromFloor = fromFeature.Attributes[floorField].ToString();
            var fromFloorSuffix = GetOrdinalSuffix(fromFloor);
            var toFloor = fromFeature.Attributes[floorField].ToString();
            var toFloorSuffix = GetOrdinalSuffix(toFloor);

            _startFloorTextView.Text = $"{fromFloor}{fromFloorSuffix} Floor";
            _endFloorTextView.Text = $"{toFloor}{toFloorSuffix} Floor";

            // Display the walk time
            _walkTimeTextView.Text = walkTime;

            _routeCardView.Animate().Alpha(1).SetDuration(200);

            _mapView.IsAttributionTextVisible = false;

            var cardXY = new int[2];
            _routeCardView.GetLocationOnScreen(cardXY);
            var size = new Point();
            WindowManager.DefaultDisplay.GetSize(size);
            var bottomPadding = size.Y - cardXY[1];
            _mapView.SetViewInsets(0, 0, 0, bottomPadding);
        }


        private void HideRouteResults()
        {
            _routeGraphicsOverlay.Graphics.Clear();
            _routeCardView.Animate().Alpha(0).SetDuration(200);

            _mapView.IsAttributionTextVisible = true;

            _mapView.SetViewInsets(0, 0, 0, 0);
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
                case nameof(MapViewModel.Map):
                    if (this._mapViewModel.Map != null)
                    {
                        // Add the map to the MapView to be displayedd
                        this._mapView.Map = this._mapViewModel.Map;
                    }

                    break;
                case nameof(MapViewModel.Viewpoint):
                    if (this._mapViewModel.Viewpoint != null)
                    {
                        await this._mapView.SetViewpointAsync(this._mapViewModel.Viewpoint);
                    }

                    break;
            }
        }

        [Export("SettingsButton_Click")]
        public void SettingsButton_Click(View view)
        {
            StartActivity(typeof(SettingsActivity));
        }

        [Export("RouteButton_Click")]
        public void RouteButton_Click(View view)
        {
            var routeIntent = new Intent(this, typeof(RouteActivity));
            routeIntent.PutExtra(IntentNames.EndLocation, _mainTextView.Text);
            StartActivity(routeIntent);
        }


        private string GetOrdinalSuffix(string number)
        {
            if (number.Length == 0)
            {
                return string.Empty;
            }

            var lastChar = number[number.Length - 1];
            var lastDigit = (int)Char.GetNumericValue(lastChar);
            if (lastDigit > 9) // Char was not a number
            {
                return string.Empty;
            }
            else if (lastDigit > 3 || lastDigit == 0)
            {
                return "th";
            }
            else if (lastDigit == 3)
            {
                return "rd";
            }
            else if (lastDigit == 2)
            {
                return "nd";
            }
            else if (lastDigit == 1)
            {
                return "st";
            }

            return string.Empty;
        }
    }
}
