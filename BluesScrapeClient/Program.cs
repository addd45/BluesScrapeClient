using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Client;
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;

namespace BluesScrapeClient
{
    class Program
    {
        static BluesScraper _scraper;
        static IMqttClient _mqttClient;
        static IMqttClientOptions _mqttOptions;
        static readonly string _mqttScoreTopic = "Other/BluesScore";
        static readonly string _mqttSettingsTopic = "Other/BluesSettings";

        static async Task<int> Main(string[] args)
        {
            var mqttInit = InitializeMqtt("192.168.0.111", "homeassistant", "sb4517");
            string arg1 = args.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(arg1))
            {
                Console.WriteLine("No args provided. exiting");
                return -1;
            }

            int gameID = int.Parse(arg1);
            Console.WriteLine($"Running with gameID: {gameID}");
            _scraper = new BluesScraper(gameID);

            _mqttClient = await mqttInit;
            await Execute();

            return 0;
        }

        static async Task<IMqttClient> InitializeMqtt(string addr, string user, string pw)
        {
            var factory = new MqttFactory();
            var ret = factory.CreateMqttClient();
            ret.Disconnected += Mqtt_Disconnected;

            _mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId("BluesScraper")
                .WithTcpServer(addr)
                .WithCredentials(user, pw)
                //.WithCleanSession()
                .Build();

            try
            {
                await ret.ConnectAsync(_mqttOptions);
            }
            catch(MqttConnectingFailedException e)
            {
                //TODO: make decisions
                switch (e.ReturnCode)
                {
                        //idk
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionAccepted:
                        break;
                        //protocol issue
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedUnacceptableProtocolVersion:
                        Console.WriteLine("Protocol Issue with connecting to MQTT. Check connection settings");
                        throw;
                        //idk
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedIdentifierRejected:
                        break;
                        //server problem
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedServerUnavailable:
                        break;
                        //auth problem
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword:
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedNotAuthorized:
                        Console.WriteLine("Error Authenticating to MQTT. Check that shit");
                        throw;
                    default:
                        break;
                }
            }
            return ret;
        }

        static async Task Execute()
        {
            while (true)
            {
                var data = await _scraper.RefreshData();
                
                TimeSpan delay = (GetDelayTime(data.Item2));
                await SendData(data.Item1);

                //End execution 
                if (delay == default)
                {
                    break;
                }

                Console.WriteLine($"Sleeping for {delay} ");
                Console.WriteLine("Game status " + data.Item2.ToString());
                Thread.Sleep(delay);
            }
        }

        private static TimeSpan GetDelayTime(GameStatuses status)
        {
            switch (status)
            {
                case GameStatuses.CriticalAction:
                    return TimeSpan.FromSeconds(1);
                case GameStatuses.Intermission:
                case GameStatuses.NotStarted:
                    return TimeSpan.FromSeconds(90);
                case GameStatuses.Preview:
                    return TimeSpan.FromSeconds(30);
                case GameStatuses.InAction:
                    return TimeSpan.FromSeconds(5);
                case GameStatuses.Final:
                default:
                    return default;
            }

        }

        static async Task SendData(GameInfo gameInfo)
        {
            string json = JsonConvert.SerializeObject(gameInfo);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_mqttScoreTopic)
                .WithAtMostOnceQoS()
                .WithPayload(json)
                .Build();
            try
            {
                Console.WriteLine($"Sending Message {Environment.NewLine} {json}");
                await _mqttClient.PublishAsync(message);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error publishing mqtt message");
            }
        }


        private static async void Mqtt_Disconnected(object sender, MqttClientDisconnectedEventArgs e)
        {
            //Never was connected in the first place
            if (!e.ClientWasConnected)
            {
                throw e.Exception;
            }
            else
            {
                //TODO: Dynamic
                for(int i=0; i<3; i++)
                {
                    await _mqttClient.ConnectAsync(_mqttOptions);
                    if (_mqttClient.IsConnected)
                    {
                        break;
                    }
                    else { Thread.Sleep(690); }
                }
            }
        }

    }
}
