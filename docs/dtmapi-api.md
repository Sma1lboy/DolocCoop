# DTMAPI 公开 API 参考(反射自 DTMAPI.Abstractions.dll)

> 生成于 2026-08-12,DTMAPI 0.5.x-alpha(workshop 3743016467)。重新生成:运行 scratchpad 的 dump-dtmapi-api.ps1。

## 接口 (Interfaces)

### IActionCompletionApi
- `void Configure(IManifest owner, ActionCompletionOptions options)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`

### IActionSpeedApi
- `void Configure(IManifest owner, ActionSpeedOptions options)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`

### IAdvancedDebugApi
- `DebugValueResult AddMoney(IManifest owner, int amount)`
- `DebugValueResult AddTechPoint(IManifest owner, string pointTypeId, int amount)`
- `TimeSkipResult AdvanceTime(IManifest owner, AdvancedTimeAdvanceKind kind, int amount)`
- `CreativeModeState GetCreativeModeState()`
- `IReadOnlyList<SpawnDebugOption> GetMonsterOptions()`
- `IReadOnlyList<SpawnDebugOption> GetResourceOptions()`
- `BridgeFeatureStatus GetStatus()`
- `IReadOnlyList<TechPointDebugOption> GetTechPointOptions()`
- `InventoryGiveResult GiveCreativeGenerator(IManifest owner)`
- `CropMaturityResult MatureAllCrops(IManifest owner)`
- `TimeScaleDebugResult ResetTimeScale(IManifest owner, string reason)`
- `CreativeModeResult SetCreativeMode(IManifest owner, bool enabled)`
- `TimeScaleDebugResult SetTimeScale(IManifest owner, double multiplier)`
- `SpawnDebugResult SpawnMonster(IManifest owner, string monsterId, int count)`
- `SpawnDebugResult SpawnResource(IManifest owner, string resourceId, int count)`
- `DebugCommandResult UnlockAllTechTrees(IManifest owner)`

### IAnimalViewerApi
- `void ConfigureSpecialProduceProgress(IManifest owner, AnimalHusbandryProgressOptions options)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`

### IAudioReplacementApi
- `AudioReplacementState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `AudioReplacementRegisterResult RegisterReplacement(IManifest owner, AudioReplacementOptions options)`

### ICameraViewApi
- `ICameraViewLease AcquireLease(IManifest owner, CameraViewRequest request)`
- `CameraViewState GetSnapshot(string uniqueId)`
- `CameraViewState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`

### ICameraViewLease
- prop `bool IsReleased { get }`
- prop `CameraViewResult LastResult { get }`
- prop `string LeaseId { get }`
- prop `string OwnerId { get }`
- `CameraViewState GetState()`
- `CameraViewResult Release(string reason)`
- `CameraViewResult SetViewScale(double viewScale, string reason)`
- `CameraViewResult Update(CameraViewRequest request, string reason)`

### ICameraZoomApi
- `CameraZoomState GetSnapshot(string uniqueId)`
- `CameraZoomState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `CameraZoomRegisterResult Register(IManifest owner, CameraZoomOptions options)`
- `CameraZoomResult ResetViewScale(IManifest owner, string reason)`
- `CameraZoomResult SetViewScale(IManifest owner, double viewScale, string reason)`
- `CameraZoomResult StepViewScale(IManifest owner, int direction, string reason)`

### IChestLocatorEnhancerApi
- `ChestLocatorEnhancerState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `ChestLocatorEnhancerRegisterResult Register(IManifest owner, ChestLocatorEnhancerOptions options)`

### IConfigHelper
- `string GetConfigPath(IManifest manifest)`
- `TConfig ReadConfig(IManifest manifest)`
- `void RegisterMigration(IManifest manifest, Action<TConfig> migrate)`
- `void WriteConfig(IManifest manifest, TConfig config)`

### IConfigMenuItem
- prop `IReadOnlyList<string> AllowedValues { get }`
- prop `bool CanEdit { get }`
- prop `string DisplayValue { get }`
- prop `bool HasPendingChange { get }`
- prop `Nullable<double> Interval { get }`
- prop `bool IsVisible { get }`
- prop `string ItemId { get }`
- prop `string Kind { get }`
- prop `Nullable<double> MaxValue { get }`
- prop `Nullable<double> MinValue { get }`
- prop `string Name { get }`
- prop `string PendingValue { get }`
- prop `string Tooltip { get }`
- prop `string ValidationError { get }`
- `void Invoke()`
- `bool TrySetPendingValue(string value, String& error)`

### IConfigMenuPage
- prop `string DisplayName { get }`
- prop `bool HasPendingChanges { get }`
- prop `bool IsEditing { get }`
- prop `bool IsLocked { get }`
- prop `IReadOnlyList<IConfigMenuItem> Items { get }`
- prop `string LockReason { get }`
- prop `IManifest Manifest { get }`
- prop `bool TitleScreenOnly { get }`

### IContentAssetInfo
- prop `string ContentType { get }`
- prop `string RelativePath { get }`
- prop `string SourceModId { get }`
- prop `string SourcePath { get }`

### IContentItemInfo
- prop `string Category { get }`
- prop `string ChineseName { get }`
- prop `string ContentPath { get }`
- prop `bool Enabled { get }`
- prop `bool EnablementKnown { get }`
- prop `string EnglishName { get }`
- prop `string IconAssetKey { get }`
- prop `string IconPath { get }`
- prop `bool IsDtmApiContent { get }`
- prop `string ItemId { get }`
- prop `int LoadOrder { get }`
- prop `string RootPath { get }`
- prop `string SourceId { get }`
- prop `string SourceKind { get }`
- prop `string SourceModTitle { get }`
- prop `IReadOnlyList<string> Tags { get }`
- prop `Nullable<UInt64> WorkshopId { get }`

### IContentQueryHelper
- `IReadOnlyList<IContentAssetInfo> FindAssets(string contentType)`
- `IReadOnlyList<IContentItemInfo> GetAllIndexedItems()`
- `IContentItemInfo GetAnyIndexedItem(string itemId)`
- `IContentItemInfo GetIndexedItem(string itemId)`
- `IReadOnlyList<IContentItemInfo> GetIndexedItems()`
- `IReadOnlyList<string> GetKnownContentTypes()`
- `bool TryReadTextAsset(string relativePath, String& text)`

### ICropHarvestingApi
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `CropHarvestResult HarvestMatureCrops(IManifest owner, CropHarvestRequest request)`
- `CropHarvestResult ScanMatureCrops(IManifest owner, CropHarvestRequest request)`

### ICustomAnimalApi
- event `EventHandler<CustomAnimalLifecycleEventArgs> LifecycleChanged`
- `CustomAnimalInstanceSnapshot GetAnimalInstance(CustomEntityHandle handle)`
- `IReadOnlyList<CustomAnimalInstanceSnapshot> GetAnimalInstances(string ownerUniqueId)`
- `CustomEntityFamilySnapshot GetSnapshot(string ownerUniqueId)`
- `CustomAnimalSpeciesDefinition GetSpeciesDefinition(string speciesId)`
- `IReadOnlyList<CustomAnimalSpeciesDefinition> GetSpeciesDefinitions(string ownerUniqueId)`
- `CustomEntityCapabilityStatus GetStatus(string ownerUniqueId)`
- `CustomAnimalRegistrationResult RegisterSpecies(IManifest owner, CustomAnimalSpeciesDefinition definition)`
- `CustomEntityRequestResult RequestRemove(IManifest owner, CustomEntityHandle animalHandle, string reason)`
- `CustomAnimalSpawnResult RequestSpawn(IManifest owner, CustomAnimalSpawnRequest request)`
- `CustomEntityUnregisterResult UnregisterSpecies(IManifest owner, string speciesId)`

### ICustomAnimalBehaviorProvider
- `void OnBreedingEvent(CustomAnimalLifecycleEventArgs args)`
- `void OnExcrementProduced(CustomAnimalLifecycleEventArgs args)`
- `CustomEntityBehaviorResult OnFeedRequested(CustomAnimalBehaviorContext context, string itemId, int amount)`
- `void OnHiddenProductChanged(CustomAnimalLifecycleEventArgs args)`
- `CustomEntityBehaviorResult OnTick(CustomAnimalBehaviorContext context)`

### ICustomAttackApi
- event `EventHandler<CustomAttackLifecycleEventArgs> LifecycleChanged`
- `CustomAttackSpawnResult ExecuteAttack(IManifest owner, CustomAttackSpawnRequest request)`
- `IReadOnlyList<CustomAttackInstanceSnapshot> GetActiveAttacks(string ownerUniqueId)`
- `CustomAttackDefinition GetAttackDefinition(string attackId)`
- `IReadOnlyList<CustomAttackDefinition> GetAttackDefinitions(string ownerUniqueId)`
- `CustomAttackInstanceSnapshot GetAttackInstance(CustomEntityHandle handle)`
- `CustomEntityFamilySnapshot GetSnapshot(string ownerUniqueId)`
- `CustomEntityCapabilityStatus GetStatus(string ownerUniqueId)`
- `CustomAttackRegistrationResult RegisterAttack(IManifest owner, CustomAttackDefinition definition)`
- `CustomEntityRequestResult RequestExpire(IManifest owner, CustomEntityHandle attackHandle, string reason)`
- `CustomAttackSpawnResult SpawnProjectile(IManifest owner, CustomAttackSpawnRequest request)`
- `CustomEntityUnregisterResult UnregisterAttack(IManifest owner, string attackId)`

### ICustomAttackBehaviorProvider
- `void OnCollision(CustomAttackLifecycleEventArgs args)`
- `void OnDamageApplied(CustomAttackLifecycleEventArgs args)`
- `void OnExpired(CustomAttackLifecycleEventArgs args)`
- `CustomEntityBehaviorResult OnPatternTick(CustomAttackBehaviorContext context)`

### ICustomDroneApi
- event `EventHandler<CustomDroneLifecycleEventArgs> LifecycleChanged`
- `CustomDroneEquipmentResult Equip(IManifest owner, CustomEntityHandle droneHandle, CustomDroneEquipmentRequest request)`
- `CustomDroneDefinition GetDroneDefinition(string droneId)`
- `IReadOnlyList<CustomDroneDefinition> GetDroneDefinitions(string ownerUniqueId)`
- `CustomDroneInstanceSnapshot GetDroneInstance(CustomEntityHandle handle)`
- `IReadOnlyList<CustomDroneInstanceSnapshot> GetDroneInstances(string ownerUniqueId)`
- `CustomEntityFamilySnapshot GetSnapshot(string ownerUniqueId)`
- `CustomEntityCapabilityStatus GetStatus(string ownerUniqueId)`
- `CustomDroneRegistrationResult RegisterDrone(IManifest owner, CustomDroneDefinition definition)`
- `CustomEntityRequestResult RequestDismiss(IManifest owner, CustomEntityHandle droneHandle, string reason)`
- `CustomDroneSummonResult RequestSummon(IManifest owner, CustomDroneSummonRequest request)`
- `CustomDroneCommandResult SetMode(IManifest owner, CustomEntityHandle droneHandle, CustomDroneCommandRequest request)`
- `CustomEntityUnregisterResult UnregisterDrone(IManifest owner, string droneId)`

### ICustomDroneBehaviorProvider
- `void OnDamaged(CustomDroneLifecycleEventArgs args)`
- `void OnDestroyed(CustomDroneLifecycleEventArgs args)`
- `void OnRepaired(CustomDroneLifecycleEventArgs args)`
- `CustomEntityBehaviorResult OnTick(CustomDroneBehaviorContext context)`
- `string SelectAttack(CustomDroneBehaviorContext context, IReadOnlyList<string> availableAttackIds)`
- `CustomDroneCommandResult SelectMode(CustomDroneBehaviorContext context)`
- `CustomEntityMovementKind SelectMovement(CustomDroneBehaviorContext context)`

### ICustomEntityBehaviorProvider
- `void MigrateSaveState(CustomEntitySaveMigrationContext context)`
- `void OnRegistered(CustomEntityBehaviorContext context)`
- `void OnUnregistered(CustomEntityBehaviorContext context)`

### ICustomMonsterApi
- event `EventHandler<CustomMonsterLifecycleEventArgs> LifecycleChanged`
- `CustomMonsterDefinition GetMonsterDefinition(string monsterId)`
- `IReadOnlyList<CustomMonsterDefinition> GetMonsterDefinitions(string ownerUniqueId)`
- `CustomMonsterInstanceSnapshot GetMonsterInstance(CustomEntityHandle handle)`
- `IReadOnlyList<CustomMonsterInstanceSnapshot> GetMonsterInstances(string ownerUniqueId)`
- `CustomEntityFamilySnapshot GetSnapshot(string ownerUniqueId)`
- `CustomEntityCapabilityStatus GetStatus(string ownerUniqueId)`
- `CustomMonsterRegistrationResult RegisterMonster(IManifest owner, CustomMonsterDefinition definition)`
- `CustomMonsterRegistrationResult RegisterSpawnTable(IManifest owner, CustomMonsterSpawnTableDefinition spawnTable)`
- `CustomEntityRequestResult RequestDespawn(IManifest owner, CustomEntityHandle monsterHandle, string reason)`
- `CustomMonsterSpawnResult RequestSpawn(IManifest owner, CustomMonsterSpawnRequest request)`
- `CustomEntityUnregisterResult UnregisterMonster(IManifest owner, string monsterId)`

### ICustomMonsterBehaviorProvider
- `void OnDamaged(CustomMonsterLifecycleEventArgs args)`
- `void OnDeath(CustomMonsterLifecycleEventArgs args)`
- `void OnTargetChanged(CustomMonsterLifecycleEventArgs args)`
- `CustomEntityBehaviorResult OnTick(CustomMonsterBehaviorContext context)`
- `string SelectAttack(CustomMonsterBehaviorContext context, IReadOnlyList<string> availableAttackIds)`
- `CustomEntityMovementKind SelectMovement(CustomMonsterBehaviorContext context)`

### IDebugConsoleApi
- prop `bool IsOpen { get }`
- `void Bind(IManifest owner, IInventoryDebugApi inventoryApi, IWeatherDebugApi weatherApi, ITeleportDebugApi teleportApi, ITimeDebugApi timeApi, IMovementDebugApi movementApi, IInstantSaveDebugApi instantSaveApi)`
- `void BindAdvanced(IManifest owner, IAdvancedDebugApi advancedDebugApi)`
- `void Close(IManifest owner, string reason)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `void Open(IManifest owner, string reason)`
- `void SetLanguage(IManifest owner, string language)`
- `void Toggle(IManifest owner, string reason)`

