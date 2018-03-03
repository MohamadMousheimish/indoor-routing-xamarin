
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
using Java.Interop;

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

        protected async override void OnCreate(Bundle savedInstanceState)
        {
            RequestWindowFeature(WindowFeatures.NoTitle);

            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Map);

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

            _locationViewModel = LocationViewModel.Instance;

            // Remove mapview grid and set its background
            _mapView.BackgroundGrid.GridLineWidth = 0;
            _mapView.BackgroundGrid.Color = System.Drawing.Color.WhiteSmoke;

            // Add a graphics overlay to hold the pins and route graphics
            var pinsGraphicOverlay = new GraphicsOverlay();
            pinsGraphicOverlay.Id = "PinsGraphicsOverlay";
            _mapView.GraphicsOverlays.Add(pinsGraphicOverlay);

            var labelsGraphicOverlay = new GraphicsOverlay();
            labelsGraphicOverlay.Id = "LabelsGraphicsOverlay";
            _mapView.GraphicsOverlays.Add(labelsGraphicOverlay);

            var routeGraphicsOverlay = new GraphicsOverlay();
            routeGraphicsOverlay.Id = "RouteGraphicsOverlay";
            _mapView.GraphicsOverlays.Add(routeGraphicsOverlay);

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

        protected override void OnStart()
        {
            base.OnStart();

            if (AppSettings.CurrentSettings.IsLocationServicesEnabled == true)
            {
                _mapView.LocationDisplay.IsEnabled = true;

                // TODO: Set floor when available in the API (Update 2?)
            }
            else
            {
                _mapView.LocationDisplay.IsEnabled = false;
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
                // create a picture marker symbol
                var mapPinStream = Assets.Open("iOS8_MapPin_End36.png");
                var mapPinMemoryStream = new MemoryStream();
                await mapPinStream.CopyToAsync(mapPinMemoryStream);
                var mapPinBytes = mapPinMemoryStream.ToArray();
                var roomMarker = new PictureMarkerSymbol(new RuntimeImage(mapPinBytes));

                // Get pin size and use it to set offset
                var options = new BitmapFactory.Options() { InJustDecodeBounds = true };
                await BitmapFactory.DecodeByteArrayAsync(mapPinBytes, 0, mapPinBytes.Length, options);
                roomMarker.Width = options.OutWidth;
                roomMarker.Height = options.OutHeight;
                roomMarker.OffsetY = options.OutHeight * 0.65;

                // Create graphic
                var mapPinGraphic = new Graphic(geocodeResult.DisplayLocation, roomMarker);

                // Add pin to map
                var graphicsOverlay = this._mapView.GraphicsOverlays["PinsGraphicsOverlay"];
                graphicsOverlay.Graphics.Clear();
                graphicsOverlay.Graphics.Add(mapPinGraphic);
                graphicsOverlay.IsVisible = true;

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
        }

        private void HideContactCard()
        {
            _contactCardView.Animate().Alpha(0).SetDuration(200);

            _mapView.IsAttributionTextVisible = true;
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

        [Export("RouteButton_Click")]
        public void RouteButton_Click(View view)
        {
            
        }
    }
}
