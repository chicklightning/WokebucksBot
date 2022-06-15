using Discord;

namespace Swamp.WokebucksBot.Bot
{
    public static class Levels
    {
        public static Level NeanderthalBrain = new Level()
        {
            Id = 1,
            Name = "Neanderthal Brain",
            Amount = 75,
            Color = Color.DarkRed,
            UpperLimit = 15,
            LowerLimit = -10
        };

        public static Level ExtremelySmoothBrain = new Level()
        {
            Id = 2,
            Name = "Extremely Smooth Brain",
            Amount = 100,
            Color = Color.Red,
            UpperLimit = 15,
            LowerLimit = -10
        };

        public static Level VerySmoothBrain = new Level()
        {
            Id = 3,
            Name = "Very Smooth Brain",
            Amount = 125,
            Color = Color.Orange,
            UpperLimit = 16,
            LowerLimit = -11
        };

        public static Level SmoothBrain = new Level()
        {
            Id = 4,
            Name = "Smooth Brain",
            Amount = 150,
            Color = Color.Gold,
            UpperLimit = 16,
            LowerLimit = -11
        };

        public static Level UnwrinkledBrain = new Level()
        {
            Id = 5,
            Name = "Unwrinkled Brain",
            Amount = 175,
            Color = Color.Green,
            UpperLimit = 17,
            LowerLimit = -12
        };

        public static Level HasOneWrinkleBrain = new Level()
        {
            Id = 6,
            Name = "Has-One-Wrinkle Brain",
            Amount = 200,
            Color = Color.DarkGreen,
            UpperLimit = 17,
            LowerLimit = -12
        };

        public static Level KindaWrinkleBrain = new Level()
        {
            Id = 7,
            Name = "Kinda Wrinkle Brain",
            Amount = 200,
            Color = Color.Blue,
            UpperLimit = 18,
            LowerLimit = -13
        };

        public static Level WrinkleBrain = new Level()
        {
            Id = 8,
            Name = "Wrinkle Brain",
            Amount = 200,
            Color = Color.DarkBlue,
            UpperLimit = 18,
            LowerLimit = -13
        };

        public static Level VeryWrinkleBrain = new Level()
        {
            Id = 9,
            Name = "Very Wrinkle Brain",
            Amount = 200,
            Color = Color.Purple,
            UpperLimit = 19,
            LowerLimit = -14
        };

        public static Level ExtremelyWrinkleBrain = new Level()
        {
            Id = 10,
            Name = "Extremely Wrinkle Brain",
            Amount = 200,
            Color = Color.DarkPurple,
            UpperLimit = 19,
            LowerLimit = -14
        };

        public static Level GalaxyBrain = new Level()
        {
            Id = 11,
            Name = "Galaxy Brain",
            Amount = 250,
            Color = Color.Magenta,
            UpperLimit = 20,
            LowerLimit = -15
        };

        public static IDictionary<uint, Level> AllLevels = new Dictionary<uint, Level>() {
            { 1, NeanderthalBrain },
            { 2, ExtremelySmoothBrain },
            { 3, VerySmoothBrain },
            { 4, SmoothBrain },
            { 5, UnwrinkledBrain },
            { 6, HasOneWrinkleBrain },
            { 7, KindaWrinkleBrain },
            { 8, WrinkleBrain },
            { 9, VeryWrinkleBrain },
            { 10, ExtremelyWrinkleBrain },
            { 11, GalaxyBrain }
        };

        public class Level
        {
            public uint Id;
            public string Name;
            public double Amount;
            public Color Color;
            public double UpperLimit;
            public double LowerLimit;
        }
    }
}
