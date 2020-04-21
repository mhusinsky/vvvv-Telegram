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
//using Telegram.Bot.Types.InputMessageContents;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace VVVV.Nodes
{
    public abstract class TelegramSendNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("Bots", Order = -100)]
        public ISpread<BotClient> FBotClient;

        [Input("Chat ID", Order = -90)]
        public IDiffSpread<int> FChatId;

        [Input("Disable Notification")]
        public IDiffSpread<bool> FDisableNotification;

        [Input("Reply to Message ID")]
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

            setMessagesSliceCount(FBotClient.SliceCount);

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
                        FLogger.Log(LogType.Debug, "Bot at index " + i + ": Cannot send message, no BotClient available");
                        FError[i] = "Cannot send message, no BotClient available";
                    }
                    else if(FChatId.SliceCount == 0)
                    {
                        FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Cannot send message, no ChatID given");
                        FError[i] = "Cannot send message, no ChatID given";
                    }
                    else if (FReplyId.SliceCount == 0)
                    {
                        FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Cannot send message, no Reply Message ID given");
                        FError[i] = "Cannot send message, no Reply Message ID given";
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
                        FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Cannot send message, BotClient not connected");
                        FError[i] = "Cannot send message, BotClient not connected";
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
                case 1: return new ForceReplyMarkup();
                case 2: return new ReplyKeyboardRemove();
                case 3: return FReplyMarkupKeyboard[i];
            }

            return null;
        }

        protected void printMessageSentSuccess(int i, MessageType mt)
        {
            FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Sent " + mt.ToString());
            FError[i] = "";
        }

        protected abstract Task sendMessageAsync(int i);
        protected virtual void setMessagesSliceCount(int botCount) { }

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
            foreach (string s in FTextMessage[i])
            {
                Message m = await FBotClient[i].BC.SendTextMessageAsync(FChatId[i], s, ParseMode.Default, false , FDisableNotification[i], FReplyId[i], getReplyMarkup(i)); //TODO: Make WebPagePreview configurable
                printMessageSentSuccess(i, m.Type);
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
                var m = await FBotClient[i].BC.SendContactAsync(FChatId[i], FPhone[i][j], FFirstName[i][j], FLastName[i][j], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
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
                var m = await FBotClient[i].BC.SendLocationAsync(FChatId[i], (float)FLong[i][j], (float)FLat[i][j], 0, FDisableNotification[i], FReplyId[i]);
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
                var m = await FBotClient[i].BC.SendVenueAsync(FChatId[i], (float)FLong[i][j], (float)FLat[i][j], FTitle[i][j], FAdress[i][j], null, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
                printMessageSentSuccess(i, m.Type);
            }
            FStopwatch[i].Stop();
        }
    }

    public abstract class TelegramSendFileNode : TelegramSendNode
    {
        [Input("File Name", DefaultString = "filename.xyz", StringType = StringType.Filename)]
        public ISpread<string> FFileName;
        [Input("Send File by ID", DefaultBoolean = false, Visibility = PinVisibility.OnlyInspector)]
        public ISpread<bool> FSendById;
        [Input("File ID", StringType = StringType.String, Visibility = PinVisibility.OnlyInspector)]
        public ISpread<string> FInFileId;

        [Output("Sent File ID")]
        public ISpread<ISpread<string>> FOutFileId;

        [Output("Sent File")]
        public ISpread<ISpread<TelegramFile>> FFile;

        protected override void setMessagesSliceCount(int botCount)
        {
            FFile.SliceCount = botCount;
            FOutFileId.SliceCount = botCount;
        }

        protected override async Task sendMessageAsync(int i)
        {
            await PerformChatActionAsync(i);

            FOutFileId[i].SliceCount = 0;
            FFile[i].SliceCount = 0;

            if (FSendById[i])
            {
                try
                {
                    await SendFileAsync(i, FInFileId[i]);
                    FStopwatch[i].Stop();
                }
                catch (Exception e)
                {
                    FStopwatch[i].Stop();
                    FStopwatch[i].Reset();
                    FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Cannot send message. Exception: " + e.Message);
                    FError[i] = e.Message;
                }
            }
            else
            {
                try
                {
                    using (var fileStream = new FileStream(FFileName[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        try
                        {
                            await SendFileAsync(i, fileStream);
                            FStopwatch[i].Stop();
                        }
                        catch (Exception e)
                        {
                            FStopwatch[i].Stop();
                            FStopwatch[i].Reset();
                            FError[i] = e.Message;
                            FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Cannot send message. Exception: " + e.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    FStopwatch[i].Stop();
                    FStopwatch[i].Reset();
                    FError[i] = e.Message;
                    FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Cannot send message. Exception: " + e.Message);
                }
            }
        }

        
        protected abstract Task PerformChatActionAsync(int i);
        protected abstract Task SendFileAsync(int i, Stream stream);
        protected abstract Task SendFileAsync(int i, string FileId);
        protected abstract void SetFileOutputs(int i, Message m);
    }

    #region PluginInfo
    [PluginInfo(Name = "SendSticker", Category = "Telegram", Version = "", Help = "Sends stickers (webp or jpg)",
                Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi",
                Bugs = "Only one sticker per bot", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendStickerNode : TelegramSendFileNode
    {

        protected override async Task PerformChatActionAsync(int i)
        {
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadDocument);
        }

        protected override async Task SendFileAsync(int i, Stream stream)
        {
            var iof = new InputOnlineFile(stream);
            Message m = await FBotClient[i].BC.SendStickerAsync(FChatId[i], iof, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override async Task SendFileAsync(int i, string FileId)
        {
            Message m = await FBotClient[i].BC.SendStickerAsync(FChatId[i], FileId, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override void SetFileOutputs(int i, Message m)
        {
            FFile[i].Add(new TelegramFile(m.Sticker, FBotClient[i].BC));
            FOutFileId[i].Add(m.Sticker.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendDocument", Category = "Telegram", Version = "", Help = "Sends document files", 
                Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", 
                Bugs = "Only one document per bot", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendDocumentNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public ISpread<string> FCaption;


        protected override async Task PerformChatActionAsync(int i)
        {
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadDocument);
        }

        protected override async Task SendFileAsync(int i, Stream stream)
        {
            Message m =  await FBotClient[i].BC.SendDocumentAsync(FChatId[i], stream, FCaption[i], ParseMode.Default, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override async Task SendFileAsync(int i, string FileId)
        {
            Message m = await FBotClient[i].BC.SendDocumentAsync(FChatId[i], FileId, FCaption[i], ParseMode.Default, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override void SetFileOutputs(int i, Message m)
        {
            FFile[i].Add(new TelegramFile(m.Document, FBotClient[i].BC));
            FOutFileId[i].Add(m.Document.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendPhoto", Category = "Telegram", Version = "", Help = "Sends images from files", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", Bugs = "Only one photo per bot", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendPhotoNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;

        protected override async Task PerformChatActionAsync(int i)
        {
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.UploadPhoto);
        }

        protected override async Task SendFileAsync(int i, Stream stream)
        {
            Message m = await FBotClient[i].BC.SendPhotoAsync(FChatId[i], stream, FCaption[i], ParseMode.Default, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override async Task SendFileAsync(int i, string FileId)
        {
            Message m = await FBotClient[i].BC.SendPhotoAsync(FChatId[i], FileId, FCaption[i], ParseMode.Default, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override void SetFileOutputs(int i, Message m)
        {
            foreach (PhotoSize ps in m.Photo)
            {
                FFile[i].Add(new TelegramFile(ps, FBotClient[i].BC));
                FOutFileId[i].Add(ps.FileId);
            }
            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendVideo", Category = "Telegram", Version = "", Help = "Sends video files (use .mp4 only)", Credits = "Based on telegram.bot", Tags = "Network, Bot", Bugs = "Only one video per bot", Author = "motzi", AutoEvaluate = true)]
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

        protected override async Task SendFileAsync(int i, Stream stream)
        {
            Message m = await FBotClient[i].BC.SendVideoAsync(FChatId[i], stream, FDuration[i], 0, 0, FCaption[i], ParseMode.Default , false, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override async Task SendFileAsync(int i, string FileId)
        {
            Message m = await FBotClient[i].BC.SendVideoAsync(FChatId[i], FileId, FDuration[i], 0, 0, FCaption[i], ParseMode.Default, false, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override void SetFileOutputs(int i, Message m)
        {
            FFile[i].Add(new TelegramFile(m.Video, FBotClient[i].BC));
            FOutFileId[i].Add(m.Video.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendAudio", Category = "Telegram", Version = "", Help = "Sends audio files (use .mp3 only)", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", Bugs = "Only one audio file per bot", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendAudioNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;

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

        protected override async Task SendFileAsync(int i, Stream stream)
        {
            Message m = await FBotClient[i].BC.SendAudioAsync(FChatId[i], stream, FCaption[i], ParseMode.Default, FDuration[i], FPerformer[i], FTitle[i],  FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override async Task SendFileAsync(int i, string FileId)
        {
            Message m = await FBotClient[i].BC.SendAudioAsync(FChatId[i], FileId, FCaption[i], ParseMode.Default, FDuration[i], FPerformer[i], FTitle[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override void SetFileOutputs(int i, Message m)
        {
            FFile[i].Add(new TelegramFile(m.Audio, FBotClient[i].BC));
            FOutFileId[i].Add(m.Audio.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendVoice", Category = "Telegram", Version = "", Help = "Sends voice files (use .ogg with OPUS codec only)", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", Bugs = "Only one audio file per bot", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSendVoiceNode : TelegramSendFileNode
    {
        [Input("Caption", DefaultString = "caption")]
        public IDiffSpread<string> FCaption;

        [Input("Duration")]
        public IDiffSpread<int> FDuration;

        protected override async Task PerformChatActionAsync(int i)
        {
            await FBotClient[i].BC.SendChatActionAsync(FChatId[i], ChatAction.RecordAudio);
        }

        protected override async Task SendFileAsync(int i, Stream stream)
        {
            Message m = await FBotClient[i].BC.SendVoiceAsync(FChatId[i], stream, FCaption[i], ParseMode.Default, FDuration[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override async Task SendFileAsync(int i, string FileId)
        {
            Message m = await FBotClient[i].BC.SendVoiceAsync(FChatId[i], FileId, FCaption[i], ParseMode.Default, FDuration[i], FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
            SetFileOutputs(i, m);
        }
        protected override void SetFileOutputs(int i, Message m)
        {
            FFile[i].Add(new TelegramFile(m.Voice, FBotClient[i].BC));
            FOutFileId[i].Add(m.Voice.FileId);

            printMessageSentSuccess(i, m.Type);
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "SendPhotoRaw", Category = "Telegram", Version = "", Help = "Sends images from Raw Stream", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", Bugs = "Only one photo per bot", AutoEvaluate = true)]
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

            try
            {
                var iof = new InputOnlineFile(outputStream, FFileName[i]);
                
                try
                {
                    Message m = await FBotClient[i].BC.SendPhotoAsync(FChatId[i], iof, FCaption[i], ParseMode.Default, FDisableNotification[i], FReplyId[i], getReplyMarkup(i));
                    foreach(PhotoSize ps in m.Photo)
                    {
                        FFile[i].Add(new TelegramFile(ps, FBotClient[i].BC));
                        FFileId[i].Add(ps.FileId);
                    }
                
                    FStopwatch[i].Stop();
                    printMessageSentSuccess(i, m.Type);
                }
                catch (Exception e)
                {
                    FStopwatch[i].Stop();
                    FStopwatch[i].Reset();
                    FError[i] = e.Message;
                    FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Cannot send message. Exception: " + e.Message);
                }
            }
            catch (Exception e)
            {
                FStopwatch[i].Stop();
                FStopwatch[i].Reset();
                FError[i] = e.Message;
                FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": Cannot send message. Exception: " + e.Message);
            }

        }
    }
}
