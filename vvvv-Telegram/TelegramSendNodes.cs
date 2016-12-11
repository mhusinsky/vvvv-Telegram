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
        [Input("Bots", Order = -100)]
        public ISpread<BotClient> FBotClient;

        [Input("ChatId", Order = -90)]
        public IDiffSpread<int> FChatId;

        [Input("Disable Notification")]
        public IDiffSpread<bool> FDisableNotification;

        [Input("Reply to Message")]
        public IDiffSpread<int> FReplyId;

        [Input("ReplyMarkup")]
        public IDiffSpread<ReplyMarkupEnum> FReplyMarkupEnum;

        [Input("Keyboard")]
        public IDiffSpread<IReplyMarkup> FReplyMarkupKeyboard;

        [Input("Send", IsBang = true, DefaultValue = 0)]
        public IDiffSpread<bool> FSend;

        [Output("Send Time")]
        public ISpread<double> FTime;

        [Output("Success", IsBang = true)]
        public ISpread<bool> FSuccess;
        

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

        public void Evaluate(int SpreadMax)
        {

            FStopwatch.SliceCount = FBotClient.SliceCount;
            FSuccess.SliceCount = FBotClient.SliceCount;
            FTime.SliceCount = FBotClient.SliceCount;
            FError.SliceCount = FBotClient.SliceCount;

            for (int i=0; i < FBotClient.SliceCount; i++)
            {
                if (FStopwatch[i] == null)
                    FStopwatch[i] = new Stopwatch();
                if(FSuccess[i])
                    FSuccess[i] = false;


                if (FSend[i])
                {
                    if(FBotClient[i] == null)
                    {
                        FLogger.Log(LogType.Debug, "Bot " + i + ": Cannot send message, no client available");
                    }
                    else if (FBotClient[i].IsConnected)
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

                double currentTime = FStopwatch[i].ElapsedMilliseconds / 1000.0;
                if (FTime[i] < currentTime && !FStopwatch[i].IsRunning)     // stopwatch was stopped somewhere in between
                {
                    FSuccess[i] = true;
                }
                else
                {
                    FSuccess[i] = false;
                }
                //FSending[i] = FTime[i] < FStopwatch[i].ElapsedMilliseconds / 1000.0;
                FTime[i] = currentTime;
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
            FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Sent " + mt.ToString());
        }

        protected abstract Task sendMessageAsync(int i);

    }

    #region PluginInfo
    [PluginInfo(Name = "SendText", Category = "Telegram", Version = "", Help = "Sends Text Messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendTextNode : TelegramSendNode
    {
        [Input("Text", DefaultString = "Text")]
        public ISpread<ISpread<string>> FTextMessage;

        protected override async Task sendMessageAsync(int i)
        {
            foreach(string s in FTextMessage[i])
            {
                Message m = await FBotClient[i].BC.SendTextMessageAsync(FChatId[i], s, false, FDisableNotification[i], FReplyId[i], getReplyMarkup(i), ParseMode.Default);
                printMessageSentSuccess(i,m.Type);
            }
            
            FStopwatch[i].Stop();
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendContact", Category = "Telegram", Version = "", Help = "Sends contact messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendContactNode : TelegramSendNode
    {
        [Input("Phone Number", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FPhone;
        [Input("First Name", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FFirstName;
        [Input("Last Name", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FLastName;

        protected override async Task sendMessageAsync(int i)
        {
            int max = Math.Max(FPhone[i].SliceCount, Math.Max(FFirstName[i].SliceCount, FLastName[i].SliceCount));
            for (int j = 0; j < max; j++)
            {
                var m = await FBotClient[i].BC.SendContactAsync(FChatId[i], FPhone[i][j], FFirstName[i][j], FLastName[i][j], false, 0, getReplyMarkup(i));
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
        [Input("Longitude", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<double>> FLong;
        [Input("Latitude", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<double>> FLat;

        protected override async Task sendMessageAsync(int i)
        {
            int max = Math.Max(FLong[i].SliceCount, FLat[i].SliceCount);
            for(int j=0; j<max; j++)
            {
                var m = await FBotClient[i].BC.SendLocationAsync(FChatId[i], (float)FLong[i][j], (float)FLat[i][j], false, 0, getReplyMarkup(i));
                printMessageSentSuccess(i, m.Type);
            }
            FStopwatch[i].Stop();
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendVenue", Category = "Telegram", Version = "", Help = "Sends location messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendVenueNode : TelegramSendLocationNode
    {
        [Input("Title", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FTitle;
        [Input("Adress", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FAdress;

        protected override async Task sendMessageAsync(int i)
        {
            int max = Math.Max(FLong[i].SliceCount, FLat[i].SliceCount);
            for (int j = 0; j < max; j++)
            {
                var m = await FBotClient[i].BC.SendVenueAsync(FChatId[i], (float)FLong[i][j], (float)FLat[i][j], FTitle[i][j], FAdress[i][j], null, false, 0, getReplyMarkup(i));
                printMessageSentSuccess(i, m.Type);
            }
            FStopwatch[i].Stop();
        }
    }

    public abstract class TelegramSendFileNode : TelegramSendNode
    {
        [Input("File Name", DefaultString = "filename.xyz", StringType = StringType.Filename)]
        public ISpread<string> FFileName;

        [Output("Sent File ID")]
        public ISpread<ISpread<string>> FFileId;

        [Output("Sent File")]
        public ISpread<ISpread<TelegramFile>> FFile;

        protected override async Task sendMessageAsync(int i)
        {
            await PerformChatActionAsync(i);

            FFileId[i].SliceCount = 0;
            FFile[i].SliceCount = 0;

            using (var fileStream = new FileStream(FFileName[i], FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fts = new FileToSend(FFileName[i], fileStream);

                try
                {
                    await SendFileAsync(i, fts);
                    FStopwatch[i].Stop();
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
    [PluginInfo(Name = "SendSticker", Category = "Telegram", Version = "", Help = "Sends stickers (webp or jpg)",
                Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi",
                Bugs = "Files not spreadable", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendStickerNode : TelegramSendFileNode
    {

        protected override async Task PerformChatActionAsync(int i)
        {
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadDocument);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            Message m = await FBotClient[i].BC.SendStickerAsync(FChatId[i], fts, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            FFile[i].Add(new TelegramFile(m.Sticker, FBotClient[i].BC));
            FFileId[i].Add(m.Sticker.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendDocument", Category = "Telegram", Version = "", Help = "Sends document files", 
                Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", 
                Bugs = "Files not spreadable", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendDocumentNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public ISpread<string> FCaption;


        protected override async Task PerformChatActionAsync(int i)
        {
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadDocument);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            Message m =  await FBotClient[i].BC.SendDocumentAsync(FChatId[i], fts, FCaption[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            FFile[i].Add(new TelegramFile(m.Document, FBotClient[i].BC));
            FFileId[i].Add(m.Document.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendPhoto", Category = "Telegram", Version = "", Help = "Sends images from files", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", Bugs = "Photos not spreadable", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendPhotoNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;

        protected override async Task PerformChatActionAsync(int i)
        {
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadPhoto);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            Message m = await FBotClient[i].BC.SendPhotoAsync(FChatId[i], fts, FCaption[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            foreach(PhotoSize ps in m.Photo)
            {
                FFile[i].Add(new TelegramFile(ps, FBotClient[i].BC));
                FFileId[i].Add(ps.FileId);
            }
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
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadVideo);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            Message m = await FBotClient[i].BC.SendVideoAsync(FChatId[i], fts, FDuration[i], FCaption[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            FFile[i].Add(new TelegramFile(m.Video, FBotClient[i].BC));
            FFileId[i].Add(m.Video.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendAudio", Category = "Telegram", Version = "", Help = "Sends audio files (use .mp3 only)", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", Bugs = "Audio not spreadable", AutoEvaluate = true)]
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
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadAudio);
        }

        protected override async Task SendFileAsync(int i, FileToSend fts)
        {
            Message m = await FBotClient[i].BC.SendAudioAsync(FChatId[i], fts, FDuration[i], FPerformer[i], FTitle[i],  FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            FFile[i].Add(new TelegramFile(m.Audio, FBotClient[i].BC));
            FFileId[i].Add(m.Audio.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendPhotoRaw", Category = "Telegram", Version = "", Help = "Sends images from Raw Stream", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", Bugs = "Raw not spreadable", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendPhotoRawNode : TelegramSendNode
    {
        [Input("File Name", DefaultString = "filename.jpg")]
        public ISpread<string> FFileName;

        [Input("Image")]
        public ISpread<Stream> FImage;

        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;

        [Output("Sent File ID")]
        public ISpread<ISpread<string>> FFileId;

        [Output("Sent File")]
        public ISpread<ISpread<TelegramFile>> FFile;


        readonly byte[] FBuffer = new byte[1024];
        
        protected override async Task sendMessageAsync(int i)
        {

            FFileId[i].SliceCount = 0;
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadPhoto);

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

            outputStream.Position = 0;  // seems to be nescessary. otherwise telegram.bot api will not send any data

            var fts = new FileToSend(FFileName[i], outputStream);
            try
            {
                Message m = await FBotClient[i].BC.SendPhotoAsync(FChatId[i], fts, FCaption[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
                foreach(PhotoSize ps in m.Photo)
                {
                    FFile[i].Add(new TelegramFile(ps, FBotClient[i].BC));
                    FFileId[i].Add(ps.FileId);
                }
                
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
