using Il2CppTLD.Gear;

namespace DeadAirReborn
{
    internal static class DeadAirUtils
    {
        private enum AirPollutionProfile
        {
            Outside,
            Inside,
            CinderHills,
            Safe
        }

        private enum CanisterKind
        {
            Unknown,
            Normal,
            Improvised
        }

        internal const string NormalCanisterGearName = "GEAR_Canister";
        internal const string ImprovisedCanisterGearName = "GEAR_ImprovisedFilter";
        internal const string RespiratorGearName = "GEAR_Respirator";
        internal const string CharcoalGearName = "GEAR_Charcoal";
        internal const string ClothGearName = "GEAR_Cloth";

        public static Panel_Inventory inventory;

        private static GearItem loadedCanister;
        private static GearItem loadedCanister2;
        private static GearItem loadedCanister3;
        private static GearItem loadedImprovisedCanister;
        private static GearItem loadedRespirator;

        private static GearItem trackedMaskDrainCanister;
        private static CanisterKind trackedMaskDrainKind = CanisterKind.Unknown;
        private static AirPollutionProfile trackedMaskDrainProfile = AirPollutionProfile.Outside;
        private static float trackedMaskDrainCondition;
        private static float lastMaskDrainRealtime = -1f;

        public static GearItem canister => loadedCanister ??= LoadGearItemPrefabSafe(NormalCanisterGearName);
        public static GearItem canister2 => loadedCanister2 ??= LoadGearItemPrefabSafe(NormalCanisterGearName);
        public static GearItem canister3 => loadedCanister3 ??= LoadGearItemPrefabSafe(NormalCanisterGearName);
        public static GearItem canisterimprov => loadedImprovisedCanister ??= LoadGearItemPrefabSafe(ImprovisedCanisterGearName);
        public static GearItem respirator => loadedRespirator ??= LoadGearItemPrefabSafe(RespiratorGearName);

        public static GameObject GetPlayer()
        {
            return GameManager.GetPlayerObject();
        }

        public static T? GetComponentSafe<T>(this Component? component) where T : Component
        {
            return component == null ? default : GetComponentSafe<T>(component.GetGameObject());
        }

        public static T? GetComponentSafe<T>(this GameObject? gameObject) where T : Component
        {
            return gameObject == null ? default : gameObject.GetComponent<T>();
        }

        public static T? GetOrCreateComponent<T>(this Component? component) where T : Component
        {
            return component == null ? default : GetOrCreateComponent<T>(component.GetGameObject());
        }

        public static T? GetOrCreateComponent<T>(this GameObject? gameObject) where T : Component
        {
            if (gameObject == null) return default;

            T? result = GetComponentSafe<T>(gameObject);
            if (result == null) result = gameObject.AddComponent<T>();

            return result;
        }

        internal static GameObject? GetGameObject(this Component? component)
        {
            try
            {
                return component == null ? default : component.gameObject;
            }
            catch (System.Exception exception)
            {
                MelonLogger.Msg($"Returning null since this could not obtain a Game Object from the component. Stack trace:\n{exception.Message}");
            }

            return null;
        }

        internal static GearItem GetInventoryGear(string gearName)
        {
            Inventory inventoryComponent = GameManager.GetInventoryComponent();
            if (inventoryComponent == null || string.IsNullOrEmpty(gearName)) return null;

            try
            {
                return inventoryComponent.GearInInventory(gearName, 1);
            }
            catch (System.Exception exception)
            {
                MelonLogger.Msg(System.ConsoleColor.Yellow, $"Inventory lookup failed for '{gearName}': {exception.Message}");
                return null;
            }
        }

        internal static GearItem SpawnGearInInventory(string gearName, int count, float normalizedCondition, float canisterSeconds = 0f)
        {
            GearItem prefab = LoadGearItemPrefabSafe(gearName);
            if (prefab == null) return null;

            PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
            if (playerManager == null) return null;

            GearItem spawned;

            try
            {
                spawned = playerManager.InstantiateItemInPlayerInventory(prefab, count, normalizedCondition, default);
            }
            catch (System.Exception exception)
            {
                MelonLogger.Msg(System.ConsoleColor.Red, $"Failed to spawn '{gearName}' in inventory: {exception.Message}");
                return null;
            }

            if (spawned == null) return null;

            spawned.SetNormalizedHP(Mathf.Clamp01(normalizedCondition), false);

            if (IsGearName(spawned, ImprovisedCanisterGearName)) EnsureRespiratorCanisterComponent(spawned, canisterSeconds > 0f ? canisterSeconds : 300f);
            if (IsGearName(spawned, NormalCanisterGearName)) SetCanisterDuration(spawned, canisterSeconds > 0f ? canisterSeconds : 600f);

            return spawned;
        }

