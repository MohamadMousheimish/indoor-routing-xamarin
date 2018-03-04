using System;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;

namespace IndoorRouting
{
    internal class RouteDisplayInfo
    {
        public RouteDisplayInfo() { }

        public MapPoint FromPoint { get; set; }

        public MapPoint ToPoint { get; set; }

        public RouteResult Route { get; set; }
    }
}
