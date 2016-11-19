using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using System.Threading;
using System.Threading.Tasks;

//using System.IO;
using System.ComponentModel.Composition;

using System.Diagnostics;


using VVVV.PluginInterfaces.V2;
using VVVV.Core.Logging;

using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
//using Telegram.Bot.Types.InlineQueryResults;
//using Telegram.Bot.Types.InputMessageContents;
using Telegram.Bot.Types.ReplyMarkups;

namespace VVVV.Nodes
{
    #region PluginInfo
    [PluginInfo(Name = "BotClient", Category = "Telegram", Version = "0.1", Help = "Provides Communication with a TelegramBot", Credits = "Based on telegram.bo", Tags = "Network, Telegram, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramBotNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("API-Key", DefaultString = "YOURKEY")]
        public IDiffSpread<string> FApiKey;

        [Input("ChatId", DefaultString = "ChatID")]
        public IDiffSpread<string> FChatId;

        [Input("Connect", IsBang = true, DefaultValue = 0, IsSingle = true)]
        public IDiffSpread<bool> FConnect;

        [Input("Execute", IsBang = true, DefaultValue = 0, IsSingle = true)]
        public IDiffSpread<bool> FExecute;

        [Input("Disconnect", IsBang = true, DefaultValue = 0, IsSingle = true)]
        public IDiffSpread<bool> FCancel;

        [Output("BotClient")]
        public ISpread<BotClient> FBotClient;

        [Output("Bot Name")]
        public ISpread<String> FBotName;
        [Output("Connect Time")]
        public ISpread<double> FConnectTime;
        [Output("Connected")]
        public ISpread<bool> FConnected;
        [Output("Text Sent", IsBang = true)]
        public ISpread<bool> FTextSent;
        [Output("Send Time")]
        public ISpread<double> FTextTime;


        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        readonly Spread<Task<User>> FTasks = new Spread<Task<User>>();
        readonly Spread<Task<Message>> FChatTask = new Spread<Task<Message>>();
        readonly Spread<CancellationTokenSource> FCts = new Spread<CancellationTokenSource>();
        readonly Spread<CancellationToken> ct = new Spread<CancellationToken>();
        //int TaskCount = 0;
        readonly Spread<Stopwatch> FStopwatch = new Spread<Stopwatch>();
        readonly Spread<Stopwatch> FTextStopwatch = new Spread<Stopwatch>();

        readonly Spread<TelegramBotClient> FClient = new Spread<TelegramBotClient>();

        public void OnImportsSatisfied()
        {
            FLogger.Log(LogType.Message, "Init TelegramBot Node");
        }

        public void Dispose()
        {
            // Should this plugin get deleted by the user or should vvvv shutdown
            // we need to wait until all still running tasks ran to a completion
            // state.
            for (int i = 0; i < FTasks.SliceCount; i++)
            {
                FLogger.Log(LogType.Message, "Dispose task:" + i);
                CancelRunningTasks(i);
            }
        }


        public void Evaluate(int SpreadMax)
        {
            FTasks.SliceCount = FApiKey.SliceCount;
            FChatTask.SliceCount = FApiKey.SliceCount;
            FCts.SliceCount = FApiKey.SliceCount;
            ct.SliceCount = FApiKey.SliceCount;
            FStopwatch.SliceCount = FApiKey.SliceCount;
            FTextStopwatch.SliceCount = FApiKey.SliceCount;

            FClient.SliceCount = FApiKey.SliceCount;

            FBotName.SliceCount = FApiKey.SliceCount;
            FConnected.SliceCount = FApiKey.SliceCount;

            FBotClient.SliceCount = FApiKey.SliceCount;

            for (int i=0; i < FClient.SliceCount; i++)
            {
                if (FCancel[i])
                {
                    CancelRunningTasks(i);
                    FClient[i].StopReceiving();
                    FConnected[i] = false;
                    FStopwatch[i].Stop();
                    FStopwatch[i].Reset();
//                  FBotClient[i]
                }

                if (FConnect[i])
                {
                    FClient[i] = new TelegramBotClient(FApiKey[i]);
                    FBotClient[i] = new BotClient();

                    FStopwatch[i] = new Stopwatch();

                    //FClient[i].OnMessage += BotOnMessageReceived;
                    //FClient[i].OnMessageEdited += BotOnMessageReceived;

                    FConnected[i] = false;
                    
                    FStopwatch[i].Reset();
                    FStopwatch[i].Start();
                    FTasks[i] = Task<User>.Factory.StartNew( () => FClient[i].GetMeAsync().Result );

                    FLogger.Log(LogType.Debug, "Connecting to Bot at index " + i);
                    FLogger.Log(LogType.Debug, "Task " + FTasks[i].Status.ToString());
                }

                if (FTasks[i] == null)
                { }
                else if (FTasks[i].Status == TaskStatus.RanToCompletion)
                {
                    //FLogger.Log(LogType.Debug, "Task " + i + ": " + FTasks[i].Status.ToString());
                    if (FStopwatch[i].IsRunning)
                    {
                        FStopwatch[i].Stop();
                        FConnectTime[i] = FStopwatch[i].ElapsedMilliseconds / 1000.0;
                        FBotName[i] = FTasks[i].Result.Username;
                        FClient[i].StartReceiving();
                        FConnected[i] = true;
                    }
                    else
                    {
                        FConnectTime[i] = FStopwatch[i].ElapsedMilliseconds / 1000.0;
                    }
                }
                else if (FTasks[i].Status == TaskStatus.Running)
                {
                    //FLogger.Log(LogType.Debug, "Task " + i + ": " + FTasks[i].Status.ToString());
                }

                if (FExecute[i])
                {
                    if (FConnected[i])
                    {
                        FTextStopwatch[i] = new Stopwatch();
                        FTextStopwatch[i].Reset();
                        FTextStopwatch[i].Start();
                        FChatTask[i] = Task<Message>.Factory.StartNew(() => FClient[i].SendTextMessageAsync(268548789, "asdf", false, false, 0, null,ParseMode.Default).Result);
                    }
                    else
                    {
                        FLogger.Log(LogType.Debug, "Bot" + ": Cannot Send, client not connected");
                    }
                }

                if (FChatTask[i] == null)
                { }
                else if (FChatTask[i].Status == TaskStatus.RanToCompletion)
                {
                    //FLogger.Log(LogType.Debug, "Task " + i + ": " + FTasks[i].Status.ToString());
                    if (FTextStopwatch[i].IsRunning)
                    {
                        FTextStopwatch[i].Stop();
                        FTextTime[i] = FStopwatch[i].ElapsedMilliseconds / 1000.0;
                        FTextSent[i] = true;
                    }
                    else
                    {
                        FTextTime[i] = FTextStopwatch[i].ElapsedMilliseconds / 1000.0;
                        FTextSent[i] = false;
                    }
                }
                else if (FChatTask[i].Status == TaskStatus.Running)
                {
                    //FLogger.Log(LogType.Debug, "Task " + i + ": " + FTasks[i].Status.ToString());
                }

                if(FBotClient[i] == null)
                {
                    FBotClient[i] = new BotClient();
                }
                FBotClient[i].BC = FClient[i];
                FBotClient[i].IsConnected = FConnected[i];
            }

        }


//        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
//        {
//            var message = messageEventArgs.Message;

//            if (message == null || message.Type != MessageType.TextMessage) return;

            
//                var usage = @"Usage:
///inline   - send inline keyboard
///keyboard - send custom keyboard
///photo    - send a photo
///request  - request location or contact
//";
                
//                await Bot.SendTextMessageAsync(message.Chat.Id, usage,
//                    replyMarkup: new ReplyKeyboardHide());
            
//        }

        // Worker and Helper Methods 
        private void CancelRunningTasks(int index)
        {
            if (FCts[index] != null)
            {
                // All our running tasks use the cancellation token of this cancellation
                // token source. Once we call cancel the ct.ThrowIfCancellationRequested()
                // will throw and the task will transition to the canceled state.
                FCts[index].Cancel();

                // Dispose the cancellation token source and set it to null so we know
                // to setup a new one in a next frame.
                FCts[index].Dispose();
                FCts[index] = null;
            }
        }
    }

    public class BotClient
    {
        public TelegramBotClient BC { get; set; }
        public bool IsConnected { get; set; }
    }

}
