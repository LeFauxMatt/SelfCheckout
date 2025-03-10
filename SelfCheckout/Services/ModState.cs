using LeFauxMods.Common.Services;
using LeFauxMods.Common.Utilities;
using StardewModdingAPI.Events;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using StardewValley.Locations;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace LeFauxMods.SelfCheckout.Services;

/// <summary>Responsible for managing state.</summary>
internal sealed class ModState
{
    private static ModState? Instance;
    private readonly ConfigHelper<ModConfig> configHelper;
    private readonly IModHelper helper;
    private readonly IManifest manifest;
    private ConfigMenu? configMenu;
    private Dictionary<string, ShopData>? data;

    private ModState(IModHelper helper, IManifest manifest)
    {
        this.helper = helper;
        this.manifest = manifest;
        this.configHelper = new ConfigHelper<ModConfig>(helper);

        // Events
        helper.Events.Content.AssetsInvalidated += this.OnAssetsInvalidated;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    public static ModConfig Config => Instance!.configHelper.Config;

    public static ConfigHelper<ModConfig> ConfigHelper => Instance!.configHelper;

    public static bool IsLivestockBazaarLoaded =>
        Instance!.helper.ModRegistry.IsLoaded(ModConstants.Mods.LivestockBazaar);

    public static Dictionary<string, ShopData> Data => Instance!.data ??= DataLoader.Shops(Game1.content);

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

    public static void Init(IModHelper helper, IManifest manifest) => Instance ??= new ModState(helper, manifest);

    public static bool CanOpenShop(string shopId, string? ownerName)
    {
        if (Config.ForceOpen.IsDown())
        {
            return true;
        }

        if (!Data.TryGetValue(shopId, out var shopData) ||
            shopData.CustomFields is null ||
            !shopData.CustomFields.ContainsKey(ModConstants.Keys.Enabled))
        {
            Log.Trace(ModConstants.LogMessage.ShopExcluded, shopId);
            return false;
        }

        var heartsRequired = shopData.CustomFields.GetInt(ModConstants.Keys.Hearts);
        if (heartsRequired <= 0)
        {
            return true;
        }

        var ownerData = ShopBuilder.GetCurrentOwners(shopData)
            .FirstOrDefault(p =>
                p.Type is ShopOwnerType.NamedNpc && (string.IsNullOrWhiteSpace(ownerName) || p.IsValid(ownerName)));

        if (ownerData is null)
        {
            Log.Info(ModConstants.LogMessage.NoOwnerFound, heartsRequired, shopId);
            return true;
        }

        var owner = Game1.getCharacterFromName(ownerData.Name);
        var heartLevel = Game1.player.getFriendshipHeartLevelForNPC(owner.Name);
        if (!owner.CanSocialize && heartLevel == 0)
        {
            Log.Info(ModConstants.LogMessage.NonSocialOwner, heartsRequired, owner.Name, shopId);
            return false;
        }

        if (heartLevel < heartsRequired)
        {
            Log.Info(ModConstants.LogMessage.HeartLevelLow, heartsRequired, heartLevel, owner.Name, shopId);
            return false;
        }

        Log.Trace(ModConstants.LogMessage.HeartLevelMet, heartsRequired, heartLevel, owner.Name, shopId);
        return true;
    }

    public static bool TryOpenShop(string shopId, GameLocation location, string? ownerName = null)
    {
        if (!CanOpenShop(shopId, ownerName))
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

                location.createQuestionDialogue(string.Empty, list.ToArray(), "Marnie");
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

                location.createQuestionDialogue(string.Empty, list.ToArray(), "Blacksmith");
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

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) =>
        this.configMenu = new ConfigMenu(this.helper, this.manifest);

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (!e.NamesWithoutLocale.Any(static assetName => assetName.IsEquivalentTo(ModConstants.DataPath.Shops)))
        {
            return;
        }

        this.data = null;
        this.configMenu?.SetupMenu();
    }
}