### IDiagnosticsEvents
- event `EventHandler<LogExportedEventArgs> LogExported`
- event `EventHandler<HookStatusChangedEventArgs> HookStatusChanged`

### IDiagnosticsHelper
- `string ExportLogs()`
- `IReadOnlyList<IDtmErrorInfo> GetErrors()`
- `IReadOnlyList<IHookStatusInfo> GetHookStatuses()`
- `string GetLatestLogPath()`
- `IReadOnlyList<IDtmWarningInfo> GetWarnings()`
- `void RecordEvidence(string caseId, string summary)`

### IDtmConfigMenuApi
- `void AddBoolOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<bool> getValue, Action<bool> setValue)`
- `void AddBoolOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<bool> getValue, Action<bool> setValue, Func<bool> canEdit, Func<bool> isVisible)`
- `void AddButton(IManifest mod, Func<string> name, Func<string> tooltip, Action onPressed)`
- `void AddChoiceOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<string> getValue, Action<string> setValue, IReadOnlyList<string> allowedValues)`
- `void AddColorPresetOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<string> getValue, Action<string> setValue, IReadOnlyList<DtmColorPreset> presets)`
- `void AddInlineBoolBoolOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<bool> getEnabled, Action<bool> setEnabled, Func<string> secondaryName, Func<string> secondaryTooltip, Func<bool> getSecondaryValue, Action<bool> setSecondaryValue, Func<bool> secondaryVisible)`
- `void AddInlineBoolNumberOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<bool> getEnabled, Action<bool> setEnabled, Func<double> getValue, Action<double> setValue, double min, double max, double interval)`
- `void AddKeybindOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<string> getValue, Action<string> setValue)`
- `void AddNumberOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<double> getValue, Action<double> setValue, double min, double max, double interval)`
- `void AddParagraph(IManifest mod, Func<string> text)`
- `void AddSectionTitle(IManifest mod, Func<string> text)`
- `void AddTextOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<string> getValue, Action<string> setValue)`
- `void AddTextOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<string> getValue, Action<string> setValue, Func<bool> canEdit, Func<bool> isVisible)`
- `IReadOnlyList<string> GetKeybindConflicts(string uniqueId)`
- `void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly)`
- `void SetDisplayName(IManifest mod, Func<string> name)`

### IDtmConfigMenuKeybindDefaultsApi
- `void AddKeybindOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<string> getValue, Action<string> setValue, Func<string> getDefaultValue)`

### IDtmDiagnosticsApi
- `IDtmDiagnosticsSnapshot GetSnapshot()`

### IDtmDiagnosticsSnapshot
- prop `IReadOnlyList<IDtmErrorInfo> Errors { get }`
- prop `IReadOnlyList<IDtmFeatureStatusInfo> FeatureStatuses { get }`
- prop `IReadOnlyList<IHookStatusInfo> HookStatuses { get }`
- prop `string LatestLogPath { get }`
- prop `string LatestReportPath { get }`
- prop `IReadOnlyList<IDtmLoadedModInfo> LoadedMods { get }`
- prop `IReadOnlyList<IDtmModStatusInfo> Mods { get }`
- prop `DateTimeOffset StartedAt { get }`
- prop `IReadOnlyList<IDtmWarningInfo> Warnings { get }`

### IDtmErrorInfo
- prop `string Details { get }`
- prop `string Message { get }`
- prop `string Owner { get }`
- prop `DateTimeOffset Time { get }`

### IDtmFeatureStatusInfo
- prop `string Details { get }`
- prop `int FailureCount { get }`
- prop `string FeatureId { get }`
- prop `string LastError { get }`
- prop `string LastOperation { get }`
- prop `string Status { get }`
- prop `bool Success { get }`
- prop `DateTimeOffset UpdatedAt { get }`

### IDtmHelper
- prop `IConfigHelper Config { get }`
- prop `IContentQueryHelper Content { get }`
- prop `IDiagnosticsHelper Diagnostics { get }`
- prop `IEventsHelper Events { get }`
- prop `IInputHelper Input { get }`
- prop `IManifest ModManifest { get }`
- prop `IModRegistry ModRegistry { get }`
- prop `IMonitor Monitor { get }`
- prop `ITranslationHelper Translation { get }`
- prop `IUiHelper UI { get }`
- prop `IWorkshopHelper Workshop { get }`
- `TConfig ReadConfig()`
- `void WriteConfig(TConfig config)`

### IDtmLoadedModInfo
- prop `string EntryType { get }`
- prop `string Name { get }`
- prop `string Type { get }`
- prop `string UniqueID { get }`
- prop `string Version { get }`

### IDtmModStatusInfo
- prop `string EnablementReason { get }`
- prop `string EntryDll { get }`
- prop `string EntryType { get }`
- prop `bool Loaded { get }`
- prop `string ManifestPath { get }`
- prop `string Name { get }`
- prop `bool OfficialEnabled { get }`
- prop `bool OfficialEnablementManaged { get }`
- prop `string OfficialId { get }`
- prop `string Reason { get }`
- prop `string RootPath { get }`
- prop `string Source { get }`
- prop `string Status { get }`
- prop `string StatusCode { get }`
- prop `string Type { get }`
- prop `string UniqueID { get }`
- prop `string Version { get }`

### IDtmWarningInfo
- prop `string Details { get }`
- prop `string Message { get }`
- prop `string Owner { get }`
- prop `DateTimeOffset Time { get }`

### IEquipmentSlotsApi
- `EquipmentSlotEquipResult EquipExtraSlot(IManifest owner, string slotId, string itemId)`
- `IReadOnlyList<EquipmentSlotInfo> GetSlots(string uniqueId)`
- `EquipmentSlotsState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `EquipmentSlotsRecoveryResult RecoverExtraSlotItems(IManifest owner, string reason)`
- `EquipmentSlotsRegisterResult RegisterSlots(IManifest owner, EquipmentSlotsOptions options)`
- `EquipmentSlotEquipResult UnequipExtraSlot(IManifest owner, string slotId, string reason)`

### IEventsHelper
- prop `IDiagnosticsEvents Diagnostics { get }`
- prop `IGameLoopEvents GameLoop { get }`
- prop `IInputEvents Input { get }`
- prop `ISaveEvents Save { get }`
- prop `IUiEvents UI { get }`
- prop `IWorkshopEvents Workshop { get }`

### IFishingAutomationApi
- `void Configure(IManifest owner, FishingAutomationOptions options)`
- `FishingAutomationState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `void SetEnabled(IManifest owner, bool enabled, string reason)`

### IGameLoopEvents
- event `EventHandler<GameLaunchedEventArgs> GameLaunched`
- event `EventHandler<UpdateTickedEventArgs> UpdateTicked`
- event `EventHandler<OneSecondUpdateTickedEventArgs> OneSecondUpdateTicked`
- event `EventHandler<ReturnedToTitleEventArgs> ReturnedToTitle`

### IHookStatusInfo
- prop `string Details { get }`
- prop `string HookId { get }`
- prop `string Source { get }`
- prop `string Status { get }`
- prop `DateTimeOffset UpdatedAt { get }`

### IInputEvents
- event `EventHandler<ButtonPressedEventArgs> ButtonPressed`
- event `EventHandler<ButtonReleasedEventArgs> ButtonReleased`
- event `EventHandler<KeybindPressedEventArgs> KeybindPressed`
- event `EventHandler<KeybindReleasedEventArgs> KeybindReleased`

### IInputHelper
- `IReadOnlyList<string> GetRegisteredButtons()`
- `DtmButtonState GetState(DtmButton button)`
- `IReadOnlyList<string> GetSuppressedButtons()`
- `bool IsDown(string button)`
- `bool IsDown(DtmButton button)`
- `bool IsKeybindDown(string id)`
- `void RegisterButton(string button)`
- `IInputRegistration RegisterKeybind(string id, string keybindText, DtmInputScope scope)`
- `IInputRegistration RegisterKeybind(string id, DtmKeybindList keybinds, DtmInputScope scope)`
- `void Suppress(string button)`
- `void UnregisterButton(string button)`
- `bool WasKeybindPressed(string id)`
- `bool WasPressed(string button)`
- `bool WasPressed(DtmButton button)`
- `bool WasReleased(string button)`
- `bool WasReleased(DtmButton button)`

### IInputRegistration
- prop `string Id { get }`
- prop `bool IsDisposed { get }`
- prop `DtmKeybindList Keybinds { get }`
- prop `string OwnerId { get }`
- prop `DtmInputScope Scope { get }`
- `void Update(string keybindText, DtmInputScope scope)`
- `void Update(DtmKeybindList keybinds, DtmInputScope scope)`

### IInstantSaveDebugApi
- `InstantSaveDebugState GetState()`
- `BridgeFeatureStatus GetStatus()`
- `InstantSaveDebugResult Save(IManifest owner, bool reloadAfterSave)`

### IInventoryDebugApi
- `InventoryDebugPage GetItems(InventoryDebugQuery query)`
- `BridgeFeatureStatus GetStatus()`
- `InventoryGiveResult GiveItem(IManifest owner, string itemId, int count)`

### IItemDisplayNameApi
- `bool TryGetDisplayName(string itemId, String& displayName)`

### IItemTooltipApi
- `void ConfigureFishRoeProvider(IManifest owner, FishRoeTooltipOptions options, Func<string, FishRoeDisplayInfo> lookup)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`

