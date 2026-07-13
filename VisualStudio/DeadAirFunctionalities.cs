namespace DeadAirReborn
{
    internal class DAFunctionalities
    {
        internal static string canisterRefillText;
        internal static GearItem canisterItem;

        private static GameObject canisterRefillButton;
        private static GearItem pendingRefillItem;
        private static int pendingCharcoalCost;
        private static int pendingClothCost;
        private static bool pendingImprovised;

        internal static void InitializeMTB(ItemDescriptionPage itemDescriptionPage)
        {
            if (itemDescriptionPage == null) return;
            if (canisterRefillButton != null) return;

            canisterRefillText = "Refill";

            GameObject equipButton = itemDescriptionPage.m_MouseButtonEquip;
            if (equipButton == null) return;

            canisterRefillButton = UnityEngine.Object.Instantiate(equipButton, equipButton.transform.parent, true);
            canisterRefillButton.name = "Button_DeadAirRefillCanister";
            canisterRefillButton.transform.Translate(0f, -0.1f, 0f);

            UILabel label = Utils.GetComponentInChildren<UILabel>(canisterRefillButton);
            if (label != null) label.text = canisterRefillText;

            AddAction(canisterRefillButton, OnCanisterRefill);
            SetCanisterRefillActive(false);
        }

        private static void AddAction(GameObject button, Action action)
        {
            Il2CppSystem.Collections.Generic.List<EventDelegate> placeHolderList = new Il2CppSystem.Collections.Generic.List<EventDelegate>();
            placeHolderList.Add(new EventDelegate(action));

            UIButton uiButton = Utils.GetComponentInChildren<UIButton>(button);
            if (uiButton != null) uiButton.onClick = placeHolderList;
        }

        internal static void SetCanisterRefillActive(bool active)
        {
            if (canisterRefillButton == null) return;
            NGUITools.SetActive(canisterRefillButton, active);
        }

        private static void OnCanisterRefill()
        {
            GearItem thisGearItem = canisterItem;
            if (thisGearItem == null) return;

            bool normalCanister = DeadAirUtils.IsGearName(thisGearItem, DeadAirUtils.NormalCanisterGearName);
            bool improvisedCanister = DeadAirUtils.IsGearName(thisGearItem, DeadAirUtils.ImprovisedCanisterGearName);

            if (!normalCanister && !improvisedCanister)
            {
                ShowMissingMaterials("This action requires charcoal and cloth");
                return;
            }

            int charcoalCost = normalCanister ? 10 : 7;
            int clothCost = 2;

            if (!HasMaterials(charcoalCost, clothCost))
            {
                ShowMissingMaterials(normalCanister ? "This action requires 10 charcoal and 2 cloth" : "This action requires 7 charcoal and 2 cloth");
                return;
            }

            pendingRefillItem = thisGearItem;
            pendingCharcoalCost = charcoalCost;
            pendingClothCost = clothCost;
            pendingImprovised = improvisedCanister;

            GameAudioManager.PlayGuiConfirm();
            InterfaceManager.GetPanel<Panel_GenericProgressBar>().Launch("Refilling...", 2f, 0f, 0f, "PLAY_CRAFTINGGENERIC", null, false, true, new Action<bool, bool, float>(OnCanisterRefillFinished));
        }

        private static void OnCanisterRefillFinished(bool success, bool playerCancel, float progress)
        {
            if (!success || playerCancel)
            {
                ClearPendingRefill();
                return;
            }

            if (pendingRefillItem == null)
            {
                ClearPendingRefill();
                return;
            }

            if (!HasMaterials(pendingCharcoalCost, pendingClothCost))
            {
                ShowMissingMaterials("This action requires charcoal and cloth");
                ClearPendingRefill();
                return;
            }

            GearItem itemToDestroy = pendingRefillItem;
            string replacementGearName = pendingImprovised ? DeadAirUtils.ImprovisedCanisterGearName : DeadAirUtils.NormalCanisterGearName;
            float replacementDuration = pendingImprovised ? DeadAirUtils.GetImprovisedDurationForCurrentScene() : DeadAirUtils.GetNormalDurationForCurrentScene();

            GameManager.GetInventoryComponent().RemoveGearFromInventory(DeadAirUtils.CharcoalGearName, pendingCharcoalCost, false);
            GameManager.GetInventoryComponent().RemoveGearFromInventory(DeadAirUtils.ClothGearName, pendingClothCost, false);

            if (itemToDestroy != null) UnityEngine.Object.Destroy(itemToDestroy.gameObject);

            DeadAirUtils.SpawnGearInInventory(replacementGearName, 1, 1f, replacementDuration);
            ClearPendingRefill();
        }

        private static void ClearPendingRefill()
        {
            pendingRefillItem = null;
            pendingCharcoalCost = 0;
            pendingClothCost = 0;
            pendingImprovised = false;
        }

        private static bool HasMaterials(int charcoalCost, int clothCost)
        {
            GearItem charcoal = GameManager.GetInventoryComponent().GetBestGearItemWithName(DeadAirUtils.CharcoalGearName);
            GearItem cloth = GameManager.GetInventoryComponent().GetBestGearItemWithName(DeadAirUtils.ClothGearName);

            return GetStackUnits(charcoal) >= charcoalCost && GetStackUnits(cloth) >= clothCost;
        }

        private static int GetStackUnits(GearItem gearItem)
        {
            if (gearItem == null || gearItem.m_StackableItem == null) return 0;
            return gearItem.m_StackableItem.m_Units;
        }

        private static void ShowMissingMaterials(string message)
        {
            HUDMessage.AddMessage(message);
            GameAudioManager.PlayGUIError();
        }
    }
}