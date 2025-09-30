namespace Multi1v1
{
    public static class Variables
    {
        // folder
        public static string assemblyFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string defaultFolderPath = assemblyFolderPath + "\\";
        public static string mainFolderPath = defaultFolderPath + @"Multi1v1\";
        public static string playersDataFolderPath = mainFolderPath + @"PlayersData\";

        // file
        public static string logFilePath = mainFolderPath + "log.txt";
        public static string playersDataFilePath = playersDataFolderPath + "database.txt";
        public static string configFilePath = mainFolderPath + "config.txt";

        // Dictionnary
        public static Dictionary<ulong, PlayerManager> connectedPlayers = [];
        public static Dictionary<ulong, int> playerToMatchIndex = [];
        public static Dictionary<int, List<ulong>> matches = [];


 
        // string
        public static string commandSymbol;

        // ulong
        public static ulong clientId;

        // float
        public static float kFactor, eloScalingFactor, initialElo;

        // int
        public static int gamemodeId = 0;

        // bool
        public static bool ranked, gameHasStarted, configStart = true;
    }
}