### ILampControlApi
- `LampManualToggleState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `LampManualToggleRegisterResult RegisterManualToggle(IManifest owner, LampManualToggleOptions options)`

### IMachineProductionApi
- `IReadOnlyList<MachineDefinition> GetMachines(string uniqueId)`
- `MachineProductionState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `MachineRegisterResult RegisterMachine(IManifest owner, MachineDefinition definition)`

### IMailDeliveryApi
- `BridgeFeatureStatus GetStatus()`
- `MailItemDeliveryResult SendItemMail(IManifest owner, MailItemDeliveryRequest request)`

### IManifest
- prop `string Author { get }`
- prop `IReadOnlyList<IManifestDependency> Dependencies { get }`
- prop `string Description { get }`
- prop `string EntryDll { get }`
- prop `string EntryType { get }`
- prop `string MinimumDTMApiVersion { get }`
- prop `string MinimumGameVersion { get }`
- prop `string Name { get }`
- prop `string Type { get }`
- prop `string UniqueID { get }`
- prop `IReadOnlyList<string> UpdateKeys { get }`
- prop `string Version { get }`

### IManifestDependency
- prop `string MinimumVersion { get }`
- prop `bool Required { get }`
- prop `string UniqueID { get }`

### IModRegistry
- `IManifest Get(string uniqueId)`
- `IReadOnlyList<IManifest> GetAll()`
- `TApi GetApi(string uniqueId)`
- `bool IsLoaded(string uniqueId)`
- `void RegisterApi(TApi api)`

### IMonitor
- `void Log(string message, LogLevel level)`
- `void LogException(Exception exception, string message)`
- `void LogOnce(string key, string message, LogLevel level)`

### IMovementDebugApi
- `MovementDebugState GetState()`
- `BridgeFeatureStatus GetStatus()`
- `MovementSpeedResult ResetSpeed(IManifest owner, string reason)`
- `MovementSpeedResult SetSpeedMultiplier(IManifest owner, double multiplier)`

### IPanoramaCameraApi

### ISaveEvents
- event `EventHandler<SaveLoadedEventArgs> SaveLoaded`
- event `EventHandler<SaveSavingEventArgs> SaveSaving`
- event `EventHandler<SaveSavedEventArgs> SaveSaved`

