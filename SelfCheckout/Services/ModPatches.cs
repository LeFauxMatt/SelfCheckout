using HarmonyLib;
using LeFauxMods.Common.Utilities;
using xTile.Dimensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace LeFauxMods.SelfCheckout.Services;

/// <summary>Encapsulates mod patches.</summary>
internal static class ModPatches
{
    private static readonly Harmony Harmony = new(ModConstants.ModId);

    public static void Apply()
    {
        Log.Trace("Applying Patches");

        try
        {
            _ = Harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.performAction),
                    [typeof(string[]), typeof(Farmer), typeof(Location)]),
                postfix: new HarmonyMethod(typeof(ModPatches), nameof(GameLocation_performAction_postfix)));

            _ = Harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.animalShop)),
                postfix: new HarmonyMethod(typeof(ModPatches),
                    nameof(GameLocation_animalShop_postfix)));

            _ = Harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.blacksmith)),
                postfix: new HarmonyMethod(typeof(ModPatches),
                    nameof(GameLocation_blacksmith_postfix)));

            _ = Harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.carpenters)),
                postfix: new HarmonyMethod(typeof(ModPatches),
                    nameof(GameLocation_carpenters_postfix)));

            _ = Harmony.Patch(
                AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.TryOpenShopMenu),
                [
                    typeof(string), typeof(GameLocation), typeof(Rectangle), typeof(int), typeof(bool), typeof(bool),
                    typeof(Action<string>)
                ]),
                new HarmonyMethod(typeof(ModPatches), nameof(Utility_TryOpenShopMenu_prefix)));

            if (ModState.IsLivestockBazaarLoaded)
            {
                _ = Harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.ShowAnimalShopMenu)),
                    new HarmonyMethod(typeof(ModPatches), nameof(GameLocation_ShowAnimalShopMenu_prefix)));
            }
        }
        catch
        {
            Log.Warn("Failed to apply patches");
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
    private static void GameLocation_performAction_postfix(
        GameLocation __instance,
        string[] action,
        Farmer who,
        Location tileLocation,
        ref bool __result)
    {
        if (__result ||
            __instance.ShouldIgnoreAction(action, who, tileLocation) ||
            !ArgUtility.TryGet(action, 0, out var actionType, out _, true, "string actionType") ||
            !who.IsLocalPlayer)
        {
            return;
        }

        if (actionType == "OpenShop" &&
            ArgUtility.TryGet(action, 1, out var shopId, out _, true, "string shopId"))
        {
            __result = ModState.TryOpenShop(shopId, __instance);
            return;
        }

        if (DataLoader.Shops(Game1.content).ContainsKey(actionType))
        {
            __result = ModState.TryOpenShop(actionType, __instance);
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
    private static void GameLocation_animalShop_postfix(GameLocation __instance, ref bool __result) =>
        __result = (__result && !Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth).Equals("Tue")) ||
                   ModState.TryOpenShop(Game1.shop_animalSupplies, __instance, "Marnie");

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
    private static bool GameLocation_ShowAnimalShopMenu_prefix(GameLocation __instance) =>
        !__instance.performAction(ModConstants.Mods.LivestockBazaar + "_Shop Marnie", Game1.player,
            new Location((int)Game1.player.Tile.X, (int)Game1.player.Tile.Y));

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
    private static void GameLocation_blacksmith_postfix(GameLocation __instance, ref bool __result) =>
        __result = __result || ModState.TryOpenShop(Game1.shop_blacksmith, __instance, "Clint");

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
    private static void GameLocation_carpenters_postfix(GameLocation __instance, ref bool __result) =>
        __result = (__result && !Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth).Equals("Tue")) ||
                   ModState.TryOpenShop(Game1.shop_carpenter, __instance, "Robin");

    [SuppressMessage("ReSharper", "RedundantAssignment", Justification = "Harmony")]
    private static void Utility_TryOpenShopMenu_prefix(string shopId, ref bool forceOpen) =>
        forceOpen |= ModState.CanOpenShop(shopId, null);
}