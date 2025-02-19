using HarmonyLib;
using LeFauxMods.Common.Integrations.GenericModConfigMenu;
using StardewModdingAPI.Events;
using StardewValley.Locations;
using xTile.Dimensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace LeFauxMods.SelfCheckout;

/// <inheritdoc />
public class ModEntry : Mod
{
    private static readonly HashSet<string> ExcludedShops = new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, List<Response>> Options => new(StringComparer.OrdinalIgnoreCase)
    {
        {
            Game1.shop_animalSupplies, [
                new Response("Supplies", Game1.content.LoadString("Strings\\Locations:AnimalShop_Marnie_Supplies")),
                new Response("Purchase", Game1.content.LoadString("Strings\\Locations:AnimalShop_Marnie_Animals")),
                new Response("Adopt", Game1.content.LoadString("Strings\\1_6_Strings:AdoptPets")),
                new Response("Leave", Game1.content.LoadString("Strings\\Locations:AnimalShop_Marnie_Leave"))
            ]
        },
        {
            Game1.shop_blacksmith, [
                new Response("Shop", Game1.content.LoadString("Strings\\Locations:Blacksmith_Clint_Shop")),
                new Response("Upgrade", Game1.content.LoadString("Strings\\Locations:Blacksmith_Clint_Upgrade")),
                new Response("Process", Game1.content.LoadString("Strings\\Locations:Blacksmith_Clint_Geodes")),
                new Response("Leave", Game1.content.LoadString("Strings\\Locations:Blacksmith_Clint_Leave"))
            ]
        },
        {
            Game1.shop_carpenter, [
                new Response("Shop", Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_Shop")),
                new Response("Upgrade", Game1.IsMasterGame
                    ? Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_UpgradeHouse")
                    : Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_UpgradeCabin")),
                new Response("Renovate",
                    Game1.IsMasterGame
                        ? Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_RenovateHouse")
                        : Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_RenovateCabin")),
                new Response("CommunityUpgrade",
                    Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_CommunityUpgrade")),
                new Response("Construct",
                    Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_Construct")),
                new Response("Leave", Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu_Leave"))
            ]
        }
    };

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        // Init
        I18n.Init(helper.Translation);

        // Events
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

        // Patches
        var harmony = new Harmony(this.ModManifest.UniqueID);
        this.Monitor.Log("Applying Patches");

        try
        {
            _ = harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.performAction),
                    [typeof(string[]), typeof(Farmer), typeof(Location)]),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameLocation_performAction_postfix)));

            _ = harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.animalShop)),
                postfix: new HarmonyMethod(typeof(ModEntry),
                    nameof(GameLocation_animalShop_postfix)));

            _ = harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.blacksmith)),
                postfix: new HarmonyMethod(typeof(ModEntry),
                    nameof(GameLocation_blacksmith_postfix)));

            _ = harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.carpenters)),
                postfix: new HarmonyMethod(typeof(ModEntry),
                    nameof(GameLocation_carpenters_postfix)));

            _ = harmony.Patch(
                AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.TryOpenShopMenu),
                [
                    typeof(string), typeof(GameLocation), typeof(Rectangle), typeof(int), typeof(bool), typeof(bool),
                    typeof(Action<string>)
                ]),
                new HarmonyMethod(typeof(ModEntry), nameof(Utility_TryOpenShopMenu_prefix)));
        }
        catch
        {
            this.Monitor.Log("Failed to apply patches");
        }

        if (!helper.ModRegistry.IsLoaded("mushymato.LivestockBazaar"))
        {
            return;
        }

        this.Monitor.Log("Applying Patches for forced compatibility with Livestock Bazaar");
        _ = harmony.Patch(
            AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.ShowAnimalShopMenu)),
            new HarmonyMethod(typeof(ModEntry), nameof(GameLocation_ShowAnimalShopMenu_prefix)));
    }

    private static bool TryOpenShop(string shopId, GameLocation location)
    {
        if (ExcludedShops.Contains(shopId))
        {
            return false;
        }

        if (!Options.TryGetValue(shopId, out var list))
        {
            return Utility.TryOpenShopMenu(shopId, location, Rectangle.Empty, null, true);
        }

        switch (shopId)
        {
            case Game1.shop_animalSupplies:
                if ((Utility.getAllPets().Count != 0 || Game1.year < 2) &&
                    !Game1.player.mailReceived.Contains("MarniePetAdoption") &&
                    !Game1.player.mailReceived.Contains("MarniePetRejectedAdoption"))
                {
                    list.RemoveAt(2);
                }

                location.createQuestionDialogue("", list.ToArray(), "Marnie");
                return true;

            case Game1.shop_blacksmith:
                if (!Game1.player.Items.Any(static item => Utility.IsGeode(item)))
                {
                    list.RemoveAt(2);
                }

                if (Game1.player.toolBeingUpgraded.Value is not null)
                {
                    list.RemoveAt(1);
                }

                if (list.Count <= 2)
                {
                    Utility.TryOpenShopMenu("Blacksmith", "Clint");
                    return true;
                }

                location.createQuestionDialogue("", list.ToArray(), "Blacksmith");
                return true;

            case Game1.shop_carpenter:
                var underConstruction =
                    Game1.player.daysUntilHouseUpgrade.Value >= 0 ||
                    Game1.IsThereABuildingUnderConstruction();

                if (underConstruction)
                {
                    list.RemoveAt(4);
                }

                if (!Game1.IsMasterGame ||
                    !(Game1.MasterPlayer.mailReceived.Contains("ccIsComplete") ||
                      Game1.MasterPlayer.mailReceived.Contains("JojaMember") ||
                      Game1.MasterPlayer.hasCompletedCommunityCenter()) ||
                    Game1.RequireLocation<Town>("Town").daysUntilCommunityUpgrade.Value > 0 ||
                    (Game1.MasterPlayer.mailReceived.Contains("pamHouseUpgrade") &&
                     Game1.MasterPlayer.mailReceived.Contains("communityUpgradeShortcuts")))
                {
                    list.RemoveAt(3);
                }

                if (Game1.player.houseUpgradeLevel.Value < 2 || underConstruction)
                {
                    list.RemoveAt(2);
                }

                if (Game1.player.houseUpgradeLevel.Value >= 3 || underConstruction)
                {
                    list.RemoveAt(1);
                }

                if (list.Count <= 2)
                {
                    Utility.TryOpenShopMenu("Carpenter", "Robin");
                    return true;
                }

                location.createQuestionDialogue(
                    Game1.content.LoadString("Strings\\Locations:ScienceHouse_CarpenterMenu"),
                    list.ToArray(),
                    "carpenter");

                return true;

            default:
                return false;
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
            __result = TryOpenShop(shopId, __instance);
            return;
        }

        if (DataLoader.Shops(Game1.content).ContainsKey(actionType))
        {
            __result = TryOpenShop(actionType, __instance);
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
    private static void GameLocation_animalShop_postfix(GameLocation __instance, ref bool __result) =>
        __result = __result || TryOpenShop(Game1.shop_animalSupplies, __instance);

    private static bool GameLocation_ShowAnimalShopMenu_prefix(GameLocation __instance) =>
        !__instance.performAction("mushymato.LivestockBazaar_Shop Marnie", Game1.player,
            new Location((int)Game1.player.Tile.X, (int)Game1.player.Tile.Y));

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
    private static void GameLocation_blacksmith_postfix(GameLocation __instance, ref bool __result) =>
        __result = __result || TryOpenShop(Game1.shop_blacksmith, __instance);

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
    private static void GameLocation_carpenters_postfix(GameLocation __instance, ref bool __result) =>
        __result = __result || TryOpenShop(Game1.shop_carpenter, __instance);

    [SuppressMessage("ReSharper", "RedundantAssignment", Justification = "Harmony")]
    private static void Utility_TryOpenShopMenu_prefix(string shopId, ref bool forceOpen) =>
        forceOpen = !ExcludedShops.Contains(shopId);

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var excludedShops = this.Helper.ReadConfig<HashSet<string>>();
        ExcludedShops.UnionWith(excludedShops);

        var api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
        {
            return;
        }

        api.Register(this.ModManifest, Reset, Save);
        api.AddParagraph(this.ModManifest, I18n.ConfigSection_ToggleShops_Description);

        var shops = DataLoader.Shops(Game1.content);
        foreach (var shop in shops.Keys)
        {
            api.AddBoolOption(
                this.ModManifest,
                () => !excludedShops.Contains(shop),
                value =>
                {
                    if (value)
                    {
                        excludedShops.Remove(shop);
                    }
                    else
                    {
                        excludedShops.Add(shop);
                    }
                },
                () => shop,
                I18n.ConfigOption_ToggleShop_Tooltip);
        }

        return;

        void Reset()
        {
            excludedShops.Clear();
            excludedShops.UnionWith(ExcludedShops);
        }

        void Save()
        {
            ExcludedShops.Clear();
            ExcludedShops.UnionWith(excludedShops);
            this.Helper.WriteConfig(ExcludedShops);
        }
    }
}