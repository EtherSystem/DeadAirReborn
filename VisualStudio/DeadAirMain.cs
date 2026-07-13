using DeadAirReborn;

[assembly: MelonInfo(typeof(DeadAirMain), "DeadAirReborn", "1.2.0", "EtherSystem", null)]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace DeadAirReborn
{
    internal sealed class DeadAirMain : MelonMod
    {
        public static bool isLoaded;

        private static bool addedCustomComponents;
        private static float nextRuntimeRefresh;
        private static float starterGearCheckAllowedAt;
        private static string lastAppliedScene = string.Empty;

        private const string StarterGearSuffix = "starterkit";
        private static readonly ModDataManager SaveData = new("DeadAirReborn", false);
        private static bool starterGearCheckedForCurrentSave;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg(System.ConsoleColor.Yellow, "I've heard tales of singing pipes");
            MelonLogger.Msg(System.ConsoleColor.Yellow, "Maybe breathing these fumes ain't the best Bourbon");
            MelonLogger.Msg(System.ConsoleColor.Green, "Canister Refill Loaded!");
            Settings.instance.AddToModSettings("Dead Air Reborn");
        }

        public override void OnSceneWasInitialized(int level, string name)
        {
            isLoaded = DeadAirUtils.IsScenePlayable(name);
            nextRuntimeRefresh = 0f;
            lastAppliedScene = string.Empty;
            starterGearCheckAllowedAt = Time.realtimeSinceStartup + 3f;

            if (!isLoaded)
            {
                ResetStarterGearRuntimeCheck();
                return;
            }

            DoStuffWithGear();

            if (!Settings.instance.useMod)
            {
                DeadAirUtils.ResetMaskDrainTracking();
                AirPollutionStop();
                return;
            }

            ApplyDeadAirRuntime(name, true);
        }

        public override void OnUpdate()
        {
            if (!isLoaded || !Settings.instance.useMod) return;
            if (GameManager.GetPlayerManagerComponent() == null) return;
            if (Time.realtimeSinceStartup < nextRuntimeRefresh) return;

            nextRuntimeRefresh = Time.realtimeSinceStartup + 1f;
            ApplyDeadAirRuntime(GameManager.m_ActiveScene, false);
            DeadAirUtils.RefreshPickedUpCanisterDurations();
            DeadAirUtils.UpdateMaskConsumptionMode();
        }

        internal static void ResetStarterGearRuntimeCheck()
        {
            starterGearCheckedForCurrentSave = false;
        }

        private static void ApplyDeadAirRuntime(string sceneName, bool forceLog)
        {
            if (!DeadAirUtils.IsScenePlayable(sceneName)) return;

            DoStuffWithGear();

            if (Time.realtimeSinceStartup >= starterGearCheckAllowedAt)
            {
                EnsureStarterGearForCurrentSave();
            }

            if (DeadAirUtils.IsSceneSafe(sceneName))
            {
                AirPollutionStop();
                LogAppliedProfile("Safe", sceneName, forceLog);
            }
            else if (DeadAirUtils.IsSceneHouse(sceneName))
            {
                AirPollutionInside();
                LogAppliedProfile("Inside", sceneName, forceLog);
            }
            else if (DeadAirUtils.IsSceneCinderHills(sceneName))
            {
                AirPollutionCinderHills();
                LogAppliedProfile("CinderHills", sceneName, forceLog);
            }
            else
            {
                AirPollutionOutside();
                LogAppliedProfile("Outside", sceneName, forceLog);
            }
        }

        private static void LogAppliedProfile(string profile, string sceneName, bool forceLog)
        {
            bool sceneChanged = !string.Equals(lastAppliedScene, sceneName, StringComparison.OrdinalIgnoreCase);
            if (forceLog || sceneChanged)
            {
                MelonLogger.Msg($"Applied pollution profile '{profile}' for scene '{sceneName}'.");
            }

            lastAppliedScene = sceneName;
        }

        private static void AirPollutionOutside()
        {
            DeadAirUtils.ConfigureInventoryRespirator(true, 600f);
            DeadAirUtils.SetInventoryCanisterDuration(DeadAirUtils.NormalCanisterGearName, 600f);
            DeadAirUtils.EnsureInventoryImprovisedFilter(300f);
            DeadAirUtils.ApplyChemicalPoisoning(true, 1, 0.05f, 300f, 2.5f);
        }

        private static void AirPollutionInside()
        {
            DeadAirUtils.ConfigureInventoryRespirator(true, 1200f);
            DeadAirUtils.SetInventoryCanisterDuration(DeadAirUtils.NormalCanisterGearName, 1200f);
            DeadAirUtils.EnsureInventoryImprovisedFilter(600f);
            DeadAirUtils.ApplyChemicalPoisoning(true, 1, 0.01f, 120f, 15f);
        }

        private static void AirPollutionCinderHills()
        {
            DeadAirUtils.ConfigureInventoryRespirator(true, 75f);
            DeadAirUtils.SetInventoryCanisterDuration(DeadAirUtils.NormalCanisterGearName, 75f);
            DeadAirUtils.EnsureInventoryImprovisedFilter(30f);
            DeadAirUtils.ApplyChemicalPoisoning(true, 1, 1.5f, 600f, 1f);
        }

        private static void AirPollutionStop()
        {
            DeadAirUtils.ConfigureInventoryRespirator(false, 600f);
            DeadAirUtils.SetInventoryCanisterDuration(DeadAirUtils.NormalCanisterGearName, 600f);
            DeadAirUtils.EnsureInventoryImprovisedFilter(300f);
            DeadAirUtils.ApplyChemicalPoisoning(false, 0, 0f, 300f, 2.5f);
        }

        private static void DoStuffWithGear()
        {
            if (addedCustomComponents) return;

            DeadAirUtils.EnsureRespiratorCanisterComponent(DeadAirUtils.canisterimprov, 300f);
            addedCustomComponents = true;
        }

        private static void EnsureStarterGearForCurrentSave()
        {
            if (starterGearCheckedForCurrentSave) return;
            if (GameManager.GetInventoryComponent() == null || GameManager.GetPlayerManagerComponent() == null) return;
            if (!TryLoadStarterGearMarker(out bool starterGearGranted)) return;

            if (starterGearGranted)
            {
                starterGearCheckedForCurrentSave = true;
                return;
            }

            if (DeadAirUtils.HasRespiratorInInventory())
            {
                MarkStarterGearGranted();
                starterGearCheckedForCurrentSave = true;
                MelonLogger.Msg("Starter respirator kit marker migrated for this save.");
                return;
            }

            if (!TryGrantStarterGear()) return;

            MarkStarterGearGranted();
            starterGearCheckedForCurrentSave = true;
        }

        private static bool TryGrantStarterGear()
        {
            if (DeadAirUtils.HasRespiratorInInventory()) return false;

            DeadAirUtils.SpawnGearInInventory(DeadAirUtils.NormalCanisterGearName, 1, 1f, 600f);
            DeadAirUtils.SpawnGearInInventory(DeadAirUtils.NormalCanisterGearName, 1, 1f, 600f);
            DeadAirUtils.SpawnGearInInventory(DeadAirUtils.ImprovisedCanisterGearName, 1, 1f, 300f);

            GearItem respirator = DeadAirUtils.SpawnGearInInventory(DeadAirUtils.RespiratorGearName, 1, 1f);
            if (respirator == null)
            {
                MelonLogger.Msg(System.ConsoleColor.Yellow, "Starter respirator kit could not be granted yet.");
                return false;
            }

            if (respirator.m_Respirator != null) respirator.m_Respirator.m_ActiveZones = 1;

            MelonLogger.Msg("Starter respirator kit granted.");
            return true;
        }

        private static bool TryLoadStarterGearMarker(out bool starterGearGranted)
        {
            starterGearGranted = false;

            try
            {
                starterGearGranted = !string.IsNullOrEmpty(SaveData.Load(StarterGearSuffix));
                return true;
            }
            catch (Exception exception)
            {
                MelonLogger.Msg(System.ConsoleColor.Yellow, $"Could not load starter gear marker yet: {exception.Message}");
                return false;
            }
        }

        private static void MarkStarterGearGranted()
        {
            try
            {
                SaveData.Save("1", StarterGearSuffix);
            }
            catch (Exception exception)
            {
                MelonLogger.Msg(System.ConsoleColor.Yellow, $"Could not save starter gear marker: {exception.Message}");
            }
        }
    }
}