using Android.App;
using Android.Views;
using Android.Widget;
using IndoorRouting.Models;
using Java.Lang;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorRouting.Android
{
    internal class CategoriesAdapter: BaseExpandableListAdapter
    {
        private readonly List<SearchableTreeNode> _items;
        private readonly Activity _context;

        public CategoriesAdapter(Activity context, List<SearchableTreeNode> items)
        {
            _items = items;
            _context = context;
        }

        public override Java.Lang.Object GetChild(int groupPosition, int childPosition)
        {
            return (Java.Lang.Object)_items[groupPosition].Items[childPosition];
        }

        public override long GetChildId(int groupPosition, int childPosition)
        {
            return _items[groupPosition].Items[childPosition].GetHashCode();
        }

        public override int GetChildrenCount(int groupPosition)
        {
            return _items[groupPosition].Items.Count;
        }

        public override Java.Lang.Object GetGroup(int groupPosition)
        {
            return (Java.Lang.Object)(object)_items[groupPosition];
        }

        public override long GetGroupId(int groupPosition)
        {
            return _items[groupPosition].GetHashCode();
        }

        public override View GetGroupView(int groupPosition, bool isExpanded, View convertView, ViewGroup parent)
        {
            var view = _context.LayoutInflater.Inflate(Resource.Layout.CategoriesLayout, parent, false);
            var name = view.FindViewById<TextView>(Resource.Id.groupNameTextView);
            name.Text = _items[groupPosition].Name;
            return view;
        }

        public override View GetChildView(int groupPosition, int childPosition, bool isLastChild, View convertView, ViewGroup parent)
        {
            var view = _context.LayoutInflater.Inflate(Resource.Layout.CategoriesLayout, parent, false);
            var name = view.FindViewById<TextView>(Resource.Id.sampleNameTextView);
            var sample = (SampleInfo)_items[groupPosition].Items[childPosition];
            name.Text = sample.SampleName;
            return view;
        }

        public override bool IsChildSelectable(int groupPosition, int childPosition)
        {
            if(_items[groupPosition]?.Items[childPosition] != null)
            {
                return true;
            }
            return false;
        }

        public override int GroupCount => _items.Count;

        public override bool HasStableIds => true;

    }
}
