namespace DeadAirReborn
{
    internal class Patches
    {
        [HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.Initialize))]
        internal class DeadAirInitialization
        {
            private static void Postfix(Panel_Inventory __instance)
            {
                DeadAirUtils.inventory = __instance;
                DAFunctionalities.InitializeMTB(__instance.m_ItemDescriptionPage);
            }
        }

        [HarmonyPatch(typeof(ItemDescriptionPage), nameof(ItemDescriptionPage.UpdateGearItemDescription))]
        internal class UpdateInventoryButton
        {
            private static void Postfix(ItemDescriptionPage __instance, GearItem __0)
            {
                if (__instance != InterfaceManager.GetPanel<Panel_Inventory>()?.m_ItemDescriptionPage) return;

                GearItem gearItem = __0;
                DAFunctionalities.canisterItem = gearItem;
                DAFunctionalities.SetCanisterRefillActive(DeadAirUtils.IsRefillableRuinedCanister(gearItem));
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSaveGameSlot), new[] { typeof(string), typeof(int) })]
        internal class LoadSavePatch
        {
            private static void Postfix()
            {
                DeadAirMain.ResetStarterGearRuntimeCheck();
            }
        }

        [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.CreateSlot), new[] { typeof(string), typeof(SaveSlotType), typeof(uint), typeof(Episode) })]
        internal class NewGamePatch
        {
            private static void Postfix()
            {
                DeadAirMain.ResetStarterGearRuntimeCheck();
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.DoExitToMainMenu))]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadMainMenu))]
        internal class MainMenuPatch
        {
            private static void Postfix()
            {
                DeadAirMain.ResetStarterGearRuntimeCheck();
            }
        }
    }
}