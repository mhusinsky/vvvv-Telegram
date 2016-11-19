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
        readonly Spread<Stopwatch> FStopwatch = new Spread<Stopwatch>();
        
        
        public void OnImportsSatisfied()
        {
            FLogger.Log(LogType.Message, "Init TelegramBot Node");
        }

        public void Dispose()
        {
            // Should this plugin get deleted by the user or should vvvv shutdown
            // we need to wait until all still running tasks ran to a completion
            // state.
            for (int i = 0; i < FTask.SliceCount; i++)
            {
                FLogger.Log(LogType.Message, "Dispose task:" + i);
                CancelRunningTasks(i);
            }
        }


        public void Evaluate(int SpreadMax)
        {
            FTask.SliceCount = FClient.SliceCount;
            FCts.SliceCount = FClient.SliceCount;
            ct.SliceCount = FClient.SliceCount;
            FStopwatch.SliceCount = FClient.SliceCount;
            
            for (int i=0; i < FClient.SliceCount; i++)
            {
                if (FCancel[i])
                {
                    CancelRunningTasks(i);
                    FClient[i].BC.StopReceiving();
                    FStopwatch[i].Stop();
                    FStopwatch[i].Reset();
                }

                
                if (FSend[i])
                {
                    if (FClient[i].IsConnected)
                    {
                        FStopwatch[i] = new Stopwatch();
                        FStopwatch[i].Reset();
                        FStopwatch[i].Start();
                        // STARTTASK
                        sendMessage(i);
                    }
                    else
                    {
                        FLogger.Log(LogType.Debug, "Bot " + i + ": Cannot Send Text, client not connected");
                    }
                }

                if (FTask[i] == null)
                { }
                else if (FTask[i].Status == TaskStatus.RanToCompletion)
                {
                    //FLogger.Log(LogType.Debug, "Task " + i + ": " + FTasks[i].Status.ToString());
                    if (FStopwatch[i].IsRunning)
                    {
                        FStopwatch[i].Stop();
                        FTime[i] = FStopwatch[i].ElapsedMilliseconds / 1000.0;
                        FSent[i] = true;
                    }
                    else
                    {
                        FTime[i] = FStopwatch[i].ElapsedMilliseconds / 1000.0;
                        FSent[i] = false;
                    }
                }
                else if (FTask[i].Status == TaskStatus.Running)
                {
                    //FLogger.Log(LogType.Debug, "Task " + i + ": " + FTasks[i].Status.ToString());
                }
            }

        }

        protected abstract void sendMessage(int i);

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

    #region PluginInfo
    [PluginInfo(Name = "SendText", Category = "Telegram", Version = "0.1", Help = "Sends Text Messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendTextNode : TelegramSendNode
    {
        [Input("Text", DefaultString = "Your Message")]
        public IDiffSpread<string> FTextMessage;

        protected override async void sendMessage(int i)
        {
            FTask[i] = Task<Message>.Factory.StartNew(() => FClient[i].BC.SendTextMessageAsync(FChatId[i], FTextMessage[i], false, false, 0, null, ParseMode.Default).Result);
            //await FClient[i].BC.SendTextMessageAsync(FChatId[i], FTextMessage[i], false, false, 0, null, ParseMode.Default);
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

        protected override async void sendMessage(int i)
        {

            await FClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadPhoto);
            
            // original example code
            using (var fileStream = new FileStream(FFileName[i], FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fts = new FileToSend(FFileName[i], fileStream);

                // Does not work - Cannot read from File that is not opened. (threading is probably the reason)
                //FTask[i] = Task<Message>.Factory.StartNew(() => FClient[i].BC.SendPhotoAsync(FChatId[i], fts, FCaption[i]).Result);

                await FClient[i].BC.SendPhotoAsync(FChatId[i], fts, FCaption[i]);

                // Exception when trying to access the stream from vvvv directly
                // Exception: Bytes to be written to the stream exceed the Content-Length bytes size specified
            }
        }
    }
}
