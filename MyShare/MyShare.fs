// Copyright Fabulous contributors. See LICENSE.md for license.
namespace MyShare

open Fabulous
open Fabulous.XamarinForms
open Fabulous.XamarinForms.LiveUpdate
open Xamarin.Forms
open Xamarin.Essentials
open System.Net.Sockets
open System.Text
open System.Net

module App = 
    

    type Model = 
      { Count: int
        Step: int
        TimerOn: bool
        Ip: string
        IpTarget: string
        WifiName: string }

    type Msg = 
        | Increment 
        | Decrement 
        | Reset
        | SetStep of int
        | TimerToggled of bool
        | TimedTick
        | SendFile
        | SendHello
        | ChangeIpTarget of string

    let getIp() =
        match Dns.GetHostAddresses(Dns.GetHostName()) |> Array.tryHead with
        | Some ip -> ip.ToString()
        | None -> null

    let getWifiName() = 
        let p = Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() |> Async.AwaitIAsyncResult |> Async.RunSynchronously
        
        if Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>() then
            Permissions.RequestAsync<Permissions.LocationWhenInUse>() |> Async.AwaitIAsyncResult |> Async.RunSynchronously |> ignore
        let wifiConfig = DependencyService.Get<GetWifiSSID.IGetWifiSSID>()
        try
            match wifiConfig.WifiSSID() with
            | Some ssid -> ssid
            | None -> "[Disabled]"
        with
            | ex -> "[Failed]"

    let initModel = { Count = 0; Step = 1; TimerOn=false; Ip = getIp(); IpTarget = "192.168.1.146"; WifiName = getWifiName() }

    let init () = initModel, Cmd.none

    let timerCmd =
        async { do! Async.Sleep 200
                return TimedTick }
        |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | Increment -> { model with Count = model.Count + model.Step }, Cmd.none
        | Decrement -> { model with Count = model.Count - model.Step }, Cmd.none
        | Reset -> init ()
        | SetStep n -> { model with Step = n }, Cmd.none
        | TimerToggled on -> { model with TimerOn = on }, (if on then timerCmd else Cmd.none)
        | TimedTick -> 
            if model.TimerOn then 
                { model with Count = model.Count + model.Step }, timerCmd
            else 
                model, Cmd.none
        | SendFile ->
            let cmd =
                async {
                    let options = PickOptions(FileTypes = FilePickerFileType.Images, PickerTitle = "Pick an image")
                    let! result = FilePicker.PickAsync(options) |> Async.AwaitTask
                    match result with
                    | file ->
                            let! stream = file.OpenReadAsync() |> Async.AwaitTask
                            return None
                } |> Cmd.ofAsyncMsgOption
            model, cmd
        | SendHello ->
            let cmd =
                async {
                    let client = new TcpClient(model.IpTarget, 8090)
                    let stream = client.GetStream()
                    let bytes = Encoding.ASCII.GetBytes "Helloo"
                    stream.Write(bytes, 0, bytes.Length)
                    stream.Close()
                    client.Close()
                    return None
                } |> Cmd.ofAsyncMsgOption
            model, cmd
        | ChangeIpTarget ip -> { model with IpTarget = ip }, Cmd.none
            
                
    let view (model: Model) dispatch =
        View.ContentPage(
          content = View.StackLayout(padding = Thickness 20.0, verticalOptions = LayoutOptions.Center,
            children = [ 
                View.Label(text = sprintf "%d" model.Count, horizontalOptions = LayoutOptions.Center, width=200.0, horizontalTextAlignment=TextAlignment.Center)
                View.Button(text = "Incrementer", command = (fun () -> dispatch Increment), horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Decrement", command = (fun () -> dispatch Decrement), horizontalOptions = LayoutOptions.Center)
                View.Label(text = "Timer", horizontalOptions = LayoutOptions.Center)
                View.Switch(isToggled = model.TimerOn, toggled = (fun on -> dispatch (TimerToggled on.Value)), horizontalOptions = LayoutOptions.Center)
                View.Slider(minimumMaximum = (0.0, 10.0), value = double model.Step, valueChanged = (fun args -> dispatch (SetStep (int (args.NewValue + 0.5)))), horizontalOptions = LayoutOptions.FillAndExpand)
                View.Label(text = sprintf "Step size: %d" model.Step, horizontalOptions = LayoutOptions.Center) 
                View.Button(text = "Reset", horizontalOptions = LayoutOptions.Center, command = (fun () -> dispatch Reset), commandCanExecute = (model <> initModel))
                View.Button(text = "File", command = fun () -> dispatch SendFile)
                View.StackLayout(orientation = StackOrientation.Horizontal,
                    children = [
                        View.Label(text = sprintf "Wifi: %s" model.WifiName, horizontalOptions = LayoutOptions.StartAndExpand)
                        View.Label(text = sprintf "MyIP: %s" model.Ip, horizontalOptions = LayoutOptions.End)
                    ])
                View.StackLayout(orientation = StackOrientation.Horizontal,
                    children = [
                        View.Entry(text = model.IpTarget, textChanged = (fun(t) -> dispatch(ChangeIpTarget t.NewTextValue)), horizontalOptions = LayoutOptions.FillAndExpand)
                        View.Button(text = "SendHello", command = fun () -> dispatch SendHello)
                    ])
            ]))

    // Note, this declaration is needed if you enable LiveUpdate
    let program =
        XamarinFormsProgram.mkProgram init update view
#if DEBUG
        |> Program.withConsoleTrace
#endif

type App () as app = 
    inherit Application ()

    let runner = 
        App.program
        |> XamarinFormsProgram.run app

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/tools.html#live-update for further  instructions.
    //
    do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/models.html#saving-application-state for further  instructions.
#if APPSAVE
    let modelId = "model"
    override __.OnSleep() = 

        let json = Newtonsoft.Json.JsonConvert.SerializeObject(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<App.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()
#endif


