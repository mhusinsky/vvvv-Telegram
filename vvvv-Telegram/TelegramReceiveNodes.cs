﻿using System;
using System.Collections.Generic;
using System.Linq;
//using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.IO;
using System.ComponentModel.Composition;

using System.Diagnostics;

using VVVV.PluginInterfaces.V2;
using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.Utils;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;



namespace VVVV.Nodes
{

    public abstract class TelegramReceiveNode : IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        #region fields & pins
        [Input("Bots")]
        public ISpread<BotClient> FBotClient;

        [Output("Message ID", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FMessageId;
        [Output("User ID", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FUid;
        [Output("User Name", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FUserName;
        [Output("First Name", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FFirstName;
        [Output("Last Name", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FLastName;
        [Output("User", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<User>> FUser;
        [Output("Timestamp", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<double>> FDate;        // TODO: check for compatibility with tmp's Time-Pack
        [Output("Received", IsBang = true)]
        public ISpread<bool> FReceived;

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        public virtual void OnImportsSatisfied() { }

        public void Evaluate(int SpreadMax)
        {
            setMessagesSliceCount(FBotClient.SliceCount);
            
            for (int i = 0; i < FBotClient.SliceCount; i++)
            {
                FReceived[i] = false;
                if (FBotClient[i] == null)
                    return;
                checkForMessage(i);
            }
        }

        protected virtual void setMessagesSliceCount(int botCount)
        {
            FReceived.SliceCount = botCount;
            setMessageInfoSliceCount(botCount);
        }

        protected void setMessageInfoSliceCount(int botCount)
        {
            FUser.SliceCount = botCount;
            FUserName.SliceCount = botCount;
            FFirstName.SliceCount = botCount;
            FLastName.SliceCount = botCount;
            FUid.SliceCount = botCount;
            FDate.SliceCount = botCount;
            FMessageId.SliceCount = botCount;
        }

        protected void initMessageInfoSlices(int index, int SliceCount)
        {
            FUser[index].SliceCount = SliceCount;
            FUserName[index].SliceCount = SliceCount;
            FFirstName[index].SliceCount = SliceCount;
            FLastName[index].SliceCount = SliceCount;
            FUid[index].SliceCount = SliceCount;
            FDate[index].SliceCount = SliceCount;
            FMessageId[index].SliceCount = SliceCount;
        }

        protected void setMessageInfoData(int index, int count, VTelegramMessage tm)
        {
            User u = tm.message.From;
            FUser[index][count] = u;

            FUserName[index][count] = u.Username;
            FFirstName[index][count] = u.FirstName;
            FLastName[index][count] = u.LastName;
            FUid[index][count] = u.Id;
            FDate[index][count] = tm.created.ToOADate();
            FMessageId[index][count] = tm.message.MessageId;
        }

        protected List<VTelegramMessage> getMessageList(int i)
        {
            return FBotClient[i].Messages.Where(m => m.message.Type == getMyMessageType()).ToList();
        }

        protected virtual void checkForMessage(int i)
        {
            var myMessages = getMessageList(i);
            int messageCount = myMessages.Count();

            if (messageCount < 1) return;

            resetMessageTypeData(i);
            initMessageInfoSlices(i, messageCount);
            int count = 0;
            
            foreach (VTelegramMessage tm in myMessages)
            {
                var m = tm.message;

                int fileCount = SetOutputs(i, m);

                setFileCount(i, fileCount);
                setMessageInfoData(i, count, tm);
                FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": " + getMyMessageType().ToString() + " received");
                count++;
            }

            FReceived[i] = true;
        }

        protected virtual void setFileCount(int i, int count) { }
        
        protected abstract MessageType getMyMessageType();
        protected abstract void resetMessageTypeData(int index);

        protected abstract int SetOutputs(int i, Message m);
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveText", Category = "Telegram", Version = "", Help = "Receives Text Messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveTextNode : TelegramReceiveNode
    {
        [Output("Text", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FTextMessage;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Text;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);
            FTextMessage.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            FTextMessage[index] = new Spread<string>();
        }

        protected override int SetOutputs(int i, Message m)
        {
            FTextMessage[i].Add(m.Text);
            return 0;
        }   
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveContact", Category = "Telegram", Version = "", Help = "Receives contact messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveContactNode : TelegramReceiveNode
    {
        [Output("Contact Phone", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FContactPhone;
        [Output("Contact First Name", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FContactFirstname;
        [Output("Contact Last Name", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FContactLastname;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Contact;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);
            FContactPhone.SliceCount = botCount;
            FContactFirstname.SliceCount = botCount;
            FContactLastname.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            FContactPhone[index] = new Spread<string>();
            FContactFirstname[index] = new Spread<string>();
            FContactLastname[index] = new Spread<string>();
        }

        protected override int SetOutputs(int i, Message m)
        {
            Contact c = m.Contact;
            FContactPhone[i].Add(c.PhoneNumber);
            FContactFirstname[i].Add(c.FirstName);
            FContactLastname[i].Add(c.LastName);
            return 0;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveLocation", Category = "Telegram", Version = "", Help = "Receives location messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveLocationNode : TelegramReceiveNode
    {
        [Output("Longitude", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<double>> FLong;
        [Output("Latitude", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<double>> FLat;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Location;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);
            FLong.SliceCount = botCount;
            FLat.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            FLong[index] = new Spread<double>();
            FLat[index] = new Spread<double>();
        }

        protected override int SetOutputs(int i, Message m)
        {
            FLong[i].Add(m.Location.Longitude);
            FLat[i].Add(m.Location.Latitude);
            return 0;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveVenue", Category = "Telegram", Version = "", Help = "Receives venue messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo

    public class TelegramReceiveVenueNode : TelegramReceiveLocationNode
    {
        [Output("Title", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FTitle;
        [Output("Adress", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FAdress;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Venue;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);
            FLong.SliceCount = botCount;
            FLat.SliceCount = botCount;
            FTitle.SliceCount = botCount;
            FAdress.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            FLong[index] = new Spread<double>();
            FLat[index] = new Spread<double>();
            FTitle[index] = new Spread<string>();
            FAdress[index] = new Spread<string>();
        }

        protected override int SetOutputs(int i, Message m)
        {
            FLong[i].Add(m.Venue.Location.Longitude);
            FLat[i].Add(m.Venue.Location.Latitude);
            FTitle[i].Add(m.Venue.Title);
            FAdress[i].Add(m.Venue.Address);
            return 0;
        }
    }

    
    public abstract class TelegramReceiveFileMessageNode : TelegramReceiveNode
    {
        [Output("File", Order = -100, BinOrder = -99)]
        public ISpread<ISpread<TelegramFile>> FFile;
        [Output("Files per Message", Order = -90, BinVisibility = PinVisibility.False)]
        public ISpread<ISpread<int>> FFileCount;

        protected override void setFileCount(int i, int count)
        {
            FFileCount[i].Add(count);
        }

        public override void OnImportsSatisfied()
        {
            FFile.SliceCount = 0;
            FFileCount.SliceCount = 0;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);

            FFile.SliceCount = botCount;
            FFileCount.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            FFile[index] = new Spread<TelegramFile>();
            FFileCount[index] = new Spread<int>();
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveSticker", Category = "Telegram", Version = "", Help = "Receives sticker messages (containing a .webp file)", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveStickerNode : TelegramReceiveNode
    {
        [Output("Emoji", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FEmoji;
        [Output("Dimensions", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<Vector2D>> FDimensions;
        [Output("Sticker", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<TelegramFile>> FThumb;


        protected override MessageType getMyMessageType()
        {
            return MessageType.Sticker;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);
            FEmoji.SliceCount = botCount;
            FDimensions.SliceCount = botCount;
            FThumb.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            FEmoji[index] = new Spread<string>(); ;
            FDimensions[index] = new Spread<Vector2D>(); ;
            FThumb[index] = new Spread<TelegramFile>(); ;
        }

        protected override int SetOutputs(int i, Message m)
        {
            Sticker s = m.Sticker;
            int count = 0;
            FEmoji[i].Add(s.Emoji);
            FDimensions[i].Add(new Vector2D(Convert.ToDouble(s.Height), Convert.ToDouble(s.Width)));
            FThumb[i].Add(new TelegramFile(s.Thumb, FBotClient[i].BC));

            return ++count;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceivePhoto", Category = "Telegram", Version = "", Help = "Receives photo messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceivePhotoNode : TelegramReceiveFileMessageNode
    {
        [Output("Caption", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FCaption;
        [Output("Dimensions", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<Vector2D>> FDimensions;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Photo;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);

            FDimensions.SliceCount = botCount;
            FCaption.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            base.resetMessageTypeData(index);

            FDimensions[index] = new Spread<Vector2D>();
            FCaption[index] = new Spread<string>();
        }


        protected override int SetOutputs(int i, Message m)
        {
            PhotoSize[] ps = m.Photo;
            FCaption[i].Add(m.Caption);
            int count = 0;

            foreach (PhotoSize p in ps)
            {
                FDimensions[i].Add(new Vector2D((double)p.Width, (double)p.Height));
                FFile[i].Add(new TelegramFile(p, FBotClient[i].BC));
                count++;
            }

            return count;
        }
    }

    

    #region PluginInfo
    [PluginInfo(Name = "ReceiveDocument", Category = "Telegram", Version = "", Help = "Receives document messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveDocumentNode : TelegramReceiveFileMessageNode
    {
        [Output("Caption", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FCaption;
        [Output("Filename", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FFilename;
        [Output("FileId", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FFileId;
        [Output("FileUniqueId", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FFileUniqueId;
        [Output("MimeType", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FMimeType;
        [Output("File Size", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FSize;
        [Output("Thumbnail", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<TelegramFile>> FThumb;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Document;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);

            FCaption.SliceCount = botCount;
            FFilename.SliceCount = botCount;
            FFileId.SliceCount = botCount;
            FFileUniqueId.SliceCount = botCount;
            FMimeType.SliceCount = botCount;
            FSize.SliceCount = botCount;
            FThumb.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            base.resetMessageTypeData(index);

            FCaption[index] = new Spread<string>();
            FFilename[index] = new Spread<string>();
            FFileId[index] = new Spread<string>();
            FFileUniqueId[index] = new Spread<string>();
            FMimeType[index] = new Spread<string>();
            FSize[index] = new Spread<int>();
            FThumb[index] = new Spread<TelegramFile>();
        }


        protected override int SetOutputs(int i, Message m)
        {
            Document d = m.Document;

            FCaption[i].Add(m.Caption);
            FFilename[i].Add(d.FileName);
            FFileId[i].Add(d.FileId);
            FFileUniqueId[i].Add(d.FileUniqueId);
            FMimeType[i].Add(d.MimeType);
            FSize[i].Add(d.FileSize);
            FThumb[i].Add(new TelegramFile(d.Thumb, FBotClient[i].BC));
            FFile[i].Add(new TelegramFile(d, FBotClient[i].BC));

            return 1;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveAudio", Category = "Telegram", Version = "", Help = "Receives audio messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveAudioNode : TelegramReceiveFileMessageNode
    {

        [Output("Title", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FTitle;
        [Output("Performer", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FPerformer;
        [Output("Duration", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FDuration;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Audio;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);

            FTitle.SliceCount = botCount;
            FPerformer.SliceCount = botCount;
            FDuration.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            base.resetMessageTypeData(index);

            FTitle[index] = new Spread<string>();
            FPerformer[index] = new Spread<string>();
            FDuration[index] = new Spread<int>();
        }

        protected override int SetOutputs(int i, Message m)
        {
            Audio a = m.Audio;
            int count = 0;
            FTitle[i].Add(a.Title);
            FPerformer[i].Add(a.Performer);
            FDuration[i].Add(a.Duration);
            FFile[i].Add(new TelegramFile(a, FBotClient[i].BC));

            return ++count;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveVoice", Category = "Telegram", Version = "", Help = "Receives audio messages (.ogg format with OPUS codec)", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveVoiceNode : TelegramReceiveFileMessageNode
    {

        [Output("Duration", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FDuration;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Voice;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);

            FDuration.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            base.resetMessageTypeData(index);

            FDuration[index] = new Spread<int>();
        }


        protected override int SetOutputs(int i, Message m)
        {
            Voice v = m.Voice;
            int count = 0;
            FDuration[i].Add(v.Duration);
            FFile[i].Add(new TelegramFile(v, FBotClient[i].BC));

            return ++count;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveVideo", Category = "Telegram", Version = "", Help = "Receives video messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveVideoNode : TelegramReceiveFileMessageNode
    {
        [Output("Caption", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FCaption;
        [Output("MimeType", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FMimeType;
        [Output("Duration", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FDuration;
        [Output("Dimensions", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<Vector2D>> FDimensions;
        [Output("Thumbnail", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<TelegramFile>> FThumb;

        protected override MessageType getMyMessageType()
        {
            return MessageType.Video;
        }

        protected override void setMessagesSliceCount(int botCount)
        {
            base.setMessagesSliceCount(botCount);

            FCaption.SliceCount = botCount;
            FMimeType.SliceCount = botCount;
            FDuration.SliceCount = botCount;
            FDimensions.SliceCount = botCount;
            FThumb.SliceCount = botCount;
        }

        protected override void resetMessageTypeData(int index)
        {
            base.resetMessageTypeData(index);

            FCaption[index] = new Spread<string>();
            FMimeType[index] = new Spread<string>();
            FDuration[index] = new Spread<int>();
            FDimensions[index] = new Spread<Vector2D>();
            FThumb[index] = new Spread<TelegramFile>();
        }


        protected override int SetOutputs(int i, Message m)
        {
            Video v = m.Video;
            
            FCaption[i].Add(m.Caption);
            FMimeType[i].Add(v.MimeType);
            FDuration[i].Add(v.Duration);
            FDimensions[i].Add(new Vector2D(Convert.ToDouble(v.Height), Convert.ToDouble(v.Width)));
            FThumb[i].Add(new TelegramFile(m.Video.Thumb, FBotClient[i].BC));

            FFile[i].Add(new TelegramFile(v, FBotClient[i].BC));
            
            return 1;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "GetFile", Category = "Telegram", Version = "", Help = "Downloads files from messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramFileNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("File")]
        public ISpread<TelegramFile> FFile;
        [Input("Get", IsBang = true)]
        public ISpread<bool> FGet;

        [Output("File ID")]
        public ISpread<string> FFileId;
        [Output("File Path")]
        public ISpread<string> FFilePath;
        [Output("File Size")]
        public ISpread<int> FFileSize;
        [Output("Data")]
        public ISpread<Stream> FFileData;
        [Output("Received", IsBang = true)]
        public ISpread<bool> FReceived;

        private Spread<int> isNew = new Spread<int>();
		
        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        public void OnImportsSatisfied()
        {
            FFileData.SliceCount = 0;
        }


        public void Evaluate(int SpreadMax)
        {
            FReceived.SliceCount = Math.Max(1, FFile.SliceCount);

            FFileId.SliceCount = FFile.SliceCount;
            FFilePath.SliceCount = FFile.SliceCount;
            FFileSize.SliceCount = FFile.SliceCount;
            isNew.SliceCount = FFile.SliceCount;
            FFileData.ResizeAndDispose(FFile.SliceCount, () => new MemoryStream());
            
            for(int i=0; i < FFile.SliceCount; i++)
            {
                if(isNew[i] > 0)
                {
                    FReceived[i] = true;
                    isNew[i]--;
                }
                else
                {
                    FReceived[i] = false;
                }
                
                var f = FFile[i];
                if (f == null || FGet[i] == false)
                    continue;

                DownloadFileAsync(i, f);
            }
                
        }

        public async void DownloadFileAsync(int index, TelegramFile f)
        {
            var inputStream = new MemoryStream();
            
            var downloadFile = await f.botClient.GetInfoAndDownloadFileAsync(f.file.FileId, inputStream);
            FFileId[index] = downloadFile.FileId;
            FFilePath[index] = downloadFile.FilePath;
            FFileSize[index] = downloadFile.FileSize;

            inputStream.Position = 0;
            FFileData[index] = inputStream;

            isNew[index]++; 
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "User", Category = "Telegram", Version = "Split", Help = "Split user data", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramSplitUserNode : IPluginEvaluate
    {
        
        [Input("User")]
        public ISpread<User> FUser;

        [Output("Username")]
        public ISpread<string> FUsername;
        [Output("First Name")]
        public ISpread<string> FFirstName;
        [Output("Last Name")]
        public ISpread<string> FLastName;
        [Output("User ID")]
        public ISpread<int> FUid;
    
        public void Evaluate(int SpreadMax)
        {
            if (FUser.SliceCount < 1)
                return;

            FUsername.SliceCount = 0;
            FFirstName.SliceCount = 0;
            FLastName.SliceCount = 0;
            FUid.SliceCount = 0;

            foreach (User u in FUser)
            {
                if (u == null)
                    return;
                FUsername.Add(u.Username);
                FFirstName.Add(u.FirstName);
                FLastName.Add(u.LastName);
                FUid.Add(u.Id);
            }

        }
    }
}
