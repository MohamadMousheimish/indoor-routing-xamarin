﻿using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using System.Collections.Generic;
using IndoorRouting.Models;
using System;
using IndoorRouting.Managers;
using System.Linq;
using Android.Content;
using Esri.ArcGISRuntime.Security;

namespace IndoorRouting.Android
{
    [Activity(Label = "@string/app_name", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private List<SearchableTreeNode> _sampleCategories;
        private List<SearchableTreeNode> _filteredSampleCategories;
        private ExpandableListView _categoriesListView;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.CategoriesList);

            try
            {
                // Initialize the SampleManager and create the Sample Categories
                SampleManager.Current.Initialize();
                _sampleCategories = SampleManager.Current.FullTree.Items.OfType<SearchableTreeNode>().ToList();
                _filteredSampleCategories = _sampleCategories;

                // Set up the custom ArrayAdapter for displaying the Categories
                var categoriesAdapter = new CategoriesAdapter(this, _sampleCategories);
                _categoriesListView = FindViewById<ExpandableListView>(Resource.Id.categoriesListView);
                _categoriesListView.SetAdapter(categoriesAdapter);
                _categoriesListView.ChildClick += CategoriesListViewOnChildClick;
                _categoriesListView.DividerHeight = 2;
                _categoriesListView.SetGroupIndicator(null);

                // Set up the search filtering
                var searchBox = FindViewById<SearchView>(Resource.Id.categorySearchView);
                searchBox.QueryTextChange += SearchBoxOnQueryTextChange;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void SearchBoxOnQueryTextChange(object sender, SearchView.QueryTextChangeEventArgs queryTextChangeEventArgs)
        {
            var stnResult = SampleManager.Current.FullTree.Search(sample => SampleManager.Current.SampleSearchFunc(sample, queryTextChangeEventArgs.NewText));
            if(stnResult != null)
            {
                _filteredSampleCategories = stnResult.Items.OfType<SearchableTreeNode>().ToList();
            }
            else
            {
                _filteredSampleCategories = new List<SearchableTreeNode>();
            }
            _categoriesListView.SetAdapter(new CategoriesAdapter(this, _filteredSampleCategories));

            // Expand all entries; makes it easier to see search results
            for (int index = 0; index < _filteredSampleCategories.Count; index++)
            {
                _categoriesListView.ExpandGroup(index);
            }
        }

        private async void CategoriesListViewOnChildClick(object sender, ExpandableListView.ChildClickEventArgs childClickEventArgs)
        {
            var sampleName = string.Empty;
            try
            {
                // Call a function to clear existing credentials
                ClearCredentials();

                //Get the clicked item
                var item = (SampleInfo)_filteredSampleCategories[childClickEventArgs.GroupPosition].Items[childClickEventArgs.ChildPosition];

                // Download any offline data before showing the sample
                if(item.OfflineDataItems != null)
                {
                    // Show the waiting dialog
                    var builder = new AlertDialog.Builder(this);
                    builder.SetView(new ProgressBar(this)
                    {
                        Indeterminate = true
                    });
                    builder.SetMessage("Downloading data");
                    var dialog = builder.Create();
                    dialog.Show();

                    // Begin downloading data
                    await DataManager.EnsureSampleDataPresent(item);

                    // Hide the progess dialog
                    dialog.Dismiss();
                }

                // Each Sample is an activity, so locate it and launch it via an Intent
                var newActivity = new Intent(this, item.SampleType);

                // Start the activity
                StartActivity(newActivity);

            }
            catch(Exception e)
            {
                AlertDialog.Builder bldr = new AlertDialog.Builder(this);
                var dialog = bldr.Create();
                dialog.SetTitle($"Unable to load {sampleName}");
                dialog.SetMessage(e.Message);
                dialog.Show();
            }
        }

        private static void ClearCredentials()
        {
            // Clear Credentials (if any) from previous sample runs
            foreach (Credential cred in AuthenticationManager.Current.Credentials)
            {
                AuthenticationManager.Current.RemoveCredential(cred);
            }
        }

    }
}