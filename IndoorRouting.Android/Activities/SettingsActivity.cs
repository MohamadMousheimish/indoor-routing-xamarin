using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using static Android.Widget.CompoundButton;

namespace IndoorRouting.Android.Activities
{
    [Activity(Label = "Settings Activity")]
    public class SettingsActivity : Activity
    {
        //UI Elements
        private LinearLayout _mainLayout;
        private Switch _routingSwitch;
        private Toolbar _toolbar;
        private ImageButton _backButton;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            _toolbar = new Toolbar(this);
            _backButton = new ImageButton(this);
            _backButton.SetImageResource(Resource.Drawable.abc_ic_ab_back_material);
            _backButton.Click += BackButton_Clicked;
            _toolbar.AddView(_backButton, new ViewGroup.LayoutParams(DpToPx(40), DpToPx(40)));
            _mainLayout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            _routingSwitch = new Switch(this) { Text = "Enable Routing" };
            _routingSwitch.SetPadding(50, 50, 50, 0);
            _routingSwitch.CheckedChange += RoutingSwitch_Changed;
            _routingSwitch.Checked = AppSettings.CurrentSettings == null ? false : AppSettings.CurrentSettings.IsRoutingEnabled;
            _mainLayout.AddView(_toolbar);
            _mainLayout.AddView(_routingSwitch);
            
            SetContentView(_mainLayout);
            // Create your application here
        }

        private void BackButton_Clicked(object sender, EventArgs e)
        {
            Finish();
        }

        private void RoutingSwitch_Changed(object sender, CheckedChangeEventArgs e)
        {
            if(AppSettings.CurrentSettings != null)
            {
                AppSettings.CurrentSettings.IsRoutingEnabled = e.IsChecked;
            }
        }

        private int DpToPx(int dp)
        {
            return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, Resources.DisplayMetrics);
        }
    }
}