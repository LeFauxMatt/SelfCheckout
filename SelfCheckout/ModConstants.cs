namespace LeFauxMods.SelfCheckout;

internal static class ModConstants
{
    public const string ModId = "furyx639.SelfCheckout";

    public static class DataPath
    {
        public const string Shops = "Data/Shops";
    }

    public static class Keys
    {
        public const string Enabled = ModId + "/Enabled";

        public const string Hearts = ModId + "/Hearts";
    }

    public static class LogMessage
    {
        public const string HeartLevelLow = "Hearts required is {0} but heart level is {1} with owner {2} for shop {3}";

        public const string HeartLevelMet = "Hearts required is {0} and heart level is {1} with owner {2} for shop {3}";

        public const string NonSocialOwner = "Hearts required is {0} but owner {1} is non-social for shop {2}";

        public const string NoOwnerFound = "Hearts required is {0} but no owner could be found for shop {1}";

        public const string ShopExcluded = "Shop {0} is excluded from self checkout";
    }

    public static class Mods
    {
        public const string LivestockBazaar = "mushymato.LivestockBazaar";
    }
}