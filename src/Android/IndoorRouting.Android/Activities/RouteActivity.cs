
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Widget;
using Java.Interop;

namespace IndoorRouting
{
    [Activity(Label = "Get Directions")]
    public class RouteActivity : Activity
    {
        private string StartLocation;
        private string EndLocation;
        private AutoCompleteTextView _startLocationAutoCompleteView;
        private AutoCompleteTextView _endLocationAutoCompleteView;
        private ArrayAdapter<string> _startSuggestionsAdapter;
        private ArrayAdapter<string> _endSuggestionsAdapter;
        private LocationViewModel _locationViewModel;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Route);

            _startLocationAutoCompleteView = FindViewById<AutoCompleteTextView>(Resource.Id.StartLocationAutoCompleteTextView);
            _endLocationAutoCompleteView = FindViewById<AutoCompleteTextView>(Resource.Id.EndLocationAutoCompleteTextView);

            EndLocation = Intent.GetStringExtra(IntentNames.EndLocation);
            if (EndLocation != null)
            {
                _endLocationAutoCompleteView.Text = EndLocation;
            }

            _startSuggestionsAdapter = new ArrayAdapter<string>(this,
                Android.Resource.Layout.SimpleDropDownItem1Line, new string[] { });
            _startSuggestionsAdapter.SetNotifyOnChange(true);
            _startLocationAutoCompleteView.Adapter = _startSuggestionsAdapter;

            _endSuggestionsAdapter = new ArrayAdapter<string>(this,
                Android.Resource.Layout.SimpleDropDownItem1Line, new string[] { });
            _endSuggestionsAdapter.SetNotifyOnChange(true);
            _endLocationAutoCompleteView.Adapter = _endSuggestionsAdapter;

            // Set start location as the current location, if available
            // Set start location as home location if available
            if (AppSettings.CurrentSettings.IsLocationServicesEnabled)
            {
                StartLocation = "Current Location";
                _startLocationAutoCompleteView.Text = StartLocation;
            }
            else if (AppSettings.CurrentSettings.HomeLocation != MapViewModel.DefaultHomeLocationText)
            {
                StartLocation = AppSettings.CurrentSettings.HomeLocation;
                _startLocationAutoCompleteView.Text = StartLocation;
            }

            _startLocationAutoCompleteView.TextChanged += (s, e) =>
            {
                GetSuggestionsAndUpdateView(_startLocationAutoCompleteView.Text, _startSuggestionsAdapter);
            };
            _startLocationAutoCompleteView.ItemClick += (s, e) =>
            {
                StartLocation = _startLocationAutoCompleteView.Text = _startSuggestionsAdapter.GetItem(e.Position);
            };
            _endLocationAutoCompleteView.TextChanged += (s, e) =>
            {
                GetSuggestionsAndUpdateView(_endLocationAutoCompleteView.Text, _endSuggestionsAdapter);
            };
            _endLocationAutoCompleteView.ItemClick += (s, e) =>
            {
                EndLocation = _endLocationAutoCompleteView.Text = _endSuggestionsAdapter.GetItem(e.Position);
            };

            _locationViewModel = LocationViewModel.Instance;
        }

        private async void GetSuggestionsAndUpdateView(string inputText, ArrayAdapter<string> suggestionsAdapter)
        {
            var suggestions = !string.IsNullOrEmpty(inputText) ? 
                await _locationViewModel.GetLocationSuggestionsAsync(inputText) : null;
            var suggestionStrings = !string.IsNullOrEmpty(inputText) ?
                suggestions.Select(s => s.Label).ToList() : new List<string>();

            RunOnUiThread(() =>
            {
                // Apply the suggestions to the auto-complete textbox
                suggestionsAdapter.Clear();
                suggestionsAdapter.AddAll((System.Collections.ICollection)suggestionStrings);
            });
        }

        [Export("RouteButton_Click")]
        public async void RouteButton_Click(View view)
        {
            RouteDisplayInfo routeInfo = null;
            // Geocode the locations selected by the user
            try
            {
                var routeFromCurrentLocation = StartLocation == "Current Location";
                var fromLocationFeature = !routeFromCurrentLocation ?
                    await LocationViewModel.Instance.GetRoomFeatureAsync(StartLocation) : null;
                var toLocationFeature = await LocationViewModel.Instance.GetRoomFeatureAsync(EndLocation);

                var fromLocationPoint = fromLocationFeature?.Geometry.Extent.GetCenter()
                    ?? LocationViewModel.Instance.CurrentLocation;
                var toLocationPoint = toLocationFeature.Geometry.Extent.GetCenter();

                var route = await LocationViewModel.Instance.GetRequestedRouteAsync(fromLocationPoint, toLocationPoint);

                routeInfo = new RouteDisplayInfo()
                {
                    FromFeature = routeFromCurrentLocation ? null : fromLocationFeature,
                    ToFeature = toLocationFeature,
                    Route = route
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
            }

            var mapIntent = new Intent(this, typeof(MapActivity));
            mapIntent.PutExtra(IntentNames.RouteInfo, routeInfo);
            StartActivity(mapIntent);
        }
    }
}
