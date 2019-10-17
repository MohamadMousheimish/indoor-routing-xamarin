using Plugin.Connectivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal static class Reachability
{
    internal static bool IsNetworkAvailable()
    {
        return CrossConnectivity.Current.IsConnected;
    }
}

