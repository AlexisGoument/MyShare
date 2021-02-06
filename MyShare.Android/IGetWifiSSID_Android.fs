namespace GetWifiSSID

open Android.Content
open Android.Net.Wifi
open Xamarin.Forms

type GetWifiSSID() =
    let wifiManager: WifiManager = (Android.App.Application.Context.GetSystemService(Context.WifiService)) :?> WifiManager

    interface IGetWifiSSID with
        member this.WifiSSID() =
            if wifiManager.IsWifiEnabled then
                Some wifiManager.ConnectionInfo.SSID
            else
                None

[<assembly: Dependency(typeof<GetWifiSSID>)>]
()