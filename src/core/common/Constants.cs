namespace mono8.core.common;

public static class Constants
{
    public static class Colors
    {
        public const int Black = 0;
        public const int DarkBlue = 1;
        public const int DarkPurple = 2;
        public const int DarkGreen = 3;
        public const int Brown = 4;
        public const int DarkGray = 5;
        public const int LightGray = 6;
        public const int White = 7;
        public const int Red = 8;
        public const int Orange = 9;
        public const int Yellow = 10;
        public const int Green = 11;
        public const int Blue = 12;
        public const int Indigo = 13;
        public const int Pink = 14;
        public const int Peach = 15;

        public const int BrownBlack = 16;
        public const int DarkerBlue = 17;
        public const int DarkMaroon = 18;
        public const int DarkTeal = 19;
        public const int DarkBrick = 20;
        public const int DarkTaupe = 21;
        public const int Tan = 22;
        public const int LightYellow = 23;
        public const int DarkCrimson = 24;
        public const int BrightOrange = 25;
        public const int LimeGreen = 26;
        public const int MediumGreen = 27;
        public const int MediumBlue = 28;
        public const int Mauve = 29;
        public const int Salmon = 30;
        public const int LightPeach = 31;
    }

    public static class Screen
    {
        public static int ResolutionX = 256;
        public static int ResolutionY = 144;
    }

    public static class GameDataSizes
    {
        public const int Sfx = 64;
        public const int Music = 64;
        public const int IconSheetX = 256;
        public const int IconSheetY = 16;
        public const int SpriteSheetX = 256;
        public const int SpriteSheetY = 48 * 5;
        public const int TileSize = 8;
        public const int SpriteSheetColumns = 32; // SpriteSheetX / TileSize
        public const int SpriteSheetRows = 30; // SpriteSheetY / TileSize
        public const int MaxSpriteIndex = SpriteSheetColumns * SpriteSheetRows - 1;
        public const int MapSheetX = 512; // 16 * Screen.ResolutionX / TileSize
        public const int MapSheetY = 576; // 32 * Screen.ResolutionY / TileSize
        public const int ColorPalette = 32;
        public const int ColorPaletteMin = 0;
        public const int ColorPaletteMax = 31;
        public const int SaveDataSlotCount = 64;
    }

    public static class File
    {
        public const string Folder = "data";

        public const string Name = "data";

        public const string Main = "main";

        public static class Extensions
        {
            public const string Sfx = "sfx";

            public const string Music = "music";

            public const string SpriteSheet = "gfx";

            public const string MapSheet = "map";

            public const string Flags = "gff";

            public const string IconSheet = "icons";

            public const string Save = "save";
        }
    }
}
