using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.IO;
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
    public abstract class TelegramSendNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("Bots")]
        public ISpread<BotClient> FClient;

        [Input("ChatId")]
        public IDiffSpread<int> FChatId;

        [Input("Send", IsBang = true, DefaultValue = 0, IsSingle = true)]
        public IDiffSpread<bool> FSend;

        [Input("Disconnect", IsBang = true, DefaultValue = 0, IsSingle = true)]
        public IDiffSpread<bool> FCancel;

        [Output("Bot Name")]
        public ISpread<String> FBotName;
        [Output("Sent", IsBang = true)]
        public ISpread<bool> FSent;
        [Output("Send Time")]
        public ISpread<double> FTime;


        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        protected Spread<Task<Message>> FTask = new Spread<Task<Message>>();
        protected Spread<CancellationTokenSource> FCts = new Spread<CancellationTokenSource>();
        protected Spread<CancellationToken> ct = new Spread<CancellationToken>();
        //int TaskCount = 0;
        protected Spread<Stopwatch> FStopwatch = new Spread<Stopwatch>();
        
        
        public void OnImportsSatisfied()
        {
            FLogger.Log(LogType.Message, "Init TelegramBot Node");
        }

        public void Dispose()
        {

        }


        public void Evaluate(int SpreadMax)
        {

            FStopwatch.SliceCount = FClient.SliceCount;
            FSent.SliceCount = FClient.SliceCount;


            for (int i=0; i < FClient.SliceCount; i++)
            {
                if (FStopwatch[i] == null) FStopwatch[i] = new Stopwatch();
                if(FSent[i]) FSent[i] = false;


                if (FSend[i])
                {
                    if (FClient[i].IsConnected)
                    {
                        // STARTTASK
                        FStopwatch[i].Reset();
                        FStopwatch[i].Start();
                        sendMessageAsync(i);
                    }
                    else
                    {
                        FLogger.Log(LogType.Debug, "Bot " + i + ": Cannot Send Text, client not connected");
                    }
                }
                
                FTime[i] = FStopwatch[i].ElapsedMilliseconds;
            }

        }

        protected abstract Task sendMessageAsync(int i);

    }

    #region PluginInfo
    [PluginInfo(Name = "SendText", Category = "Telegram", Version = "0.1", Help = "Sends Text Messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendTextNode : TelegramSendNode
    {
        [Input("Text", DefaultString = "Your Message")]
        public IDiffSpread<string> FTextMessage;

        protected override async Task sendMessageAsync(int i)
        {
            //FTask[i] = Task<Message>.Factory.StartNew(() => FClient[i].BC.SendTextMessageAsync(FChatId[i], FTextMessage[i], false, false, 0, null, ParseMode.Default).Result);
            await FClient[i].BC.SendTextMessageAsync(FChatId[i], FTextMessage[i], false, false, 0, null, ParseMode.Default);
            FStopwatch[i].Stop();
            FSent[i] = true;    // TODO: only do when sending was actually successful
        }

    }

    #region PluginInfo
    [PluginInfo(Name = "SendPhoto", Category = "Telegram", Version = "0.1", Help = "Sends Images", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendPhotoNode : TelegramSendNode
    {
        [Input("FileName", DefaultString = "filename.jpg", StringType =StringType.Filename)]
        public IDiffSpread<string> FFileName;

        [Input("FCaption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;

        protected override async Task sendMessageAsync(int i)
        {

            await FClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadPhoto);
            
            using (var fileStream = new FileStream(FFileName[i], FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fts = new FileToSend(FFileName[i], fileStream);

                await FClient[i].BC.SendPhotoAsync(FChatId[i], fts, FCaption[i]);
                FStopwatch[i].Stop();
                FSent[i] = true;    // TODO: only do when sending was actually successful

                // Exception when trying to access the stream from vvvv directly
                // Exception: Bytes to be written to the stream exceed the Content-Length bytes size specified
            }
        }
    }
}
