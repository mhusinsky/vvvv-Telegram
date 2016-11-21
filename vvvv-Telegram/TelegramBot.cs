﻿using System;
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

        [Input("Disconnect", IsBang = true, DefaultValue = 0, IsSingle = true)]
        public IDiffSpread<bool> FDisconnect;

        [Output("BotClient")]
        public ISpread<BotClient> FBotClient;

        [Output("Bot Name")]
        public ISpread<String> FBotName;
        [Output("Connect Time")]
        public ISpread<double> FConnectTime;
        [Output("Connected")]
        public ISpread<bool> FConnected;


        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        readonly Spread<Task<User>> FTasks = new Spread<Task<User>>();
        readonly Spread<CancellationTokenSource> FCts = new Spread<CancellationTokenSource>();
        readonly Spread<CancellationToken> ct = new Spread<CancellationToken>();
        //int TaskCount = 0;
        readonly Spread<Stopwatch> FStopwatch = new Spread<Stopwatch>();
        
        //readonly Spread<TelegramBotClient> FClient = new Spread<TelegramBotClient>();

        public void OnImportsSatisfied()
        {
            FLogger.Log(LogType.Message, "Init TelegramBot Node");
        }



        public void Evaluate(int SpreadMax)
        {
            FTasks.SliceCount = FApiKey.SliceCount;
            FCts.SliceCount = FApiKey.SliceCount;
            ct.SliceCount = FApiKey.SliceCount;
            FStopwatch.SliceCount = FApiKey.SliceCount;
            
            FBotName.SliceCount = FApiKey.SliceCount;
            FConnected.SliceCount = FApiKey.SliceCount;

            FBotClient.SliceCount = FApiKey.SliceCount;

            for (int i=0; i < FBotClient.SliceCount; i++)
            {

                if (FConnect[i])
                {
                    FBotClient[i] = new BotClient(FApiKey[i]);

                    FStopwatch[i] = new Stopwatch();
                    FConnected[i] = false;
                    
                    FStopwatch[i].Reset();
                    FStopwatch[i].Start();

                    FBotClient[i].ConnectAsync();
                    FLogger.Log(LogType.Debug, "Connecting to Bot at index " + i);
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
                    }

                    if (FDisconnect[i])
                    {
                        FBotClient[i].Disconnect();
                    }

                    FConnected[i] = FBotClient[i].IsConnected;
                }
            }
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

        public Message lastMessage;

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

        public Message RetrieveTextMessage()
        {
            if (lastMessage == null || lastMessage.Type != MessageType.TextMessage) return null;

            if (ReceivedMessage)
            {
                ReceivedMessage = false;
                return lastMessage;
            }
            else
            {
                return null;
            }
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)   // TODO: remove async, handle multiple messages
        {
            ReceivedMessage = true;
            
            var message = messageEventArgs.Message;
            this.lastMessage = message;
            
//            if (message == null || message.Type != MessageType.TextMessage) return;

//            var usage = @"Usage:
///inline   - send inline keyboard
///keyboard - send custom keyboard
///photo    - send a photo
///request  - request location or contact
//";

//            await BC.SendTextMessageAsync(message.Chat.Id, usage,
//                replyMarkup: new ReplyKeyboardHide());
        }
    }

}
