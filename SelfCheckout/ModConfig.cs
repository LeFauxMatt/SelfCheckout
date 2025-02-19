using System.Globalization;
using System.Text;
using LeFauxMods.Common.Interface;
using LeFauxMods.Common.Models;

namespace LeFauxMods.SelfCheckout;

/// <inheritdoc cref="IModConfig{TConfig}" />
internal sealed class ModConfig : IModConfig<ModConfig>, IConfigWithLogAmount
{
    /// <summary>Gets or sets shops to exclude.</summary>
    public HashSet<string> ExcludedShops { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the heart level required for self-checkout.</summary>
    public int HeartLevel { get; set; }

    /// <inheritdoc />
    public LogAmount LogAmount { get; set; }

    /// <inheritdoc />
    public void CopyTo(ModConfig other)
    {
        other.ExcludedShops.Clear();
        other.ExcludedShops.UnionWith(this.ExcludedShops);
        other.HeartLevel = this.HeartLevel;
    }

    /// <inheritdoc />
    public string GetSummary() =>
        new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture,
                $"{nameof(this.ExcludedShops),25}: {string.Join(',', this.ExcludedShops)}")
            .AppendLine(CultureInfo.InvariantCulture, $"{nameof(this.HeartLevel),25}: {this.HeartLevel}")
            .ToString();
}