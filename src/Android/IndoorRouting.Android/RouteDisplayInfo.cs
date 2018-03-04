using System;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;

namespace IndoorRouting
{
    internal class RouteDisplayInfo
    {
        public RouteDisplayInfo() { }

        public Feature FromFeature { get; set; }

        public Feature ToFeature { get; set; }

        public RouteResult Route { get; set; }
    }
}
