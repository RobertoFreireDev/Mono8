namespace mono8.core.common;

public static class Constants
{
    public static class Screen
    {
        public static int ResolutionX = 256;
        public static int ResolutionY = 144;
    }

    public static class GameDataSizes
    {
        public const int Sfx = 64;
        public const int Music = 64;
        public const int SpriteSheetX = 128;
        public const int SpriteSheetY = 128;
        public const int TileSize = 8;
        public const int SpriteSheetColumns = 16; // SpriteSheetX / TileSize
        public const int SpriteSheetRows = 16; // SpriteSheetY / TileSize
        public const int MaxSpriteIndex = SpriteSheetColumns * SpriteSheetRows - 1;
        public const int MapSheetX = 128;
        public const int MapSheetY = 64;
        public const int ColorPalette = 16;
        public const int ColorPaletteMin = 0;
        public const int ColorPaletteMax = 15;
        public const int SaveDataSlotCount = 64;
    }

    public static class File
    {
        public const string Name = "data";

        public const string Main = "main";

        public static class Extensions
        {
            public const string Sfx = "sfx";

            public const string Music = "music";

            public const string SpriteSheet = "sprt";

            public const string MapSheet = "map";

            public const string Flags = "flags";

            public const string Save = "save";
        }
    }
}