        internal static bool HasRespiratorInInventory()
        {
            return GetInventoryGear(RespiratorGearName) != null;
        }

        internal static void RefreshPickedUpCanisterDurations()
        {
            PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
            if (playerManager == null) return;

            GearItem pickup = playerManager.m_PickupGearItem;
            if (pickup == null) return;

            if (IsGearName(pickup, NormalCanisterGearName))
            {
                SetCanisterDuration(pickup, GetNormalDurationForCurrentScene());
                return;
            }

            if (IsGearName(pickup, ImprovisedCanisterGearName))
            {
                EnsureRespiratorCanisterComponent(pickup, GetImprovisedDurationForCurrentScene());
            }
        }


        internal static void UpdateMaskConsumptionMode()
        {
            if (!Settings.UseInGameMaskConsumption)
            {
                ResetMaskDrainTracking();
                return;
            }

            Respirator currentRespirator = GetCurrentEquippedRespirator();
            RespiratorCanister attachedCanister = currentRespirator?.m_AttachedCanister;
            GearItem gearItem = attachedCanister?.m_GearItem;
            if (currentRespirator == null || currentRespirator.m_ActiveZones <= 0 || attachedCanister == null || gearItem == null)
            {
                ResetMaskDrainTracking();
                return;
            }

            AirPollutionProfile profile = GetCurrentAirPollutionProfile();
            if (profile == AirPollutionProfile.Safe)
            {
                ResetMaskDrainTracking();
                return;
            }

            CanisterKind kind = GetCanisterKind(attachedCanister);
            if (kind == CanisterKind.Unknown) kind = CanisterKind.Normal;

            float lifetimeHours = GetInGameLifetimeHours(kind, profile);
            if (lifetimeHours <= 0f)
            {
                ResetMaskDrainTracking();
                return;
            }

            attachedCanister.m_ProtectionDurationRTSeconds = GetRealTimeProtectionSeconds(kind, profile);

            float now = Time.realtimeSinceStartup;
            float currentCondition = GetNormalizedConditionSafe(gearItem);

            if (trackedMaskDrainCanister != gearItem || trackedMaskDrainKind != kind || trackedMaskDrainProfile != profile || lastMaskDrainRealtime < 0f)
            {
                trackedMaskDrainCanister = gearItem;
                trackedMaskDrainKind = kind;
                trackedMaskDrainProfile = profile;
                trackedMaskDrainCondition = currentCondition;
                lastMaskDrainRealtime = now;
                return;
            }

            float realSecondsElapsed = Mathf.Clamp(now - lastMaskDrainRealtime, 0f, 5f);
            lastMaskDrainRealtime = now;

            TimeOfDay timeOfDay = GameManager.GetTimeOfDayComponent();
            if (timeOfDay == null || realSecondsElapsed <= 0f)
            {
                trackedMaskDrainCondition = currentCondition;
                return;
            }

            float gameHoursElapsed = timeOfDay.GetTODHours(realSecondsElapsed);
            if (gameHoursElapsed <= 0f)
            {
                trackedMaskDrainCondition = currentCondition;
                return;
            }

            float targetCondition = Mathf.Clamp01(trackedMaskDrainCondition - (gameHoursElapsed / lifetimeHours));
            gearItem.SetNormalizedHP(targetCondition, false);
            trackedMaskDrainCondition = targetCondition;
        }

        internal static void ResetMaskDrainTracking()
        {
            trackedMaskDrainCanister = null;
            trackedMaskDrainKind = CanisterKind.Unknown;
            trackedMaskDrainProfile = AirPollutionProfile.Outside;
            trackedMaskDrainCondition = 0f;
            lastMaskDrainRealtime = -1f;
        }

