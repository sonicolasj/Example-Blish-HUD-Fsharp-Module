namespace ExampleBHUDFsModule

open Blish_HUD
open Blish_HUD.Controls
open Blish_HUD.Modules
open Blish_HUD.Settings
open Gw2Sharp.WebApi.V2.Models
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open System
open System.ComponentModel.Composition
open System.IO

[<Export(typeof<Module>)>]
type ExampleBHUDFsModule
    [<ImportingConstructor>]([<Import("ModuleParameters")>] moduleParameters: ModuleParameters) as this =
    inherit Module(moduleParameters)
    do ExampleBHUDFsModule.moduleInstance <- this

    /// <summary>
    /// This is your logger for writing to the log.  Ensure the type of of your module class.
    /// Other classes can have their own logger.  Instance those loggers the same as you have
    /// here, but with their type as the argument.
    /// </summary>
    static member private Logger: Logger = Logger.GetLogger(typeof<ExampleBHUDFsModule>)

    [<DefaultValue>] static val mutable private moduleInstance: ExampleBHUDFsModule
    static member internal ModuleInstance = ExampleBHUDFsModule.moduleInstance

    //Service Managers
    member this.SettingsManager    = this.ModuleParameters.SettingsManager
    member this.ContentsManager    = this.ModuleParameters.ContentsManager
    member this.DirectoriesManager = this.ModuleParameters.DirectoriesManager
    member this.Gw2ApiManager      = this.ModuleParameters.Gw2ApiManager

    [<DefaultValue>] val mutable private _mugTexture: Texture2D
    let mutable _runningTime = 0
    [<DefaultValue>] val mutable private _dungeons: List<Dungeon> 

    [<DefaultValue>] val mutable private _anotherExampleSetting: SettingEntry<bool>

    // Controls (be sure to dispose of these in Unload()
    [<DefaultValue>] val mutable private _exampleIcon: CornerIcon
    [<DefaultValue>] val mutable private _dungeonContextMenuStrip: ContextMenuStrip

    /// <summary>
    /// Define the settings you would like to use in your module.  Settings are persistent
    /// between updates to both Blish HUD and your module.
    /// </summary>
    override this.DefineSettings(settings: SettingCollection) =
        settings.DefineSetting("ExampleSetting.", "This is the value of the setting", "Display name of setting", "If exposed, this setting will be shown in settings with this description, automatically.") |> ignore

        // Assigning the return value is the preferred way of keeping track of your settings
        this._anotherExampleSetting <- settings.DefineSetting("AnotherExample", true, "This setting is a bool setting.", "Settings can be many different types")
        ()
    
    /// <summary>
    /// Allows your module to perform any initialization it needs before starting to run.
    /// Please note that Initialize is NOT asynchronous and will block Blish HUD's update
    /// and render loop, so be sure to not do anything here that takes too long.
    /// </summary>
    override this.Initialize() = ()

    /// <summary>
    /// Load content and more here. This call is asynchronous, so it is a good time to
    /// run any long running steps for your module. Be careful when instancing
    /// <see cref="Blish_HUD.Entities.Entity"/> and <see cref="Blish_HUD.Controls.Control"/>.
    /// Setting their parent is not thread-safe and can cause the application to crash.
    /// You will want to queue them to add later while on the main thread or in a delegate queued
    /// with <see cref="Blish_HUD.OverlayService.QueueMainThreadUpdate(Action{GameTime})"/>.
    /// </summary>
    override this.LoadAsync() =
        task {
            // Load content from the ref directory automatically with the ContentsManager
            this._mugTexture <- this.ContentsManager.GetTexture("603447.png")

            // Use the Gw2ApiManager to make requests to the API using the permissions provided in your manifest
            //let! dungeonRequest = this.Gw2ApiManager.Gw2ApiClient.V2.Dungeons.AllAsync()
            //this._dungeons <- Seq.toList dungeonRequest
            this._dungeons <- List.empty

            // If you really need to, you can recall your settings values with the SettingsManager
            // It is better if you just hold onto the returned "TypeEntry" instance when doing your initial DefineSetting, though
            let setting1 = this.SettingsManager.ModuleSettings["ExampleSetting"] :?> SettingEntry<string>

            // Get your manifest registered directories with the DirectoriesManager
            for directoryName in this.DirectoriesManager.RegisteredDirectories do
                let fullDirectoryPath = this.DirectoriesManager.GetFullDirectoryPath(directoryName)

                let allFiles = Directory.EnumerateFiles(fullDirectoryPath, "*", SearchOption.AllDirectories) |> Seq.toList

                ExampleBHUDFsModule.Logger.Info($"'{directoryName}' can be found at '{fullDirectoryPath}' and has {allFiles.Length} total files within it.")
        }

    /// <summary>
    /// Allows you to perform an action once your module has finished loading (once
    /// <see cref="LoadAsync"/> has completed).  You must call "base.OnModuleLoaded(e)" at the
    /// end for the <see cref="Module.ModuleLoaded"/> event to fire and for
    /// <see cref="Module.Loaded" /> to update correctly.
    /// </summary>
    override this.OnModuleLoaded(e: EventArgs) =
        // Add a mug icon in the top left next to the other icons
        this._exampleIcon <- new CornerIcon(
            Icon             = this._mugTexture,
            BasicTooltipText = $"{this.Name} [{this.Namespace}]",
            Parent           = GameService.Graphics.SpriteScreen
        )

        // Show a notification in the middle of the screen when the icon is clicked
        this._exampleIcon.Click.Add(fun _ ->
            ScreenNotification.ShowNotification("Hello from Blish HUD!")
        )

        // Add a right click menu to the icon that shows each Revenant legend (pulled from the API)
        this._dungeonContextMenuStrip <- new ContextMenuStrip()

        for dungeon in this._dungeons do
            let dungeonItem = this._dungeonContextMenuStrip.AddMenuItem(dungeon.Id)

            let dungeonMenu = new ContextMenuStrip()

            for path in dungeon.Paths do
                dungeonMenu.AddMenuItem($"{path.Id} ({path.Type})") |> ignore

            dungeonItem.Submenu <- dungeonMenu

        this._exampleIcon.Menu <- this._dungeonContextMenuStrip

        base.OnModuleLoaded(e)

    /// <summary>
    /// Allows your module to run logic such as updating UI elements,
    /// checking for conditions, playing audio, calculating changes, etc.
    /// This method will block the primary Blish HUD loop, so any long
    /// running tasks should be executed on a separate thread to prevent
    /// slowing down the overlay.
    /// </summary>
    override this.Update(gameTime: GameTime) =
        _runningTime <- _runningTime + Convert.ToInt32(gameTime.ElapsedGameTime.TotalMilliseconds)

        if _runningTime > 60000 then
            _runningTime <- _runningTime - 60000
            ScreenNotification.ShowNotification("The examples module shows this message every 60 seconds!", ScreenNotification.NotificationType.Warning)

    /// <summary>
    /// For a good module experience, your module should clean up ANY and ALL entities
    /// and controls that were created and added to either the World or SpriteScreen.
    /// Be sure to remove any tabs added to the Director window, CornerIcons, etc.
    /// </summary>
    override this.Unload() =
        this._exampleIcon.Dispose()
        this._dungeonContextMenuStrip.Dispose()

        // Static members are not automatically cleared and will keep a reference to your,
        // module unless manually unset.
        ExampleBHUDFsModule.moduleInstance <- Unchecked.defaultof<ExampleBHUDFsModule>
