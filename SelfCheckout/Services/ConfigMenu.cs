using LeFauxMods.Common.Services;

namespace LeFauxMods.SelfCheckout.Services;

/// <inheritdoc />
internal sealed class ConfigMenu(IModHelper helper, IManifest manifest)
    : BaseConfigMenu<ModConfig>(helper, manifest)
{
    /// <inheritdoc />
    protected override ModConfig Config => ModState.ConfigHelper.Temp;

    /// <inheritdoc />
    protected override ConfigHelper<ModConfig> ConfigHelper => ModState.ConfigHelper;

    /// <inheritdoc />
    protected internal override void SetupOptions()
    {
        this.Api.AddKeybindList(
            this.Manifest,
            () => this.Config.ForceOpen,
            value => this.Config.ForceOpen = value,
            I18n.ConfigOption_ForceOpen_Name,
            I18n.ConfigOption_ForceOpen_Tooltip);

        this.Api.AddNumberOption(
            this.Manifest,
            () => this.Config.HeartLevel,
            value => this.Config.HeartLevel = value,
            I18n.ConfigOption_HeartLevel_Name,
            I18n.ConfigOption_HeartLevel_Tooltip,
            0,
            10);

        this.Api.AddParagraph(this.Manifest, I18n.ConfigSection_ToggleShops_Description);
        foreach (var shop in ModState.Data.Keys)
        {
            this.Api.AddBoolOption(
                this.Manifest,
                () => !this.Config.ExcludedShops.Contains(shop),
                value =>
                {
                    if (value)
                    {
                        this.Config.ExcludedShops.Remove(shop);
                    }
                    else
                    {
                        this.Config.ExcludedShops.Add(shop);
                    }
                },
                () => shop,
                I18n.ConfigOption_ToggleShop_Tooltip);
        }
    }
}