module GetWifiSSID

type IGetWifiSSID =
    interface
        abstract WifiSSID: unit -> string option
    end
