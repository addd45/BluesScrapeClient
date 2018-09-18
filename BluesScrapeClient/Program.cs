using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Client;
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BluesScrapeClient
{
    class Program
    {
        static BluesScraper _scraper;
        static IMqttClient _mqttClient;
        static readonly string _mqttTopic = "Other/BluesScore";

        static async Task Main(string[] args)
        {
            var mqttInit = InitializeMqtt("192.168.0.111", "homeassistant", "sb4517");

            int temp = 2017020245;
            _scraper = new BluesScraper(temp);

            _mqttClient = await mqttInit;
            await Execute();
        }

        static async Task<IMqttClient> InitializeMqtt(string addr, string user, string pw)
        {
            var factory = new MqttFactory();
            var ret = factory.CreateMqttClient();
            ret.Disconnected += Mqtt_Disconnected;

            var options = new MqttClientOptionsBuilder()
                .WithClientId("BluesScraper")
                .WithTcpServer(addr)
                .WithCredentials(user, pw)
                //.WithCleanSession()
                .Build();

            try
            {
                await ret.ConnectAsync(options);
            }
            catch(MqttConnectingFailedException e)
            {
                switch (e.ReturnCode)
                {
                        //idk
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionAccepted:
                        break;
                        //protocol issue
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedUnacceptableProtocolVersion:
                        break;
                        //idk
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedIdentifierRejected:
                        break;
                        //server problem
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedServerUnavailable:
                        break;
                        //auth problem
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword:
                    case MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedNotAuthorized:
                        break;
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
                    return TimeSpan.FromSeconds(60);
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
                .WithTopic(_mqttTopic)
                .WithAtMostOnceQoS()
                .WithPayload(json)
                .Build();
            try
            {
                await _mqttClient.PublishAsync(message);
            }
            catch(Exception e)
            {

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
                //TODO retry logic
            }
        }

    }
}