### ISaveSlotsApi
- `SaveSlotsState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `SaveSlotsRegisterResult RegisterSlots(IManifest owner, SaveSlotsOptions options)`

### IStrongPlantingGunApi
- `StrongPlantingGunState GetState(string uniqueId)`
- `BridgeFeatureStatus GetStatus(string uniqueId)`
- `StrongPlantingGunRegisterResult Register(IManifest owner, StrongPlantingGunOptions options)`

### ITeleportDebugApi
- `TeleportCsvExportResult ExportDestinationsCsv(IManifest owner)`
- `TeleportSnapshot GetCurrentSnapshot()`
- `IReadOnlyList<TeleportDestination> GetDestinations()`
- `BridgeFeatureStatus GetStatus()`
- `TeleportResult Teleport(IManifest owner, string destinationId)`

### ITimeDebugApi
- `TimeDebugState GetState()`
- `BridgeFeatureStatus GetStatus()`
- `TimeSkipResult SkipToNextWeatherPeriod(IManifest owner)`

### ITranslationHelper
- prop `string Language { get }`
- `string Get(string key, string fallback)`

### IUiEvents
- event `EventHandler<MenuOpenedEventArgs> MenuOpened`
- event `EventHandler<MenuClosedEventArgs> MenuClosed`

### IUiHelper
- `string ExportLogs()`
- `void OpenConfigPage(string uniqueId)`
- `void OpenDtmApiStatusPage()`
- `void OpenErrorPage()`
- `void OpenHookStatusPage()`
- `void OpenModListPage()`

### IWeatherDebugApi
- `IReadOnlyList<WeatherDebugOption> GetAvailableWeathers()`
- `WeatherDebugState GetState()`
- `BridgeFeatureStatus GetStatus()`
- `WeatherSetResult SetWeather(IManifest owner, string weatherId, bool patchCurrentPeriod)`

### IWorkshopEvents
- event `EventHandler<WorkshopModListChangedEventArgs> ModListChanged`

### IWorkshopHelper
- `IReadOnlyList<IWorkshopModInfo> GetDtmApiMods()`
- `string GetEnablementHint(IWorkshopModInfo mod)`
- `IReadOnlyList<IWorkshopModInfo> GetOfficialMods()`
- `bool IsOfficialEnablementManaged(IWorkshopModInfo mod)`

### IWorkshopModInfo
- prop `bool CanDTMApiToggle { get }`
- prop `bool IsEnabledByOfficialPath { get }`
- prop `string Name { get }`
- prop `string RootPath { get }`
- prop `string Source { get }`
- prop `string UniqueID { get }`
- prop `Nullable<UInt64> WorkshopId { get }`

## 类与结构体 (Classes & Structs)

### ActionCompletionOptions
- prop `bool CompleteFeeder { get; set }`
- prop `bool CompleteGarbage { get; set }`
- prop `bool CompleteMachineFuel { get; set }`
- prop `bool CompleteOres { get; set }`
- prop `bool CompleteTrees { get; set }`
- prop `bool CompleteWeeds { get; set }`
- prop `bool Enabled { get; set }`
- prop `bool VerboseLogging { get; set }`

### ActionSpeedOptions
- prop `bool AutoFillBottle { get; set }`
- prop `double AutoFillCooldownSeconds { get; set }`
- prop `bool AutoFillStrong { get; set }`
- prop `double AutoFillStrongCooldownSeconds { get; set }`
- prop `double BottleFillMultiplier { get; set }`
- prop `bool BottleFillSpeedEnabled { get; set }`
- prop `bool ContinuousDrinkWithRightClick { get; set }`
- prop `double EatDrinkMultiplier { get; set }`
- prop `bool EatDrinkSpeedEnabled { get; set }`
- prop `bool Enabled { get; set }`
- prop `double HarvestMultiplier { get; set }`
- prop `bool HarvestSpeedEnabled { get; set }`
- prop `double MachineAddMultiplier { get; set }`
- prop `bool MachineAddSpeedEnabled { get; set }`
- prop `double PlantMultiplier { get; set }`
- prop `bool PlantSpeedEnabled { get; set }`
- prop `double ToolMultiplier { get; set }`
- prop `bool ToolSpeedEnabled { get; set }`
- prop `bool VerboseLogging { get; set }`

### AnimalHusbandryProgressOptions
- prop `int CacheSeconds { get; set }`
- prop `bool Enabled { get; set }`
- prop `DtmColor ProgressColor { get; set }`
- prop `bool VerboseLogging { get; set }`

### AudioReplacementEntryInfo
- prop `string AudioPath { get; set }`
- prop `bool Enabled { get; set }`
- prop `string LastMessage { get; set }`
- prop `string LoadStatus { get; set }`
- prop `string NativeSoundEvent { get; set }`
- prop `bool PreloadReady { get; set }`
- prop `string ReplacementId { get; set }`

### AudioReplacementOptions
- prop `string AudioPath { get; set }`
- prop `int CooldownMilliseconds { get; set }`
- prop `bool Enabled { get; set }`
- prop `string NativeSoundEvent { get; set }`
- prop `string ReplacementId { get; set }`
- prop `bool SuppressNativeWhenReady { get; set }`
- prop `bool VerboseLogging { get; set }`
- prop `double Volume { get; set }`

### AudioReplacementRegisterResult
- prop `bool Enabled { get; set }`
- prop `string FailureReason { get; set }`
- prop `bool HookInstalled { get; set }`
- prop `string Message { get; set }`
- prop `string NativeSoundEvent { get; set }`
- prop `string OwnerId { get; set }`
- prop `bool PreloadReady { get; set }`
- prop `string ReplacementId { get; set }`
- prop `bool Success { get; set }`

### AudioReplacementState
- prop `bool Enabled { get; set }`
- prop `bool HookInstalled { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `string LastMessage { get; set }`
- prop `string LastNativeSoundEvent { get; set }`
- prop `bool LastNativeSuppressed { get; set }`
- prop `string LastReplacementId { get; set }`
- prop `bool LastReplacementPlayed { get; set }`
- prop `string OwnerId { get; set }`
- prop `int ReplacementCount { get; set }`
- prop `IReadOnlyList<AudioReplacementEntryInfo> Replacements { get; set }`
- prop `string Status { get; set }`

### BridgeFeatureStatus
- prop `string Details { get }`
- prop `string Status { get }`

### ButtonPressedEventArgs : EventArgs
- prop `string Button { get }`
- prop `DtmButton PhysicalButton { get }`

### ButtonReleasedEventArgs : EventArgs
- prop `string Button { get }`
- prop `DtmButton PhysicalButton { get }`

### CameraViewRequest
- prop `bool Enabled { get; set }`
- prop `string LeaseName { get; set }`
- prop `double MaxViewScale { get; set }`
- prop `double MinViewScale { get; set }`
- prop `int Priority { get; set }`
- prop `double Step { get; set }`
- prop `bool VerboseLogging { get; set }`
- prop `double ViewScale { get; set }`

### CameraViewResult
- prop `string ActiveLeaseId { get; set }`
- prop `string ActiveOwnerId { get; set }`
- prop `double AfterViewScale { get; set }`
- prop `double AppliedOrthographicSize { get; set }`
- prop `double AppliedViewScale { get; set }`
- prop `string ArbitrationStatus { get; set }`
- prop `double BeforeViewScale { get; set }`
- prop `string CameraOwnerStatus { get; set }`
- prop `double ClampedViewScale { get; set }`
- prop `string FailureReason { get; set }`
- prop `string LeaseId { get; set }`
- prop `string LifecycleStatus { get; set }`
- prop `string Message { get; set }`
- prop `string NativeRefreshStatus { get; set }`
- prop `string OwnerId { get; set }`
- prop `double RequestedViewScale { get; set }`
- prop `bool Success { get; set }`
- prop `string UiScaleStatus { get; set }`
- prop `double VanillaOrthographicSize { get; set }`

### CameraViewState
- prop `string ActiveLeaseId { get; set }`
- prop `string ActiveOwnerId { get; set }`
- prop `double AppliedOrthographicSize { get; set }`
- prop `double AppliedViewScale { get; set }`
- prop `string ArbitrationStatus { get; set }`
- prop `bool CameraAvailable { get; set }`
- prop `string CameraOwnerStatus { get; set }`
- prop `double ClampedViewScale { get; set }`
- prop `string CurrentRoomId { get; set }`
- prop `bool CurrentRoomShowsBackground { get; set }`
- prop `string CurrentRoomTitle { get; set }`
- prop `double CurrentViewScale { get; set }`
- prop `bool Enabled { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `bool IsReleased { get; set }`
- prop `string LastMessage { get; set }`
- prop `int LeaseCount { get; set }`
- prop `string LeaseId { get; set }`
- prop `string LeaseName { get; set }`
- prop `string LifecycleStatus { get; set }`
- prop `double MaxViewScale { get; set }`
- prop `double MinViewScale { get; set }`
- prop `string NativeRefreshStatus { get; set }`
- prop `string OwnerId { get; set }`
- prop `int Priority { get; set }`
- prop `double RequestedViewScale { get; set }`
- prop `string Status { get; set }`
- prop `double Step { get; set }`
- prop `string UiScaleStatus { get; set }`
- prop `double VanillaOrthographicSize { get; set }`

### CameraZoomOptions
- prop `bool CompensateBackground { get; set }`
- prop `bool CompensateDepthFog { get; set }`
- prop `bool Enabled { get; set }`
- prop `double MaxViewScale { get; set }`
- prop `double MinViewScale { get; set }`
- prop `bool RefreshCameraController { get; set }`
- prop `bool RefreshScanners { get; set }`
- prop `double Step { get; set }`
- prop `bool VerboseLogging { get; set }`

### CameraZoomRegisterResult
- prop `string ActiveOwnerId { get; set }`
- prop `double AppliedViewScale { get; set }`
- prop `string BackgroundCompensationStatus { get; set }`
- prop `string CameraControllerStatus { get; set }`
- prop `double CurrentViewScale { get; set }`
- prop `string FailureReason { get; set }`
- prop `string FogCompensationStatus { get; set }`
- prop `string LifecycleRestoreStatus { get; set }`
- prop `double MaxViewScale { get; set }`
- prop `string Message { get; set }`
- prop `double MinViewScale { get; set }`
- prop `string OwnerId { get; set }`
- prop `string ScannerRefreshStatus { get; set }`
- prop `bool Success { get; set }`
- prop `string UiScaleStatus { get; set }`

### CameraZoomResult
- prop `string ActiveOwnerId { get; set }`
- prop `double AfterViewScale { get; set }`
- prop `double AppliedOrthographicSize { get; set }`
- prop `double AppliedViewScale { get; set }`
- prop `string BackgroundCompensationStatus { get; set }`
- prop `double BeforeViewScale { get; set }`
- prop `string CameraControllerStatus { get; set }`
- prop `double ClampedViewScale { get; set }`
- prop `string FailureReason { get; set }`
- prop `string FogCompensationStatus { get; set }`
- prop `string LifecycleRestoreStatus { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `double RequestedViewScale { get; set }`
- prop `string ScannerRefreshStatus { get; set }`
- prop `bool Success { get; set }`
- prop `string UiScaleStatus { get; set }`
- prop `double VanillaOrthographicSize { get; set }`

### CameraZoomState
- prop `string ActiveOwnerId { get; set }`
- prop `double AppliedOrthographicSize { get; set }`
- prop `double AppliedViewScale { get; set }`
- prop `string BackgroundCompensationStatus { get; set }`
- prop `bool CameraAvailable { get; set }`
- prop `string CameraControllerStatus { get; set }`
- prop `double ClampedViewScale { get; set }`
- prop `bool CompensateBackground { get; set }`
- prop `bool CompensateDepthFog { get; set }`
- prop `string CurrentRoomId { get; set }`
- prop `bool CurrentRoomShowsBackground { get; set }`
- prop `string CurrentRoomTitle { get; set }`
- prop `double CurrentViewScale { get; set }`
- prop `bool Enabled { get; set }`
- prop `string FogCompensationStatus { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `string LastMessage { get; set }`
- prop `string LifecycleRestoreStatus { get; set }`
- prop `double MaxViewScale { get; set }`
- prop `double MinViewScale { get; set }`
- prop `string OwnerId { get; set }`
- prop `bool RefreshCameraController { get; set }`
- prop `bool RefreshScanners { get; set }`
- prop `double RequestedViewScale { get; set }`
- prop `string ScannerRefreshStatus { get; set }`
- prop `string Status { get; set }`
- prop `double Step { get; set }`
- prop `string UiScaleStatus { get; set }`
- prop `double VanillaOrthographicSize { get; set }`

### ChestLocatorEnhancerOptions
- prop `bool Enabled { get; set }`
- prop `bool IncludeSharedCases { get; set }`
- prop `bool IncludeSharedStorageShelfBoxes { get; set }`
- prop `bool RespectNativeAutoUseBoxSetting { get; set }`
- prop `bool VerboseLogging { get; set }`

### ChestLocatorEnhancerRegisterResult
- prop `bool Enabled { get; set }`
- prop `string FailureReason { get; set }`
- prop `bool HookInstalled { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `bool Success { get; set }`

### ChestLocatorEnhancerState
- prop `bool Enabled { get; set }`
- prop `int ExtensionApplications { get; set }`
- prop `bool HookInstalled { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `int LastAppendedInventoryCount { get; set }`
- prop `int LastBaseInventoryCount { get; set }`
- prop `string LastMessage { get; set }`
- prop `int LastScannedEquipmentCount { get; set }`
- prop `int LastScannedRootCount { get; set }`
- prop `int LastSharedCaseCount { get; set }`
- prop `int LastSharedStorageBoxCount { get; set }`
- prop `string OwnerId { get; set }`
- prop `string Status { get; set }`

### CreativeModeResult
- prop `CreativeModeState After { get; set }`
- prop `CreativeModeState Before { get; set }`
- prop `bool Enabled { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `bool Success { get; set }`

### CreativeModeState
- prop `bool Enabled { get; set }`
- prop `string GeneratorItemId { get; set }`
- prop `bool GeneratorRuntimeAvailable { get; set }`
- prop `string LastMessage { get; set }`
- prop `bool RuntimeHooksInstalled { get; set }`

### CropHarvestRequest
- prop `bool DryRun { get; set }`
- prop `bool IncludeBushes { get; set }`
- prop `bool IncludeMushroomBags { get; set }`
- prop `bool IncludeOrdinaryCrops { get; set }`
- prop `bool IncludeTreeBasinCrops { get; set }`
- prop `bool IncludeVines { get; set }`
- prop `int MaxHarvests { get; set }`
- prop `CropHarvestScope Scope { get; set }`
- prop `bool SendNativeMessage { get; set }`
- prop `IReadOnlyList<string> TargetIds { get; set }`
- prop `bool VerboseLogging { get; set }`

### CropHarvestResult
- prop `bool Busy { get; set }`
- prop `bool DryRun { get; set }`
- prop `int FailedCount { get; set }`
- prop `string FailureReason { get; set }`
- prop `int HarvestedCount { get; set }`
- prop `int MatureTargetsFound { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `int PlantBasinsVisited { get; set }`
- prop `int RoomsVisited { get; set }`
- prop `CropHarvestScope Scope { get; set }`
- prop `int SkippedCount { get; set }`
- prop `bool Success { get; set }`
- prop `IReadOnlyList<CropHarvestTargetResult> Targets { get; set }`

### CropHarvestTargetResult
- prop `string CropId { get; set }`
- prop `string CropTitle { get; set }`
- prop `string EquipmentName { get; set }`
- prop `bool IsMature { get; set }`
- prop `CropHarvestTargetKind Kind { get; set }`
- prop `string Message { get; set }`
- prop `string RoomId { get; set }`
- prop `string RoomTitle { get; set }`
- prop `CropHarvestTargetStatus Status { get; set }`
- prop `string TargetId { get; set }`

### CropMaturityResult
- prop `int CropsMatured { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `int PlantBasinsVisited { get; set }`
- prop `bool Success { get; set }`

### CustomAnimalBehaviorContext
- prop `CustomEntityBehaviorContext Entity { get; set }`
- prop `CustomAnimalInstanceSnapshot Snapshot { get; set }`

### CustomAnimalBreedingPolicy
- prop `IReadOnlyList<string> CompatibleSpeciesIds { get; set }`
- prop `double CooldownHours { get; set }`
- prop `bool Enabled { get; set }`
- prop `int OffspringCount { get; set }`
- prop `int PopulationLimitPerOwner { get; set }`
- prop `double PregnancyOrIncubationHours { get; set }`

### CustomAnimalConsumptionPolicy
- prop `double HungerIntervalHours { get; set }`
- prop `int MaxFeedCapacity { get; set }`
- prop `bool RaiseFeedEvents { get; set }`
- prop `bool TrackFedState { get; set }`

### CustomAnimalDietPolicy
- prop `IReadOnlyList<string> AcceptedItemIds { get; set }`
- prop `IReadOnlyList<string> AcceptedItemTags { get; set }`
- prop `bool CanGraze { get; set }`
- prop `int UnitsPerFeeding { get; set }`

### CustomAnimalExcrementPolicy
- prop `bool Enabled { get; set }`
- prop `double IntervalHours { get; set }`
- prop `int MaxPendingCount { get; set }`
- prop `IReadOnlyList<CustomAnimalItemOutput> Outputs { get; set }`
- prop `bool RequiresManualCleanup { get; set }`

### CustomAnimalHabitatPolicy
- prop `IReadOnlyList<string> AllowedBuildingTags { get; set }`
- prop `IReadOnlyList<string> AllowedHabitatTags { get; set }`
- prop `IReadOnlyList<string> AllowedRoomIds { get; set }`
- prop `int PopulationLimitPerRoom { get; set }`
- prop `bool RequiresShelter { get; set }`

### CustomAnimalInstanceSnapshot
- prop `int AgeDays { get; set }`
- prop `CustomEntityHandle Handle { get; set }`
- prop `double HiddenProductProgress { get; set }`
- prop `bool IsHungry { get; set }`
- prop `bool NeedsExcrementCleanup { get; set }`
- prop `CustomEntityGridPosition Position { get; set }`
- prop `CustomEntityRuntimeStatus RuntimeStatus { get; set }`
- prop `string SpeciesId { get; set }`
- prop `IReadOnlyDictionary<string, string> State { get; set }`
- prop `string VariantId { get; set }`

### CustomAnimalItemOutput
- prop `double Chance { get; set }`
- prop `string ItemId { get; set }`
- prop `int MaxStack { get; set }`
- prop `int MinStack { get; set }`

### CustomAnimalLifecycleEventArgs : EventArgs
- prop `int Amount { get; set }`
- prop `CustomEntityLifecycleEventArgs Entity { get; set }`
- prop `string ItemId { get; set }`
- prop `CustomAnimalInstanceSnapshot Snapshot { get; set }`

### CustomAnimalLifeStageDefinition
- prop `CustomEntityLocalizedText DisplayName { get; set }`
- prop `Nullable<int> MaximumAgeDays { get; set }`
- prop `int MinimumAgeDays { get; set }`
- prop `CustomEntityAssetHandle Sprite { get; set }`
- prop `string StageId { get; set }`

### CustomAnimalProductRule
- prop `CustomEntityLocalizedText DisplayName { get; set }`
- prop `bool HiddenUntilReady { get; set }`
- prop `IReadOnlyList<CustomAnimalItemOutput> Outputs { get; set }`
- prop `string ProductId { get; set }`
- prop `double ProgressPerGameHour { get; set }`
- prop `double RequiredProgress { get; set }`
- prop `IReadOnlyList<string> RequiredStateTags { get; set }`

### CustomAnimalRegistrationResult : CustomEntityRegistrationResult

### CustomAnimalSpawnRequest
- prop `string InitialLifeStageId { get; set }`
- prop `IReadOnlyDictionary<string, string> InitialState { get; set }`
- prop `CustomEntityGridPosition Position { get; set }`
- prop `string SpeciesId { get; set }`
- prop `string VariantId { get; set }`

### CustomAnimalSpawnResult : CustomEntityRequestResult
- prop `CustomAnimalInstanceSnapshot Snapshot { get; set }`

### CustomAnimalSpeciesDefinition
- prop `CustomAnimalBreedingPolicy Breeding { get; set }`
- prop `CustomAnimalConsumptionPolicy Consumption { get; set }`
- prop `CustomEntityLocalizedText Description { get; set }`
- prop `CustomAnimalDietPolicy Diet { get; set }`
- prop `CustomEntityLocalizedText DisplayName { get; set }`
- prop `CustomAnimalExcrementPolicy Excrement { get; set }`
- prop `CustomAnimalHabitatPolicy Habitat { get; set }`
- prop `IReadOnlyList<CustomAnimalProductRule> HiddenProducts { get; set }`
- prop `CustomEntityAssetHandle Icon { get; set }`
- prop `IReadOnlyList<CustomAnimalLifeStageDefinition> LifeStages { get; set }`
- prop `CustomEntityPersistencePolicy Persistence { get; set }`
- prop `IReadOnlyList<CustomAnimalProductRule> ProduceRules { get; set }`
- prop `ICustomAnimalBehaviorProvider Provider { get; set }`
- prop `string SpeciesId { get; set }`
- prop `CustomEntityAssetHandle Sprite { get; set }`
- prop `CustomAnimalStats Stats { get; set }`
- prop `CustomEntityTickPolicy TickPolicy { get; set }`
- prop `IReadOnlyList<string> VariantIds { get; set }`

### CustomAnimalStats
- prop `IReadOnlyDictionary<string, double> CustomValues { get; set }`
- prop `int MaxFriendship { get; set }`
- prop `int MaxHealth { get; set }`
- prop `int MaxMood { get; set }`
- prop `double MoveSpeed { get; set }`

### CustomAttackBehaviorContext
- prop `CustomEntityBehaviorContext Entity { get; set }`
- prop `CustomAttackInstanceSnapshot Snapshot { get; set }`

### CustomAttackDefinition
- prop `string AttackId { get; set }`
- prop `CustomEntityAssetHandle Audio { get; set }`
- prop `int BounceCount { get; set }`
- prop `CustomDamagePayload Damage { get; set }`
- prop `IReadOnlyList<string> EffectTags { get; set }`
- prop `string FactionId { get; set }`
- prop `bool FriendlyFire { get; set }`
- prop `CustomHitboxDefinition Hitbox { get; set }`
- prop `bool Homing { get; set }`
- prop `double LifetimeSeconds { get; set }`
- prop `CustomBarragePatternDefinition Pattern { get; set }`
- prop `CustomEntityPersistencePolicy Persistence { get; set }`
- prop `int PierceCount { get; set }`
- prop `ICustomAttackBehaviorProvider Provider { get; set }`
- prop `CustomEntityRelationKind RelationToPlayer { get; set }`
- prop `CustomEntityTickPolicy TickPolicy { get; set }`
- prop `CustomTrajectoryDefinition Trajectory { get; set }`
- prop `CustomEntityAssetHandle Visual { get; set }`

### CustomAttackInstanceSnapshot
- prop `double AgeSeconds { get; set }`
- prop `string AttackId { get; set }`
- prop `CustomEntityHandle Handle { get; set }`
- prop `int HitCount { get; set }`
- prop `CustomEntityGridPosition Position { get; set }`
- prop `CustomEntityRuntimeStatus RuntimeStatus { get; set }`
- prop `CustomEntityHandle Source { get; set }`
- prop `IReadOnlyDictionary<string, string> State { get; set }`

### CustomAttackLifecycleEventArgs : EventArgs
- prop `int DamageAmount { get; set }`
- prop `CustomEntityLifecycleEventArgs Entity { get; set }`
- prop `CustomEntityHandle HitTarget { get; set }`
- prop `CustomAttackInstanceSnapshot Snapshot { get; set }`

### CustomAttackRegistrationResult : CustomEntityRegistrationResult

### CustomAttackSpawnRequest
- prop `string AttackId { get; set }`
- prop `CustomEntityVector2 Direction { get; set }`
- prop `IReadOnlyDictionary<string, string> InitialState { get; set }`
- prop `CustomEntityGridPosition Origin { get; set }`
- prop `CustomEntityHandle Source { get; set }`
- prop `CustomEntityHandle Target { get; set }`

### CustomAttackSpawnResult : CustomEntityRequestResult
- prop `CustomAttackInstanceSnapshot Snapshot { get; set }`

### CustomBarragePatternDefinition
- prop `double ArcDegrees { get; set }`
- prop `bool DeterministicRandomSeed { get; set }`
- prop `double IntervalSeconds { get; set }`
- prop `CustomAttackPatternKind Kind { get; set }`
- prop `int ProjectileCount { get; set }`
- prop `int RepeatCount { get; set }`

### CustomDamagePayload
- prop `int Amount { get; set }`
- prop `string DamageType { get; set }`
- prop `double Knockback { get; set }`
- prop `IReadOnlyDictionary<string, double> Scaling { get; set }`

### CustomDroneBehaviorContext
- prop `CustomEntityBehaviorContext Entity { get; set }`
- prop `CustomDroneInstanceSnapshot Snapshot { get; set }`

### CustomDroneCommandRequest
- prop `CustomEntityGridPosition Destination { get; set }`
- prop `CustomDroneBehaviorMode Mode { get; set }`
- prop `string Reason { get; set }`
- prop `CustomEntityHandle Target { get; set }`

### CustomDroneCommandResult : CustomEntityRequestResult
- prop `CustomDroneBehaviorMode Mode { get; set }`

### CustomDroneDefinition
- prop `IReadOnlyList<string> AttackIds { get; set }`
- prop `CustomEntityAssetHandle Audio { get; set }`
- prop `CustomEntityLocalizedText Description { get; set }`
- prop `CustomEntityLocalizedText DisplayName { get; set }`
- prop `string DroneId { get; set }`
- prop `CustomDroneEnergyPolicy Energy { get; set }`
- prop `IReadOnlyList<CustomDroneEquipmentSlotDefinition> EquipmentSlots { get; set }`
- prop `CustomEntityAssetHandle Icon { get; set }`
- prop `IReadOnlyList<CustomDroneEquipmentSlotDefinition> ModuleSlots { get; set }`
- prop `CustomDroneMovementPolicy Movement { get; set }`
- prop `CustomDroneOwnerBindingPolicy OwnerBinding { get; set }`
- prop `CustomEntityPersistencePolicy Persistence { get; set }`
- prop `ICustomDroneBehaviorProvider Provider { get; set }`
- prop `CustomDroneRepairPolicy Repair { get; set }`
- prop `CustomEntityAssetHandle Sprite { get; set }`
- prop `CustomDroneStats Stats { get; set }`
- prop `CustomDroneSummonPolicy Summon { get; set }`
- prop `IReadOnlyList<CustomDroneBehaviorMode> SupportedModes { get; set }`
- prop `CustomEntityTickPolicy TickPolicy { get; set }`
- prop `IReadOnlyList<string> VariantIds { get; set }`

### CustomDroneEnergyPolicy
- prop `IReadOnlyList<string> AcceptedFuelItemIds { get; set }`
- prop `int AttackEnergyCost { get; set }`
- prop `int EnergyPerSecond { get; set }`
- prop `int MaxEnergy { get; set }`

### CustomDroneEquipmentRequest
- prop `string ItemId { get; set }`
- prop `string SlotId { get; set }`
- prop `int Stack { get; set }`
- prop `bool Unequip { get; set }`

### CustomDroneEquipmentResult : CustomEntityRequestResult
- prop `string ItemId { get; set }`
- prop `string SlotId { get; set }`

### CustomDroneEquipmentSlotDefinition
- prop `IReadOnlyList<string> AllowedItemIds { get; set }`
- prop `IReadOnlyList<string> AllowedItemTags { get; set }`
- prop `CustomEntityLocalizedText DisplayName { get; set }`
- prop `bool Required { get; set }`
- prop `string SlotId { get; set }`

### CustomDroneInstanceSnapshot
- prop `string DroneId { get; set }`
- prop `int Energy { get; set }`
- prop `IReadOnlyDictionary<string, string> Equipment { get; set }`
- prop `CustomEntityHandle Handle { get; set }`
- prop `int Health { get; set }`
- prop `CustomDroneBehaviorMode Mode { get; set }`
- prop `CustomEntityGridPosition Position { get; set }`
- prop `CustomEntityRuntimeStatus RuntimeStatus { get; set }`
- prop `int Shield { get; set }`
- prop `IReadOnlyDictionary<string, string> State { get; set }`
- prop `string VariantId { get; set }`

### CustomDroneLifecycleEventArgs : EventArgs
- prop `int DamageAmount { get; set }`
- prop `CustomEntityLifecycleEventArgs Entity { get; set }`
- prop `string EquipmentSlotId { get; set }`
- prop `int RepairAmount { get; set }`
- prop `CustomDroneInstanceSnapshot Snapshot { get; set }`

### CustomDroneMovementPolicy
- prop `bool CollidesWithWorld { get; set }`
- prop `double FollowDistance { get; set }`
- prop `CustomEntityMovementKind Kind { get; set }`
- prop `int Layer { get; set }`
- prop `double PatrolRadius { get; set }`
- prop `bool ProviderCanOverride { get; set }`

### CustomDroneOwnerBindingPolicy
- prop `bool BindToOwnerMod { get; set }`
- prop `bool BindToPlayer { get; set }`
- prop `IReadOnlyList<string> PermissionTags { get; set }`

### CustomDroneRegistrationResult : CustomEntityRegistrationResult

### CustomDroneRepairPolicy
- prop `bool CanRepair { get; set }`
- prop `int RepairAmountPerItem { get; set }`
- prop `IReadOnlyList<string> RepairItemIds { get; set }`

### CustomDroneStats
- prop `int Armor { get; set }`
- prop `int ContactDamage { get; set }`
- prop `int MaxHealth { get; set }`
- prop `int MaxShield { get; set }`
- prop `double MoveSpeed { get; set }`
- prop `IReadOnlyDictionary<string, double> Resistances { get; set }`

### CustomDroneSummonPolicy
- prop `bool CanSummonAnywhere { get; set }`
- prop `double CooldownSeconds { get; set }`
- prop `int MaxActiveInstances { get; set }`

### CustomDroneSummonRequest
- prop `string DroneId { get; set }`
- prop `IReadOnlyDictionary<string, string> InitialState { get; set }`
- prop `CustomEntityGridPosition Position { get; set }`
- prop `string VariantId { get; set }`

### CustomDroneSummonResult : CustomEntityRequestResult
- prop `CustomDroneInstanceSnapshot Snapshot { get; set }`

### CustomEntityAssetHandle
- prop `string AssetId { get; set }`
- prop `string ContentType { get; set }`
- prop `string RelativePath { get; set }`
- prop `IReadOnlyList<string> Tags { get; set }`
- prop `string VariantId { get; set }`

### CustomEntityBehaviorContext
- prop `string DefinitionId { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `CustomEntityHandle Handle { get; set }`
- prop `string OwnerUniqueId { get; set }`
- prop `string RoomId { get; set }`
- prop `Nullable<int> SaveSlot { get; set }`
- prop `IReadOnlyDictionary<string, string> State { get; set }`
- prop `UInt64 UpdateTick { get; set }`

### CustomEntityBehaviorResult
- prop `string FailureReason { get; set }`
- prop `bool Succeeded { get; set }`
- prop `IReadOnlyDictionary<string, string> UpdatedState { get; set }`

### CustomEntityCapabilityStatus
- prop `int ActiveRuntimeInstanceCount { get; set }`
- prop `string Details { get; set }`
- prop `string FailureReason { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `IReadOnlyList<CustomEntityValidationMessage> Messages { get; set }`
- prop `int RegisteredDefinitionCount { get; set }`
- prop `string Status { get; set }`

### CustomEntityFamilySnapshot
- prop `int ActiveRuntimeInstanceCount { get; set }`
- prop `IReadOnlyList<string> DefinitionIds { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `string OwnerUniqueId { get; set }`
- prop `int RegisteredDefinitionCount { get; set }`
- prop `IReadOnlyList<CustomEntityHandle> RuntimeHandles { get; set }`
- prop `CustomEntityRuntimeStatus RuntimeStatus { get; set }`
- prop `int SaveStateRecordCount { get; set }`
- prop `string StatusDetails { get; set }`

### CustomEntityGridPosition
- prop `int Layer { get; set }`
- prop `string RoomId { get; set }`
- prop `int X { get; set }`
- prop `int Y { get; set }`

### CustomEntityHandle
- prop `string DefinitionId { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `bool IsEmpty { get }`
- prop `string OwnerUniqueId { get; set }`
- prop `string RuntimeId { get; set }`
- prop `int SaveSlot { get; set }`
- `string ToString()`

### CustomEntityLifecycleEventArgs : EventArgs
- prop `string DefinitionId { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `CustomEntityHandle Handle { get; set }`
- prop `CustomEntityLifecycleKind Kind { get; set }`
- prop `string OwnerUniqueId { get; set }`
- prop `string Reason { get; set }`
- prop `DateTimeOffset Time { get; set }`

### CustomEntityLocalizedText
- prop `string Default { get; set }`
- prop `string English { get; set }`
- prop `string SimplifiedChinese { get; set }`
- prop `IReadOnlyDictionary<string, string> Translations { get; set }`

### CustomEntityPersistencePolicy
- prop `CustomEntityPersistenceKind Kind { get; set }`
- prop `bool RemoveInstancesWhenOwnerMissing { get; set }`
- prop `bool RestoreRuntimeInstancesOnSaveLoad { get; set }`
- prop `IReadOnlyList<CustomEntitySaveDataKey> SaveKeys { get; set }`
- prop `int SchemaVersion { get; set }`

### CustomEntityRegistrationResult
- prop `string DefinitionId { get; set }`
- prop `string FailureReason { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `IReadOnlyList<CustomEntityValidationMessage> Messages { get; set }`
- prop `string OwnerUniqueId { get; set }`
- prop `CustomEntityRuntimeStatus RuntimeStatus { get; set }`
- prop `bool Succeeded { get; set }`

### CustomEntityRequestResult
- prop `string DefinitionId { get; set }`
- prop `string Details { get; set }`
- prop `string FailureReason { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `CustomEntityHandle Handle { get; set }`
- prop `IReadOnlyList<CustomEntityValidationMessage> Messages { get; set }`
- prop `string OwnerUniqueId { get; set }`
- prop `CustomEntityRuntimeStatus RuntimeStatus { get; set }`
- prop `bool Succeeded { get; set }`

### CustomEntitySaveDataKey
- prop `string Description { get; set }`
- prop `string Key { get; set }`
- prop `int Version { get; set }`

### CustomEntitySaveMigrationContext
- prop `string DefinitionId { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `int FromVersion { get; set }`
- prop `string OwnerUniqueId { get; set }`
- prop `IReadOnlyDictionary<string, string> SaveState { get; set }`
- prop `int ToVersion { get; set }`

### CustomEntityTickPolicy
- prop `bool DeterministicOrder { get; set }`
- prop `double IntervalSeconds { get; set }`
- prop `CustomEntityTickPolicyKind Kind { get; set }`
- prop `int Order { get; set }`

### CustomEntityUnregisterResult
- prop `string DefinitionId { get; set }`
- prop `string FailureReason { get; set }`
- prop `CustomEntityFamily Family { get; set }`
- prop `IReadOnlyList<CustomEntityValidationMessage> Messages { get; set }`
- prop `string OwnerUniqueId { get; set }`
- prop `int RemovedRuntimeInstanceCount { get; set }`
- prop `bool Succeeded { get; set }`

### CustomEntityValidationMessage
- prop `string Code { get; set }`
- prop `string Field { get; set }`
- prop `string Message { get; set }`
- prop `CustomEntityValidationSeverity Severity { get; set }`

### CustomEntityVector2
- prop `double X { get; set }`
- prop `double Y { get; set }`

### CustomHitboxDefinition
- prop `double Height { get; set }`
- prop `bool ProviderCanOverride { get; set }`
- prop `double Radius { get; set }`
- prop `CustomHitboxShapeKind Shape { get; set }`
- prop `double Width { get; set }`

### CustomMonsterAttackSlot
- prop `string AttackId { get; set }`
- prop `double CooldownSeconds { get; set }`
- prop `int Priority { get; set }`
- prop `double Range { get; set }`
- prop `string SlotId { get; set }`

### CustomMonsterBehaviorContext
- prop `CustomEntityBehaviorContext Entity { get; set }`
- prop `CustomMonsterInstanceSnapshot Snapshot { get; set }`

### CustomMonsterDefinition
- prop `IReadOnlyList<CustomMonsterAttackSlot> AttackSlots { get; set }`
- prop `CustomEntityAssetHandle Audio { get; set }`
- prop `CustomEntityLocalizedText Description { get; set }`
- prop `CustomEntityLocalizedText DisplayName { get; set }`
- prop `string FactionId { get; set }`
- prop `CustomEntityAssetHandle Icon { get; set }`
- prop `IReadOnlyList<CustomMonsterLootRule> Loot { get; set }`
- prop `int MaxCountPerRoom { get; set }`
- prop `string MonsterId { get; set }`
- prop `CustomMonsterMovementPolicy Movement { get; set }`
- prop `CustomEntityPersistencePolicy Persistence { get; set }`
- prop `ICustomMonsterBehaviorProvider Provider { get; set }`
- prop `CustomEntityRelationKind RelationToPlayer { get; set }`
- prop `IReadOnlyList<CustomMonsterSpawnRule> SpawnRules { get; set }`
- prop `CustomEntityAssetHandle Sprite { get; set }`
- prop `CustomMonsterStats Stats { get; set }`
- prop `CustomMonsterTargetPolicy Targeting { get; set }`
- prop `CustomEntityTickPolicy TickPolicy { get; set }`
- prop `IReadOnlyList<string> VariantIds { get; set }`

### CustomMonsterInstanceSnapshot
- prop `string CurrentAttackId { get; set }`
- prop `string CurrentTargetId { get; set }`
- prop `CustomEntityHandle Handle { get; set }`
- prop `int Health { get; set }`
- prop `string MonsterId { get; set }`
- prop `CustomEntityGridPosition Position { get; set }`
- prop `CustomEntityRuntimeStatus RuntimeStatus { get; set }`
- prop `IReadOnlyDictionary<string, string> State { get; set }`
- prop `string VariantId { get; set }`

### CustomMonsterLifecycleEventArgs : EventArgs
- prop `string AttackId { get; set }`
- prop `int DamageAmount { get; set }`
- prop `CustomEntityLifecycleEventArgs Entity { get; set }`
- prop `CustomMonsterInstanceSnapshot Snapshot { get; set }`
- prop `string TargetId { get; set }`

### CustomMonsterLootRule
- prop `double Chance { get; set }`
- prop `string ItemId { get; set }`
- prop `int MaxStack { get; set }`
- prop `int MinStack { get; set }`

### CustomMonsterMovementPolicy
- prop `CustomEntityMovementKind Kind { get; set }`
- prop `double PatrolRadius { get; set }`
- prop `double PreferredDistance { get; set }`
- prop `bool ProviderCanOverride { get; set }`

### CustomMonsterRegistrationResult : CustomEntityRegistrationResult

### CustomMonsterSpawnRequest
- prop `IReadOnlyDictionary<string, string> InitialState { get; set }`
- prop `string MonsterId { get; set }`
- prop `CustomEntityGridPosition Position { get; set }`
- prop `string VariantId { get; set }`

### CustomMonsterSpawnResult : CustomEntityRequestResult
- prop `CustomMonsterInstanceSnapshot Snapshot { get; set }`

### CustomMonsterSpawnRule
- prop `IReadOnlyList<string> BiomeTags { get; set }`
- prop `Nullable<int> EarliestHour { get; set }`
- prop `Nullable<int> LatestHour { get; set }`
- prop `int MaxGroupSize { get; set }`
- prop `int MinGroupSize { get; set }`
- prop `double Probability { get; set }`
- prop `IReadOnlyList<string> RoomIds { get; set }`
- prop `IReadOnlyList<string> RoomTags { get; set }`
- prop `string RuleId { get; set }`
- prop `IReadOnlyList<string> Seasons { get; set }`
- prop `IReadOnlyList<string> WeatherIds { get; set }`

### CustomMonsterSpawnTableDefinition
- prop `IReadOnlyList<string> MonsterIds { get; set }`
- prop `IReadOnlyList<CustomMonsterSpawnRule> Rules { get; set }`
- prop `string SpawnTableId { get; set }`

### CustomMonsterStats
- prop `int Armor { get; set }`
- prop `int ContactDamage { get; set }`
- prop `IReadOnlyDictionary<string, double> CustomValues { get; set }`
- prop `int MaxHealth { get; set }`
- prop `double MoveSpeed { get; set }`
- prop `IReadOnlyDictionary<string, double> Resistances { get; set }`

### CustomMonsterTargetPolicy
- prop `double AggroRange { get; set }`
- prop `bool ProviderCanOverride { get; set }`
- prop `bool RetargetWhenDamaged { get; set }`
- prop `IReadOnlyList<string> TargetTags { get; set }`

### CustomTrajectoryDefinition
- prop `double Acceleration { get; set }`
- prop `CustomEntityMovementKind Kind { get; set }`
- prop `bool ProviderCanOverride { get; set }`
- prop `double Speed { get; set }`
- prop `double TurnRateDegreesPerSecond { get; set }`

### DebugCommandResult
- prop `int AffectedCount { get; set }`
- prop `string CommandId { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `bool Success { get; set }`

### DebugValueResult
- prop `int AfterValue { get; set }`
- prop `int BeforeValue { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `int RequestedDelta { get; set }`
- prop `bool Success { get; set }`
- prop `string ValueId { get; set }`

### DtmApiDispositionAttribute : Attribute
- prop `DtmApiDisposition Disposition { get }`
- prop `string Notes { get; set }`
- prop `string Since { get; set }`

### DtmApiStatusAttribute : Attribute
- prop `string Notes { get; set }`
- prop `string Since { get; set }`
- prop `DtmApiStatus Status { get }`

### DtmButton
- prop `string Id { get }`
- prop `bool IsBound { get }`
- prop `DtmButton None { get }`
- `bool Equals(DtmButton other)`
- `bool Equals(object obj)`
- `int GetHashCode()`
- `string Normalize(string value)`
- `DtmButton Parse(string value)`
- `string ToString()`

### DtmButtonState
- prop `DtmButton Button { get }`
- prop `bool IsDown { get }`
- prop `bool WasPressed { get }`
- prop `bool WasReleased { get }`

### DtmColor
- prop `double A { get }`
- prop `double B { get }`
- prop `double G { get }`
- prop `double R { get }`

### DtmColorPreset
- prop `string HexColor { get }`
- prop `string Id { get }`
- prop `string Label { get }`

### DtmKeybind
- prop `IReadOnlyList<DtmButton> Buttons { get }`
- prop `bool IsBound { get }`
- `bool ContainsButton(string button)`
- `bool Equals(DtmKeybind other)`
- `bool Equals(object obj)`
- `int GetHashCode()`
- `bool IsDown(Func<string, bool> isDown)`
- `bool IsPressed(Func<string, bool> wasPressed, Func<string, bool> isDown)`
- `bool IsPressed(Func<string, bool> wasPressed, Func<string, bool> isDown, Func<string, bool> wasReleased)`
- `bool IsReleased(Func<string, bool> wasReleased, Func<string, bool> isDown, Func<string, bool> wasPressed)`
- `string ToString()`

### DtmKeybindList
- prop `bool IsBound { get }`
- prop `IReadOnlyList<DtmKeybind> Keybinds { get }`
- prop `DtmKeybindList None { get }`
- `bool ContainsButton(string button)`
- `bool Equals(DtmKeybindList other)`
- `bool Equals(object obj)`
- `IReadOnlyList<string> GetButtonIds()`
- `int GetHashCode()`
- `bool IsDown(Func<string, bool> isDown)`
- `bool IsDown(IInputHelper input)`
- `bool IsPressed(Func<string, bool> wasPressed, Func<string, bool> isDown)`
- `bool IsPressed(Func<string, bool> wasPressed, Func<string, bool> isDown, Func<string, bool> wasReleased)`
- `bool IsReleased(Func<string, bool> wasReleased, Func<string, bool> isDown, Func<string, bool> wasPressed)`
- `bool JustPressed(IInputHelper input)`
- `bool JustReleased(IInputHelper input)`
- `DtmKeybindList Parse(string text)`
- `string ToString()`
- `bool TryParse(string text, DtmKeybindList& keybinds, String& error)`

### DtmMod
- prop `IManifest Manifest { get; set }`
- prop `IMonitor Monitor { get; set }`
- `void AttachContext(IManifest manifest, IMonitor monitor)`
- `void Entry(IDtmHelper helper)`

### EquipmentSlotEquipResult
- prop `int AfterBackpackCount { get; set }`
- prop `int BeforeBackpackCount { get; set }`
- prop `string DisplayName { get; set }`
- prop `string FailureReason { get; set }`
- prop `string ItemId { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `int RecoveredCount { get; set }`
- prop `string SlotId { get; set }`
- prop `bool Success { get; set }`

### EquipmentSlotInfo
- prop `bool AffectsVisuals { get; set }`
- prop `bool AttributeOnly { get; set }`
- prop `string DisplayName { get; set }`
- prop `int Index { get; set }`
- prop `bool IsApplied { get; set }`
- prop `bool IsOccupied { get; set }`
- prop `bool IsRecoverable { get; set }`
- prop `string ItemId { get; set }`
- prop `string LastMessage { get; set }`
- prop `string OwnerId { get; set }`
- prop `string SlotId { get; set }`

### EquipmentSlotsOptions
- prop `bool AutoRecoverOnMissingMod { get; set }`
- prop `bool Enabled { get; set }`
- prop `int ExtraAttributeSlots { get; set }`
- prop `bool ExtraSlotsAffectVisuals { get; set }`
- prop `bool PreserveVanillaVisualSlots { get; set }`
- prop `bool SafeUnequipOnDisable { get; set }`
- prop `string SlotIdPrefix { get; set }`
- prop `bool VerboseLogging { get; set }`

### EquipmentSlotsRecoveryResult
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `int RecoveredCount { get; set }`
- prop `bool Success { get; set }`

### EquipmentSlotsRegisterResult
- prop `int ExtraAttributeSlots { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `bool Success { get; set }`

### EquipmentSlotsState
- prop `int AppliedItemCount { get; set }`
- prop `int ExtraAttributeSlots { get; set }`
- prop `bool ExtraSlotsAffectVisuals { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `string LastEquippedItemId { get; set }`
- prop `string LastEquippedSlotId { get; set }`
- prop `string LastRecoveryMessage { get; set }`
- prop `string OwnerId { get; set }`
- prop `int PendingRecoveryCount { get; set }`
- prop `bool PreserveVanillaVisualSlots { get; set }`
- prop `bool RuntimeStatsHookInstalled { get; set }`
- prop `bool RuntimeUiHookInstalled { get; set }`
- prop `bool SafeUnequipOnDisable { get; set }`
- prop `int StatsRefreshCount { get; set }`
- prop `string Status { get; set }`
- prop `int StoredItemCount { get; set }`

### FishingAutomationOptions
- prop `FishingAnimationMode AnimationMode { get; set }`
- prop `double AnimationMultiplier { get; set }`
- prop `FishingBiteWaitMode BiteWaitMode { get; set }`
- prop `double CastChargeRatio { get; set }`
- prop `double RecastDelaySeconds { get; set }`
- prop `FishingResultMode ResultMode { get; set }`
- prop `bool StopOnManualMove { get; set }`
- prop `bool VerboseLogging { get; set }`

### FishingAutomationState
- prop `bool Enabled { get; set }`
- prop `string LastNativeAction { get; set }`
- prop `string LastReason { get; set }`
- prop `string LastResult { get; set }`
- prop `string NativeOwner { get; set }`
- prop `string Phase { get; set }`

### FishRoeDisplayInfo
- prop `string FishId { get; set }`
- prop `string FishTitle { get; set }`
- prop `string GrowText { get; set }`
- prop `string IncubateText { get; set }`
- prop `string ParentSummary { get; set }`
- prop `string RoeTitle { get; set }`

### FishRoeTooltipOptions
- prop `int CacheSeconds { get; set }`
- prop `bool Enabled { get; set }`
- prop `bool LabelFishRoeDetails { get; set }`
- prop `bool LabelFishRoeTitle { get; set }`
- prop `bool VerboseLogging { get; set }`

### GameLaunchedEventArgs : EventArgs

### HookStatusChangedEventArgs : EventArgs
- prop `string HookId { get }`
- prop `string Status { get }`

### InstantSaveDebugResult
- prop `InstantSaveDebugState AfterReloadRequest { get; set }`
- prop `InstantSaveDebugState AfterSave { get; set }`
- prop `InstantSaveDebugState Before { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `bool ReloadAfterSave { get; set }`
- prop `bool ReloadRequested { get; set }`
- prop `Nullable<int> SaveSlot { get; set }`
- prop `bool Success { get; set }`

### InstantSaveDebugState
- prop `bool CanSave { get; set }`
- prop `TeleportSnapshot CurrentLocation { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `Nullable<int> SaveSlot { get; set }`

### InventoryDebugItem
- prop `bool CanGive { get; set }`
- prop `string CannotGiveReason { get; set }`
- prop `bool CanSpawn { get; set }`
- prop `string Category { get; set }`
- prop `string ChineseName { get; set }`
- prop `string ContentPath { get; set }`
- prop `string DisplayName { get; set }`
- prop `string EnglishName { get; set }`
- prop `bool HasIcon { get; set }`
- prop `string IconAssetKey { get; set }`
- prop `string IconPath { get; set }`
- prop `string Id { get; set }`
- prop `bool IsModItem { get; set }`
- prop `int LoadOrder { get; set }`
- prop `int MaxStack { get; set }`
- prop `string RootPath { get; set }`
- prop `bool RuntimeLoaded { get; set }`
- prop `int RuntimeOrder { get; set }`
- prop `string SearchText { get; set }`
- prop `bool SourceEnabled { get; set }`
- prop `bool SourceEnablementKnown { get; set }`
- prop `string SourceId { get; set }`
- prop `string SourceKind { get; set }`
- prop `string SourceModTitle { get; set }`
- prop `string SubCategory { get; set }`
- prop `IReadOnlyList<string> Tags { get; set }`
- prop `Nullable<UInt64> WorkshopId { get; set }`

### InventoryDebugPage
- prop `IReadOnlyList<string> Categories { get; set }`
- prop `IReadOnlyList<InventoryDebugItem> Items { get; set }`
- prop `int Page { get; set }`
- prop `int PageSize { get; set }`
- prop `IReadOnlyList<InventoryDebugSourceGroup> Sources { get; set }`
- prop `string Status { get; set }`
- prop `int TotalItems { get; set }`
- prop `int TotalPages { get; set }`

### InventoryDebugQuery
- prop `string Category { get; set }`
- prop `bool IncludeUnavailable { get; set }`
- prop `bool ModItemsOnly { get; set }`
- prop `int Page { get; set }`
- prop `int PageSize { get; set }`
- prop `string SearchText { get; set }`
- prop `string SourceId { get; set }`

### InventoryDebugSourceGroup
- prop `int Count { get; set }`
- prop `string DisplayName { get; set }`
- prop `bool Enabled { get; set }`
- prop `bool EnablementKnown { get; set }`
- prop `string Id { get; set }`
- prop `bool IsModSource { get; set }`
- prop `string SourceKind { get; set }`
- prop `Nullable<UInt64> WorkshopId { get; set }`

### InventoryGiveResult
- prop `int AfterCount { get; set }`
- prop `int BeforeCount { get; set }`
- prop `string DisplayName { get; set }`
- prop `string FailureReason { get; set }`
- prop `int GivenCount { get; set }`
- prop `string ItemId { get; set }`
- prop `string Message { get; set }`
- prop `int RequestedCount { get; set }`
- prop `bool Success { get; set }`

### KeybindPressedEventArgs : EventArgs
- prop `string KeybindId { get }`
- prop `DtmKeybindList Keybinds { get }`
- prop `string OwnerId { get }`
- prop `string TriggerButton { get }`

### KeybindReleasedEventArgs : EventArgs
- prop `string KeybindId { get }`
- prop `DtmKeybindList Keybinds { get }`
- prop `string OwnerId { get }`
- prop `string TriggerButton { get }`

### LampManualToggleOptions
- prop `bool Enabled { get; set }`
- prop `IReadOnlyList<string> EquipmentIds { get; set }`
- prop `bool VerboseLogging { get; set }`

### LampManualToggleRegisterResult
- prop `bool Enabled { get; set }`
- prop `string FailureReason { get; set }`
- prop `bool HookInstalled { get; set }`
- prop `string LastToggledEquipmentId { get; set }`
- prop `string LastTouchedEquipmentId { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `IReadOnlyList<string> RegisteredEquipmentIds { get; set }`
- prop `int SessionOverrideCount { get; set }`
- prop `bool Success { get; set }`

### LampManualToggleState
- prop `bool Enabled { get; set }`
- prop `string FailureReason { get; set }`
- prop `bool HookInstalled { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `string LastMessage { get; set }`
- prop `string LastToggledEquipmentId { get; set }`
- prop `bool LastToggledValue { get; set }`
- prop `string LastTouchedEquipmentId { get; set }`
- prop `string OwnerId { get; set }`
- prop `IReadOnlyList<string> RegisteredEquipmentIds { get; set }`
- prop `int SessionOverrideCount { get; set }`
- prop `string Status { get; set }`

### LogExportedEventArgs : EventArgs
- prop `string ReportPath { get }`

### MachineDefinition
- prop `bool AllowElectricMode { get; set }`
- prop `bool AllowFuelMode { get; set }`
- prop `int CycleMinutes { get; set }`
- prop `string DefaultMode { get; set }`
- prop `string DisplayName { get; set }`
- prop `int ElectricModeFuelCostPerCycle { get; set }`
- prop `int ElectricModePowerCostPerCycle { get; set }`
- prop `string EquipmentId { get; set }`
- prop `int FuelCapacity { get; set }`
- prop `int FuelOnlyFuelCostPerCycle { get; set }`
- prop `bool IncludeRuntimeModMinerals { get; set }`
- prop `string ItemId { get; set }`
- prop `string MachineId { get; set }`
- prop `string NativeTechNodeAboveTitleContains { get; set }`
- prop `string NativeTechNodeDescription { get; set }`
- prop `string NativeTechNodeId { get; set }`
- prop `string NativeTechNodeParentId { get; set }`
- prop `string NativeTechNodeTitle { get; set }`
- prop `string NativeTechTreeId { get; set }`
- prop `IReadOnlyList<MachineOutputRule> OutputRules { get; set }`
- prop `IReadOnlyDictionary<string, double> ProbabilityOverrides { get; set }`
- prop `string RecipeGroupId { get; set }`
- prop `string RecipeId { get; set }`
- prop `IReadOnlyList<MachineRecipeInput> RecipeInputs { get; set }`
- prop `bool VerboseLogging { get; set }`
- prop `double VisualScale { get; set }`

### MachineOutputRule
- prop `bool AllowProbabilityOverride { get; set }`
- prop `string DisplayName { get; set }`
- prop `string ItemId { get; set }`
- prop `int MaxCount { get; set }`
- prop `int MinCount { get; set }`
- prop `string Source { get; set }`
- prop `double Weight { get; set }`

### MachineProductionState
- prop `bool AllowElectricMode { get; set }`
- prop `bool AllowFuelMode { get; set }`
- prop `int CycleMinutes { get; set }`
- prop `int CycleTUs { get; set }`
- prop `string DefaultMode { get; set }`
- prop `string DisplayName { get; set }`
- prop `int ElectricModeFuelCostPerCycle { get; set }`
- prop `int ElectricModePowerCostPerCycle { get; set }`
- prop `string EquipmentId { get; set }`
- prop `int FuelCapacity { get; set }`
- prop `int FuelOnlyFuelCostPerCycle { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `string ItemId { get; set }`
- prop `int LastElectricPowerCost { get; set }`
- prop `int LastFuelCost { get; set }`
- prop `string LastMachineKey { get; set }`
- prop `string LastMessage { get; set }`
- prop `string LastMode { get; set }`
- prop `int LastObservedTotalTUs { get; set }`
- prop `int LastOutputCount { get; set }`
- prop `string LastOutputDisplayName { get; set }`
- prop `string LastOutputItemId { get; set }`
- prop `string LastOutputTarget { get; set }`
- prop `int LastStorageCapacity { get; set }`
- prop `int LastStorageFilledSlots { get; set }`
- prop `int LastStorageLineCapacity { get; set }`
- prop `string MachineId { get; set }`
- prop `string NativeTechTreeSummary { get; set }`
- prop `int NextDueTotalTUs { get; set }`
- prop `string OwnerId { get; set }`
- prop `int PlacedMachineCount { get; set }`
- prop `int ProductionCycleCount { get; set }`
- prop `string RecipeGroupId { get; set }`
- prop `string RecipeId { get; set }`
- prop `int RegisteredMachineCount { get; set }`
- prop `int RemainingFuel { get; set }`
- prop `bool RuntimeHookInstalled { get; set }`
- prop `string Status { get; set }`
- prop `double VisualScale { get; set }`

### MachineRecipeInput
- prop `int Count { get; set }`
- prop `string ItemId { get; set }`

### MachineRegisterResult
- prop `MachineDefinition Definition { get; set }`
- prop `string FailureReason { get; set }`
- prop `string MachineId { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `bool Success { get; set }`

### MailItemDeliveryRequest
- prop `string Content { get; set }`
- prop `int Count { get; set }`
- prop `string EmailName { get; set }`
- prop `string ItemId { get; set }`
- prop `bool PreventDuplicatePendingMail { get; set }`
- prop `string RequiredSourceId { get; set }`
- prop `bool RequireEnabledContentSource { get; set }`
- prop `string Sender { get; set }`
- prop `bool SkipIfAlreadyOwned { get; set }`
- prop `string TemplateName { get; set }`

### MailItemDeliveryResult
- prop `int BackpackCount { get; set }`
- prop `string DisplayName { get; set }`
- prop `string EmailName { get; set }`
- prop `string FailureReason { get; set }`
- prop `string ItemId { get; set }`
- prop `string Message { get; set }`
- prop `int PendingMailCount { get; set }`
- prop `int RequestedCount { get; set }`
- prop `bool Sent { get; set }`
- prop `bool Skipped { get; set }`
- prop `bool SourceEnabled { get; set }`
- prop `bool SourceEnablementKnown { get; set }`
- prop `string SourceId { get; set }`
- prop `bool Success { get; set }`
- prop `string TemplateName { get; set }`

### MenuClosedEventArgs : EventArgs
- prop `string MenuId { get }`

### MenuOpenedEventArgs : EventArgs
- prop `string MenuId { get }`

### MovementDebugState
- prop `bool IsDefault { get; set }`
- prop `double MoveSpeed { get; set }`
- prop `double Multiplier { get; set }`
- prop `string Source { get; set }`

### MovementSpeedResult
- prop `MovementDebugState After { get; set }`
- prop `double AppliedMultiplier { get; set }`
- prop `MovementDebugState Before { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `double RequestedMultiplier { get; set }`
- prop `bool Success { get; set }`

### NullMonitor
- `void Log(string message, LogLevel level)`
- `void LogException(Exception exception, string message)`
- `void LogOnce(string key, string message, LogLevel level)`

### OneSecondUpdateTickedEventArgs : EventArgs
- prop `UInt32 Second { get }`

### ReturnedToTitleEventArgs : EventArgs

### SaveLoadedEventArgs : EventArgs
- prop `bool IsNewGame { get }`
- prop `Nullable<int> SaveSlot { get }`

### SaveSavedEventArgs : EventArgs
- prop `Nullable<int> SaveSlot { get }`

### SaveSavingEventArgs : EventArgs
- prop `Nullable<int> SaveSlot { get }`

### SaveSlotsOptions
- prop `bool Enabled { get; set }`
- prop `int SlotCount { get; set }`
- prop `bool VerboseLogging { get; set }`

### SaveSlotsRegisterResult
- prop `int AppliedSlotCount { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `int PreviousSlotCount { get; set }`
- prop `int RequestedSlotCount { get; set }`
- prop `bool Success { get; set }`

### SaveSlotsState
- prop `int AppliedSlotCount { get; set }`
- prop `bool Enabled { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `string LastMessage { get; set }`
- prop `int NativeSlotCount { get; set }`
- prop `string OwnerId { get; set }`
- prop `int RequestedSlotCount { get; set }`
- prop `string Status { get; set }`

### SpawnDebugOption
- prop `string Category { get; set }`
- prop `string DisplayName { get; set }`
- prop `string Id { get; set }`
- prop `bool IsAvailableInCurrentRoom { get; set }`

### SpawnDebugResult
- prop `string DisplayName { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `int RequestedCount { get; set }`
- prop `int SpawnedCount { get; set }`
- prop `string SpawnId { get; set }`
- prop `bool Success { get; set }`

### StrongPlantingGunOptions
- prop `bool Enabled { get; set }`
- prop `bool IncludeFertilizers { get; set }`
- prop `bool IncludeFilms { get; set }`
- prop `bool IncludeSeeds { get; set }`
- prop `bool IncludeWater { get; set }`
- prop `int SlotCount { get; set }`
- prop `bool VerboseLogging { get; set }`

### StrongPlantingGunRegisterResult
- prop `bool Enabled { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `string OwnerId { get; set }`
- prop `int SlotCount { get; set }`
- prop `bool Success { get; set }`
- prop `bool ToolHookInstalled { get; set }`
- prop `bool UiHookInstalled { get; set }`

### StrongPlantingGunState
- prop `bool Enabled { get; set }`
- prop `int ExpandedGunCount { get; set }`
- prop `bool IsConfigured { get; set }`
- prop `int LastConsumedItemCount { get; set }`
- prop `int LastFertilizerActions { get; set }`
- prop `int LastFilmActions { get; set }`
- prop `string LastMessage { get; set }`
- prop `int LastSeedActions { get; set }`
- prop `int LastVisitedEquipmentCount { get; set }`
- prop `int LastWaterActions { get; set }`
- prop `string OwnerId { get; set }`
- prop `int SlotCount { get; set }`
- prop `string Status { get; set }`
- prop `bool ToolHookInstalled { get; set }`
- prop `bool UiHookInstalled { get; set }`

### TechPointDebugOption
- prop `int CurrentLevel { get; set }`
- prop `int CurrentPoints { get; set }`
- prop `string DisplayName { get; set }`
- prop `string Id { get; set }`

### TeleportCsvExportResult
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `string Path { get; set }`
- prop `int RowCount { get; set }`
- prop `bool Success { get; set }`

### TeleportDestination
- prop `string DisplayName { get; set }`
- prop `string Group { get; set }`
- prop `string Id { get; set }`
- prop `bool IsStation { get; set }`
- prop `bool IsUnlocked { get; set }`
- prop `string MarkPointId { get; set }`
- prop `string RoomId { get; set }`
- prop `string Source { get; set }`
- prop `string SuggestedDisplayName { get; set }`
- prop `double X { get; set }`
- prop `double Y { get; set }`

### TeleportResult
- prop `TeleportSnapshot AfterRequest { get; set }`
- prop `TeleportSnapshot Before { get; set }`
- prop `string DestinationId { get; set }`
- prop `string DestinationName { get; set }`
- prop `string FailureReason { get; set }`
- prop `string MarkPointId { get; set }`
- prop `string Message { get; set }`
- prop `bool Success { get; set }`

### TeleportSnapshot
- prop `string RoomId { get; set }`
- prop `string RoomTitle { get; set }`
- prop `string RoomType { get; set }`
- prop `double X { get; set }`
- prop `double Y { get; set }`
- prop `double Z { get; set }`

### TimeDebugState
- prop `string CurrentWeatherId { get; set }`
- prop `string CurrentWeatherName { get; set }`
- prop `int Day { get; set }`
- prop `int Hour { get; set }`
- prop `int Minute { get; set }`
- prop `int Month { get; set }`
- prop `string Period { get; set }`
- prop `string SeasonName { get; set }`
- prop `int Year { get; set }`

### TimeScaleDebugResult
- prop `double AfterMultiplier { get; set }`
- prop `double BeforeMultiplier { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `double RequestedMultiplier { get; set }`
- prop `bool Success { get; set }`

### TimeSkipResult
- prop `int AdvancedGameMinutes { get; set }`
- prop `int AdvancedSeconds { get; set }`
- prop `TimeDebugState After { get; set }`
- prop `TimeDebugState Before { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `bool Success { get; set }`
- prop `int TargetHour { get; set }`

### UpdateTickedEventArgs : EventArgs
- prop `UInt64 Tick { get }`

### WeatherDebugOption
- prop `string Description { get; set }`
- prop `string DisplayName { get; set }`
- prop `string Id { get; set }`
- prop `bool IsCurrent { get; set }`
- prop `bool IsCurrentDayForecast { get; set }`
- prop `bool IsMalignant { get; set }`
- prop `bool IsRainy { get; set }`
- prop `bool IsWindy { get; set }`
- prop `double Sun { get; set }`
- prop `double Water { get; set }`
- prop `double Wind { get; set }`

### WeatherDebugState
- prop `IReadOnlyList<string> CurrentDayForecastWeatherIds { get; set }`
- prop `string CurrentWeatherId { get; set }`
- prop `string CurrentWeatherName { get; set }`
- prop `int Day { get; set }`
- prop `int Hour { get; set }`
- prop `int Month { get; set }`
- prop `string SeasonName { get; set }`
- prop `int Year { get; set }`

### WeatherSetResult
- prop `string AfterWeatherId { get; set }`
- prop `string BeforeWeatherId { get; set }`
- prop `string DisplayName { get; set }`
- prop `string FailureReason { get; set }`
- prop `string Message { get; set }`
- prop `bool PatchedCurrentPeriod { get; set }`
- prop `bool Success { get; set }`
- prop `string WeatherId { get; set }`

### WorkshopModListChangedEventArgs : EventArgs
- prop `int ModCount { get }`

## 枚举 (Enums)

- **AdvancedTimeAdvanceKind**: Day, Week, Month
- **CropHarvestScope**: CurrentFarmAndFarmRooms
- **CropHarvestTargetKind**: Unknown, OrdinaryCrop, Vine, MushroomBag, Bush, TreeBasinCrop, GrassForageBasin
- **CropHarvestTargetStatus**: Pending, Harvested, NotMature, AlreadyHarvested, UnsupportedBasinType, NativeHarvestFailed, SkippedByRequestFilter, Busy
- **CustomAttackPatternKind**: Instant, Projectile, Beam, Burst, Ring, Cone, Barrage, ProviderControlled
- **CustomDroneBehaviorMode**: Idle, Follow, Guard, Patrol, Return, Attack, Support, ProviderControlled
- **CustomEntityFamily**: Animal, Monster, Attack, Drone
- **CustomEntityLifecycleKind**: Registered, Unregistered, SaveLoaded, SaveSaving, SaveSaved, ReturnedToTitle, SpawnRequested, Spawned, RemoveRequested, Removed, Tick, Damaged, Died, Expired, Failed
- **CustomEntityMovementKind**: None, Stationary, Wander, FollowTarget, Patrol, Flee, ProviderControlled
- **CustomEntityPersistenceKind**: RuntimeOnly, SaveScoped, SaveAndRespawn, DefinitionOnly
- **CustomEntityRelationKind**: Neutral, PlayerAlly, PlayerHostile, OwnerAlly, ProviderControlled
- **CustomEntityRuntimeStatus**: Unknown, Registered, ConfiguredNoRuntimeInstance, RuntimeCreationBlocked, Active, Removing, Removed, Failed
- **CustomEntityTickPolicyKind**: Disabled, OnGameUpdate, FixedInterval, OneSecond, SaveBoundaryOnly
- **CustomEntityValidationSeverity**: Info, Warning, Error
- **CustomHitboxShapeKind**: Point, Circle, Rectangle, Capsule, ProviderControlled
- **DtmApiDisposition**: Open, Frozen, Diagnostic, Internal, Disabled
- **DtmApiStatus**: Proposed, Experimental, Verified, Stable, Disabled, StableCandidate
- **DtmInputScope**: Always, Title, SaveLoaded, Gameplay
- **FishingAnimationMode**: Normal, FastCastPull
- **FishingBiteWaitMode**: NativeWait, InstantNativeBite
- **FishingResultMode**: AutoCompleteVisibleMiniGame, SkipMiniGameNativeResult
- **LogLevel**: Trace, Debug, Info, Warn, Error, Alert
