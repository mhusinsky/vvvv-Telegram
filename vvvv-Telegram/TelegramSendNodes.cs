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
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputMessageContents;
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

        [Input("Disable Notification")]
        public IDiffSpread<bool> FDisableNotification;

        [Input("Reply to Message")]
        public IDiffSpread<int> FReplyId;

        [Input("ReplyMarkup")]
        public IDiffSpread<ReplyMarkupEnum> FReplyMarkupEnum;

        [Input("Keyboard")]
        public IDiffSpread<IReplyMarkup> FReplyMarkupKeyboard;

        [Input("Send", IsBang = true, DefaultValue = 0, IsSingle = true)]
        public IDiffSpread<bool> FSend;

        [Output("Sending")]
        public ISpread<bool> FSending;
        [Output("Send Time")]
        public ISpread<double> FTime;

        [Output("Error", Visibility = PinVisibility.OnlyInspector)]
        public ISpread<String> FError;


        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        //protected Spread<Task<Message>> FTask = new Spread<Task<Message>>();
        //protected Spread<CancellationTokenSource> FCts = new Spread<CancellationTokenSource>();
        //protected Spread<CancellationToken> ct = new Spread<CancellationToken>();
        //int TaskCount = 0;
        protected Spread<Stopwatch> FStopwatch = new Spread<Stopwatch>();
               
        public void OnImportsSatisfied()
        {
            FLogger.Log(LogType.Message, "Init TelegramBot Node");
        }

        public void Evaluate(int SpreadMax)
        {

            FStopwatch.SliceCount = FClient.SliceCount;
            FSending.SliceCount = FClient.SliceCount;
            FTime.SliceCount = FClient.SliceCount;
            FError.SliceCount = FClient.SliceCount;

            for (int i=0; i < FClient.SliceCount; i++)
            {
                if (FStopwatch[i] == null)
                    FStopwatch[i] = new Stopwatch();
                if(FSending[i])
                    FSending[i] = false;


                if (FSend[i])
                {
                    if(FClient[i] == null)
                    {
                        FLogger.Log(LogType.Debug, "Bot " + i + ": Cannot send message, no client available");
                    }
                    else if (FClient[i].IsConnected)
                    {
                        // STARTTASK
                        FStopwatch[i].Reset();
                        FStopwatch[i].Start();
                        sendMessageAsync(i);
                    }
                    else
                    {
                        FLogger.Log(LogType.Debug, "Bot \"" + i + "\": Cannot send message, client not connected");
                    }
                }

                FSending[i] = FTime[i] < FStopwatch[i].ElapsedMilliseconds / 1000.0;
                FTime[i] = FStopwatch[i].ElapsedMilliseconds / 1000.0;
            }

        }

        protected IReplyMarkup getReplyMarkup(int i)
        {
            var rm = FReplyMarkupEnum[i];
            switch ((int)rm)
            {
                case 0: return null;
                case 1:
                    var fr = new ForceReply();
                    fr.Force = true;
                    return fr;
                case 2: return new ReplyKeyboardHide();
                case 3: return FReplyMarkupKeyboard[i];
            }

            return null;
        }

        protected void printMessageSentSuccess(int i, MessageType mt)
        {
            FLogger.Log(LogType.Debug, "Bot \"" + FClient[i].Username + "\": Sent " + mt.ToString());
        }

        protected abstract Task sendMessageAsync(int i);

    }

    #region PluginInfo
    [PluginInfo(Name = "SendText", Category = "Telegram", Version = "", Help = "Sends Text Messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendTextNode : TelegramSendNode
    {
        [Input("Text", DefaultString = "Text")]
        public IDiffSpread<string> FTextMessage;

        protected override async Task sendMessageAsync(int i)
        {
            //if(FReplyMarkupKeyboard[0] == null)
                await FClient[i].BC.SendTextMessageAsync(FChatId[i], FTextMessage[i], false, FDisableNotification[i], FReplyId[i], getReplyMarkup(i), ParseMode.Default);
            //else
            //    await FClient[i].BC.SendTextMessageAsync(FChatId[i], FTextMessage[i], false, false, 0, FReplyMarkupKeyboard[0], ParseMode.Default);
                printMessageSentSuccess(i,m.Type);
            }

                printMessageSentSuccess(i, m.Type);
            }
            FStopwatch[i].Stop();
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendLocation", Category = "Telegram", Version = "", Help = "Sends location messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendLocationNode : TelegramSendNode
    {
        [Input("Longitude")]
        public IDiffSpread<double> FLong;
        [Input("Latitude")]
        public IDiffSpread<double> FLat;

        protected override async Task sendMessageAsync(int i)
        {
            await FClient[i].BC.SendLocationAsync(FChatId[i], (float)FLong[i], (float)FLat[i], false, 0, getReplyMarkup(i));
            
                printMessageSentSuccess(i, m.Type);
            }
                printMessageSentSuccess(i, m.Type);
            }
            FStopwatch[i].Stop();
        }
    }

    public abstract class TelegramSendFileNode : TelegramSendNode
    {
        [Input("File Name", DefaultString = "filename.xyz", StringType = StringType.Filename)]
        public IDiffSpread<string> FFileName;

        protected override async Task sendMessageAsync(int i)
        {
            await PerformChatActionAsync(i);

            using (var fileStream = new FileStream(FFileName[i], FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fts = new FileToSend(FFileName[i], fileStream);

                try
                {
                    await SendFileAsync(i, fts);
                    FStopwatch[i].Stop();
                    FLogger.Log(LogType.Debug, "Bot " + i + ": file sent...");
                    FError[i] = "";
                }
                catch (Exception e)
                {
                    FError[i] = e.Message;
                }
            }
        }

        protected abstract Task PerformChatActionAsync(int i);
        protected abstract Task SendFileAsync(int i, FileToSend fts);
    }

    #region PluginInfo
    [PluginInfo(Name = "SendDocument", Category = "Telegram", Version = "", Help = "Sends document files", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendDocumentNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;


        protected override async Task PerformChatActionAsync(int i)
        {
            await FClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadDocument);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            await FClient[i].BC.SendDocumentAsync(FChatId[i], fts, FCaption[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendPhoto", Category = "Telegram", Version = "", Help = "Sends images from files", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendPhotoNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;

        protected override async Task PerformChatActionAsync(int i)
        {
            await FClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadPhoto);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            await FClient[i].BC.SendPhotoAsync(FChatId[i], fts, FCaption[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendVideo", Category = "Telegram", Version = "", Help = "Sends video files (use .mp4 only)", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendVideoNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;

        [Input("Duration")]
        public IDiffSpread<int> FDuration;

        protected override async Task PerformChatActionAsync(int i)
        {
            await FClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadVideo);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            await FClient[i].BC.SendVideoAsync(FChatId[i], fts, FDuration[i], FCaption[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendAudio", Category = "Telegram", Version = "", Help = "Sends audio files (use .mp3 only)", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendAudioNode : TelegramSendFileNode
    {
        [Input("Duration")]
        public IDiffSpread<int> FDuration;

        [Input("Performer")]
        public IDiffSpread<string> FPerformer;

        [Input("Title")]
        public IDiffSpread<string> FTitle;

        protected override async Task PerformChatActionAsync(int i)
        {
            await FClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadAudio);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            await FClient[i].BC.SendAudioAsync(FChatId[i], fts, FDuration[i], FPerformer[i], FTitle[i],  FDisableNotification[i], FReplyId[i], getReplyMarkup(i));

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendPhotoRaw", Category = "Telegram", Version = "", Help = "Sends images from Raw Stream", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendPhotoRawNode : TelegramSendNode
    {
        [Input("File Name")]
        public ISpread<string> FFileName;

        [Input("Image")]
        public ISpread<Stream> FImage;

        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;


        readonly byte[] FBuffer = new byte[1024];
        
        protected override async Task sendMessageAsync(int i)
        {

            await FClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadPhoto);

            var inputStream = FImage[i];
            var outputStream = new MemoryStream();

            //reset the positions of the streams
            inputStream.Position = 0;
            outputStream.Position = 0;
            outputStream.SetLength(inputStream.Length);

            var numBytesToCopy = inputStream.Length;

            while (numBytesToCopy > 0)
            {
                //make sure we don't read more than we need or more than
                //our byte buffer can hold
                var chunkSize = (int)Math.Min(numBytesToCopy, FBuffer.Length);
                //the stream's read method returns how many bytes have actually
                //been read into the buffer
                var numBytesRead = inputStream.Read(FBuffer, 0, chunkSize);
                //in case nothing has been read we need to leave the loop
                //as we requested more than there was available
                if (numBytesRead == 0) break;
                //write the number of bytes read to the output stream
                outputStream.Write(FBuffer, 0, numBytesRead);
                //decrease the total amount of bytes we still need to read
                numBytesToCopy -= numBytesRead;
            }

            outputStream.Position = 0;  // seems to be nescessary. otherwise telegram bot api will not send any data

            var fts = new FileToSend(FFileName[i], outputStream);
            try
            {
                Message mesg = await FClient[i].BC.SendPhotoAsync(FChatId[i], fts, FCaption[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
                FStopwatch[i].Stop();
                printMessageSentSuccess(i, m.Type);
                FError[i] = "";
            }
            catch (Exception e)
            {
                FError[i] = e.Message;
            }
        }
    }
}
