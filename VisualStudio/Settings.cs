namespace DeadAirReborn
{
    internal class Settings : JsonModSettings
    {
        internal static Settings instance = new Settings();

        [Section("Options")]

        [Name("Enable Mod")]
        [Description("Choose if you want Dead Air Reborn to be on or off.")]
        public bool useMod = true;

        [Name("Mask Consumption Time")]
        [Description("Choose whether respirator canisters drain using real time or in-game time.")]
        [Choice("Real-time", "In-game time")]
        public int maskConsumptionTimeMode = 0;

        internal static bool UseInGameMaskConsumption => instance.maskConsumptionTimeMode == 1;

        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            base.OnChange(field, oldValue, newValue);

            if (field.Name == nameof(maskConsumptionTimeMode))
            {
                DeadAirUtils.ResetMaskDrainTracking();
            }
        }
    }
}