        internal static void ConfigureInventoryRespirator(bool active, float attachedCanisterSeconds)
        {
            GearItem inventoryRespirator = GetInventoryGear(RespiratorGearName);
            if (inventoryRespirator != null && inventoryRespirator.m_Respirator != null)
            {
                ConfigureRespirator(inventoryRespirator.m_Respirator, active, attachedCanisterSeconds);
            }

            try
            {
                Respirator currentRespirator = RespiratorManager.CurrentEquipped;
                if (currentRespirator != null) ConfigureRespirator(currentRespirator, active, attachedCanisterSeconds);
            }
            catch
            {
            }
        }

        internal static void SetInventoryCanisterDuration(string gearName, float protectionSeconds)
        {
            SetCanisterDuration(GetInventoryGear(gearName), protectionSeconds);
        }

        internal static void EnsureInventoryImprovisedFilter(float protectionSeconds)
        {
            EnsureRespiratorCanisterComponent(GetInventoryGear(ImprovisedCanisterGearName), protectionSeconds);
        }

        internal static void ApplyChemicalPoisoning(bool inHazardZone, int activeZones, float clothingHPLostPerHour, float toxicityGainedPerHour, float toxicityLostPerHour)
        {
            ChemicalPoisoning chemicalPoisoning = GameManager.GetChemicalPoisoningComponent();
            if (chemicalPoisoning == null) return;

            chemicalPoisoning.m_InHazardZone = inHazardZone;
            chemicalPoisoning.m_ActiveZones = activeZones;
            chemicalPoisoning.m_ClothingHPLostPerHour = clothingHPLostPerHour;
            chemicalPoisoning.m_ClothingDamageRegion = (ClothingRegion)0;
            chemicalPoisoning.m_ToxicityGainedPerHour = toxicityGainedPerHour;
            chemicalPoisoning.m_ToxicityLostPerHour = toxicityLostPerHour;
        }

        internal static bool IsRefillableRuinedCanister(GearItem gearItem)
        {
            if (gearItem == null) return false;
            if (!IsGearName(gearItem, NormalCanisterGearName) && !IsGearName(gearItem, ImprovisedCanisterGearName)) return false;

            if (IsGearName(gearItem, ImprovisedCanisterGearName)) EnsureRespiratorCanisterComponent(gearItem, 300f);

            return IsRuined(gearItem);
        }

        internal static bool IsGearName(GearItem gearItem, string gearName)
        {
            if (gearItem == null || string.IsNullOrEmpty(gearName)) return false;
            if (gearItem.name == gearName) return true;
            return gearItem.name.StartsWith(gearName + "(", System.StringComparison.Ordinal);
        }

        internal static void EnsureRespiratorCanisterComponent(GearItem gearItem, float protectionSeconds)
        {
            if (gearItem == null) return;

            RespiratorCanister canisterComponent = gearItem.GetComponent<RespiratorCanister>();
            if (canisterComponent == null) canisterComponent = gearItem.gameObject.AddComponent<RespiratorCanister>();

            canisterComponent.m_GearItem = gearItem;
            canisterComponent.m_ProtectionDurationRTSeconds = protectionSeconds;
            gearItem.m_RespiratorCanister = canisterComponent;
        }


        private static Respirator GetCurrentEquippedRespirator()
        {
            try
            {
                return RespiratorManager.CurrentEquipped;
            }
            catch
            {
                return null;
            }
        }

        private static float GetNormalizedConditionSafe(GearItem gearItem)
        {
            if (gearItem == null) return 0f;

            try
            {
                return Mathf.Clamp01(gearItem.GetNormalizedCondition());
            }
            catch
            {
                return Mathf.Clamp01(gearItem.m_CurrentHP / 100f);
            }
        }

        private static GearItem LoadGearItemPrefabSafe(string gearName)
        {
            try
            {
                GearItem prefab = GearItem.LoadGearItemPrefab(gearName);
                if (prefab == null) MelonLogger.Msg(System.ConsoleColor.Yellow, $"Could not load prefab '{gearName}'.");
                return prefab;
            }
            catch (System.Exception exception)
            {
                MelonLogger.Msg(System.ConsoleColor.Red, $"Could not load prefab '{gearName}': {exception.Message}");
                return null;
            }
        }

