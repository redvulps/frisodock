using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using Windows.Networking.Connectivity;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Name of the network in use. That is all (SRP).
///
/// It comes from the WinRT connection profile, and not from the WLAN API: to show the network name,
/// the profile already carries the SSID and even says whether the connection is wireless, with no
/// adapter handle to open and no interface structures to walk.
/// </summary>
public sealed class NetworkProvider : INetworkProvider
{
    public NetworkStatus GetStatus()
    {
        try
        {
            ConnectionProfile? profile = NetworkInformation.GetInternetConnectionProfile();

            if (profile is null)
            {
                return NetworkStatus.Disconnected;
            }

            return new NetworkStatus(profile.ProfileName, profile.IsWlanConnectionProfile);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return NetworkStatus.Disconnected;
        }
    }
}
