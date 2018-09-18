using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace BluesScrapeClient
{
    public enum GameStatuses { NotStarted, InAction, CriticalAction, Intermission, Final = 7}

    class BluesScraper
    {
        string _url;
        bool _bluesHome;
        HttpClient _httpClient;
        JToken _gameData, _liveData;

        public BluesScraper(int gameCode)
        {
            _url = string.Format("https://statsapi.web.nhl.com/api/v1/game/{0}/feed/live", gameCode);
            _httpClient = new HttpClient();
        }

        public async Task<Tuple<GameInfo, GameStatuses>> RefreshData()
        {
            try
            {
                //TODO: better error handlin
                string rawJson = await _httpClient.GetStringAsync(_url);
                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    throw new Exception();
                }

                var jsonObj = JObject.Parse(rawJson);
                _gameData = jsonObj["gameData"];
                _liveData = jsonObj["liveData"];

                var gameInfo = ExtractData();
                string gameStatus = GetJsonValue(_gameData["status"]["statusCode"]);
                bool fail = !Enum.TryParse(gameStatus, out GameStatuses status);

                if (fail)
                {
                    //Failsafe
                    status = GameStatuses.Final;
                }

                return Tuple.Create(gameInfo, status);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private GameInfo ExtractData()
        {
            GameInfo gameInfo = new GameInfo();

            //Live Data
            var lineScore = _liveData["linescore"];
            gameInfo.BluesHome = (GetJsonValue(_gameData["teams"]["home"]["id"]) == "19");
            gameInfo.HomeScore = int.Parse(GetJsonValue(lineScore["teams"]["home"]["goals"]));
            gameInfo.AwayScore = int.Parse(GetJsonValue(lineScore["teams"]["away"]["goals"]));
            gameInfo.HomeSOG = int.Parse(GetJsonValue(lineScore["teams"]["home"]["shotsOnGoal"]));
            gameInfo.AwaySOG = int.Parse(GetJsonValue(lineScore["teams"]["away"]["shotsOnGoal"]));
            gameInfo.Period = int.Parse(GetJsonValue(lineScore["currentPeriod"]));
            long.TryParse(GetJsonValue(lineScore["currentPeriodTimeRemaining"]), out gameInfo.TimeRemaining);
            gameInfo.TimeRemaining = (gameInfo.TimeRemaining == 0) ? -1 : gameInfo.TimeRemaining;

            return gameInfo;
        }

        private string GetJsonValue(JToken token)
        {
            try
            {
                return token.Value<string>().Trim();
            }
            //not sure if needed but why not
            catch (Exception)
            {
                return null;
            }
        }

        public bool BluesHome { get => _bluesHome; set => _bluesHome = value; }

    }

    struct GameInfo
    {
        public bool BluesHome;
        public int HomeScore;
        public int HomeSOG;
        public int AwayScore;
        public int AwaySOG;
        public long TimeRemaining;
        public int Period;
    }
}