        private static void ConfigureRespirator(Respirator respiratorComponent, bool active, float attachedCanisterSeconds)
        {
            if (respiratorComponent == null) return;

            respiratorComponent.m_ActiveZones = active ? 1 : 0;
            if (respiratorComponent.m_AttachedCanister == null) return;

            CanisterKind kind = GetCanisterKind(respiratorComponent.m_AttachedCanister);
            if (kind == CanisterKind.Unknown)
            {
                respiratorComponent.m_AttachedCanister.m_ProtectionDurationRTSeconds = attachedCanisterSeconds;
                return;
            }

            respiratorComponent.m_AttachedCanister.m_ProtectionDurationRTSeconds = GetRealTimeProtectionSeconds(kind, GetCurrentAirPollutionProfile());
        }

        private static void SetCanisterDuration(GearItem gearItem, float protectionSeconds)
        {
            if (gearItem == null || gearItem.m_RespiratorCanister == null) return;
            gearItem.m_RespiratorCanister.m_ProtectionDurationRTSeconds = protectionSeconds;
        }

        internal static float GetNormalDurationForCurrentScene()
        {
            return GetRealTimeProtectionSeconds(CanisterKind.Normal, GetCurrentAirPollutionProfile());
        }

        internal static float GetImprovisedDurationForCurrentScene()
        {
            return GetRealTimeProtectionSeconds(CanisterKind.Improvised, GetCurrentAirPollutionProfile());
        }

        private static CanisterKind GetCanisterKind(RespiratorCanister canisterComponent)
        {
            return canisterComponent == null ? CanisterKind.Unknown : GetCanisterKind(canisterComponent.m_GearItem);
        }

        private static CanisterKind GetCanisterKind(GearItem gearItem)
        {
            if (gearItem == null) return CanisterKind.Unknown;
            if (IsGearName(gearItem, ImprovisedCanisterGearName)) return CanisterKind.Improvised;
            if (IsGearName(gearItem, NormalCanisterGearName)) return CanisterKind.Normal;
            return CanisterKind.Unknown;
        }

        private static AirPollutionProfile GetCurrentAirPollutionProfile()
        {
            string scene = GameManager.m_ActiveScene;
            if (IsSceneSafe(scene)) return AirPollutionProfile.Safe;
            if (IsSceneCinderHills(scene)) return AirPollutionProfile.CinderHills;
            if (IsSceneHouse(scene)) return AirPollutionProfile.Inside;
            return AirPollutionProfile.Outside;
        }

        private static float GetRealTimeProtectionSeconds(CanisterKind kind, AirPollutionProfile profile)
        {
            switch (profile)
            {
                case AirPollutionProfile.CinderHills:
                    return kind == CanisterKind.Improvised ? 30f : 75f;
                case AirPollutionProfile.Inside:
                    return kind == CanisterKind.Improvised ? 600f : 1200f;
                default:
                    return kind == CanisterKind.Improvised ? 300f : 600f;
            }
        }

        private static float GetInGameLifetimeHours(CanisterKind kind, AirPollutionProfile profile)
        {
            switch (profile)
            {
                case AirPollutionProfile.CinderHills:
                    return kind == CanisterKind.Improvised ? 0.1f : 0.25f;
                case AirPollutionProfile.Inside:
                    return kind == CanisterKind.Improvised ? 2f : 4f;
                default:
                    return kind == CanisterKind.Improvised ? 1f : 2f;
            }
        }

        private static bool IsRuined(GearItem gearItem)
        {
            if (gearItem == null) return false;

            try
            {
                return gearItem.GetNormalizedCondition() <= 0.001f || gearItem.m_CurrentHP <= 0f;
            }
            catch
            {
                return gearItem.m_CurrentHP <= 0f;
            }
        }

        public static bool IsScenePlayable()
        {
            return IsScenePlayable(GameManager.m_ActiveScene);
        }

        public static bool IsScenePlayable(string scene)
        {
            return !(string.IsNullOrEmpty(scene) || scene.Contains("MainMenu") || scene == "Boot" || scene == "Empty");
        }

        public static bool IsMainMenu(string scene)
        {
            return !string.IsNullOrEmpty(scene) && scene.Contains("MainMenu");
        }

        public static bool IsSceneSafe(string scene)
        {
            return !string.IsNullOrEmpty(scene) && scene.Contains("Prepper");
        }

        public static bool IsSceneHouse(string scene)
        {
            return !string.IsNullOrEmpty(scene) && (scene.Contains("Cabin") || scene.Contains("House") || scene.Contains("Church"));
        }

        public static bool IsSceneCinderHills(string scene)
        {
            return !string.IsNullOrEmpty(scene) && scene.Contains("MineTransitionZone");
        }
    }
}