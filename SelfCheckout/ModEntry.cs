using LeFauxMods.Common.Models;
using LeFauxMods.Common.Utilities;
using LeFauxMods.SelfCheckout.Services;
using StardewModdingAPI.Events;
using StardewValley.GameData.Shops;

namespace LeFauxMods.SelfCheckout;

/// <inheritdoc />
public class ModEntry : Mod
{
    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        // Init
        I18n.Init(helper.Translation);
        ModState.Init(helper, this.ModManifest);
        Log.Init(this.Monitor, ModState.Config);
        ModPatches.Apply();

        // Events
        helper.Events.Content.AssetRequested += OnAssetRequested;
        ModEvents.Subscribe<ConfigChangedEventArgs<ModConfig>>(this.OnConfigChanged);
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (!e.NameWithoutLocale.IsEquivalentTo(ModConstants.DataPath.Shops))
        {
            return;
        }

        e.Edit(static assetData =>
            {
                var data = assetData.AsDictionary<string, ShopData>().Data;
                foreach (var (shopId, shopData) in data)
                {
                    if (ModState.Config.ExcludedShops.Contains(shopId))
                    {
                        continue;
                    }

                    shopData.CustomFields ??= [];
                    shopData.CustomFields[ModConstants.Keys.Enabled] = "true";

                    if (ModState.Config.HeartLevel > 0 && shopData.Owners is not null)
                    {
                        _ = shopData.CustomFields.TryAdd(ModConstants.Keys.Hearts, $"{ModState.Config.HeartLevel}");
                    }
                }
            },
            AssetEditPriority.Late);
    }

    private void OnConfigChanged(ConfigChangedEventArgs<ModConfig> e) =>
        this.Helper.GameContent.InvalidateCache(ModConstants.DataPath.Shops);
}