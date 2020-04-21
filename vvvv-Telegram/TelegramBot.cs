using System;
using System.Collections.Generic;
using System.Linq;
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
    [PluginInfo(Name = "BotClient", Category = "Telegram", Version = "", Help = "Connects to Telegram with an API-Key", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramBotNode : IPluginEvaluate, IDisposable
    {
        #region fields & pins
        [Input("API-Key", DefaultString = "YOURKEY")]
        public IDiffSpread<string> FApiKey;

        [Input("Connect", IsBang = true, DefaultValue = 0)]
        public IDiffSpread<bool> FConnect;

        [Input("Disconnect", IsBang = true, DefaultValue = 0)]
        public IDiffSpread<bool> FDisconnect;

        [Output("BotClient")]
        public ISpread<BotClient> FBotClient;

        [Output("Bot Name")]
        public ISpread<String> FBotName;
        [Output("Connect Time")]
        public ISpread<double> FConnectTime;
        [Output("Connected")]
        public ISpread<bool> FConnected;

        [Output("Error", Visibility = PinVisibility.OnlyInspector)]
        public ISpread<String> FError;


        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        //readonly Spread<Task<User>> FTasks = new Spread<Task<User>>();
        //readonly Spread<CancellationTokenSource> FCts = new Spread<CancellationTokenSource>();
        //readonly Spread<CancellationToken> ct = new Spread<CancellationToken>();
        ////int TaskCount = 0;
        readonly Spread<Stopwatch> FStopwatch = new Spread<Stopwatch>();


        public void Evaluate(int SpreadMax)
        {
            //FTasks.SliceCount = FApiKey.SliceCount;
            //FCts.SliceCount = FApiKey.SliceCount;
            //ct.SliceCount = FApiKey.SliceCount;

            FStopwatch.SliceCount = FApiKey.SliceCount;
            
            FBotName.SliceCount = FApiKey.SliceCount;
            FConnectTime.SliceCount = FApiKey.SliceCount;
            FConnected.SliceCount = FApiKey.SliceCount;

            FBotClient.SliceCount = FApiKey.SliceCount;
            FError.SliceCount = FApiKey.SliceCount;

            for (int i=0; i < FBotClient.SliceCount; i++)
            {
                if (FBotClient[i] != null)  // TODO: move this block after connect?
                {
                    // deal with old messages
                    var last = FBotClient[i].Messages;
                    for (int m = last.Count-1; m >= 0; m--)
                    {
                        if (last[m].IsNew)
                        {
                            last[m].IsNew = false;
                        }
                        else
                        {
                            last.RemoveAt(m);   // remove message from last frame
                        }
                    }
                    // deal with old callbacks
                    var lastCallbacks = FBotClient[i].Callbacks;
                    for (int c = lastCallbacks.Count - 1; c >= 0; c--)
                    {
                        if (lastCallbacks[c].IsNew)
                        {
                            lastCallbacks[c].IsNew = false;
                        }
                        else
                        {
                            lastCallbacks.RemoveAt(c);   // remove callback object from last frame
                        }
                    }
                }
                
                
                if (FConnect[i])
                {
                    try
                    {
                        FBotClient[i] = new BotClient(FApiKey[i]);
                        FError[i] = "";

                        FStopwatch[i] = new Stopwatch();
                        FConnected[i] = false;

                        FStopwatch[i].Reset();
                        FStopwatch[i].Start();

                        FBotClient[i].ConnectAsync();
                        FLogger.Log(LogType.Debug, "Connecting to Bot at index " + i);
                    }
                    catch (Exception e)
                    {
                        FError[i] = e.Message;
                        FLogger.Log(LogType.Debug, "Error connecting to Bot at index " + i);
                        FLogger.Log(LogType.Debug, e.Message);
                    }
                }

                if(FBotClient[i] != null)
                {
                    
                    if (FBotClient[i].IsConnected && !FBotClient[i].IsReceiving)
                    {
                        FBotClient[i].StartReceiving();
                        FLogger.Log(LogType.Debug, "Started Receiving Bot " + i);
                        FStopwatch[i].Stop();
                        FConnectTime[i] = FStopwatch[i].ElapsedMilliseconds / 1000.0;
                        FBotName[i] = FBotClient[i].Username;
                        FError[i] = "";
                    }

                    if (FDisconnect[i])
                    {
                        FBotClient[i].Disconnect();
                    }

                    FConnected[i] = FBotClient[i].IsConnected;
                }
            }
        }

        public void Dispose()
        {
            foreach (var botClient in FBotClient)
            {
                botClient.Disconnect();
            }
        }
    }

    public class TelegramFile
    {
        public Telegram.Bot.Types.FileBase file;
        public TelegramBotClient botClient;

        public TelegramFile(Telegram.Bot.Types.FileBase f, TelegramBotClient bc)
        {
            file = f;
            botClient = bc;
        }
    }

    // contains the message object and a flag to mark whether it is new or old (and therefore should be deleted)
    public class VTelegramMessage
    {
        public Message message;
        public DateTime created;
        public bool IsNew;

        public VTelegramMessage(Message m)
        {
            message = m;
            IsNew = true;
            created = m.Date;
        }
    }
    public class VTelegramCallback
    {
        public CallbackQuery callback;
        public bool IsNew;

        public VTelegramCallback(CallbackQuery c)
        {
            callback = c;
            IsNew = true;
        }
    }

    public class BotClient
    {
        public TelegramBotClient BC;
        private User bcUser;
        public bool IsConnected = false;
        
        public String Username { get { return bcUser.Username; } }
        public bool IsReceiving  { get { return BC.IsReceiving; } }
        public bool ReceivedMessage = false;

        public List<VTelegramMessage> Messages = new List<VTelegramMessage>();
        public List<VTelegramCallback> Callbacks = new List<VTelegramCallback>();

        public BotClient (string ApiKey)
        {
            BC = new TelegramBotClient(ApiKey);
        }


        public async void ConnectAsync()
        {
            var user =  await BC.GetMeAsync();

            if(user.Username.Length > 0)
            {
                bcUser = user;
                IsConnected = true;

                BC.OnMessage += BotOnMessageReceived;
                BC.OnMessageEdited += BotOnMessageReceived;
                BC.OnCallbackQuery += BotOnCallbackQueryReceived;
            }
        }

        public void Disconnect()
        {
            BC.StopReceiving();
            IsConnected = false;
        }

        public void StartReceiving()
        {
            // callback methods here
            BC.StartReceiving();
        }

        private void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            this.Messages.Add(new VTelegramMessage (messageEventArgs.Message));
        }

        private void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            this.Callbacks.Add(new VTelegramCallback(callbackQueryEventArgs.CallbackQuery));
        }
    }

}
