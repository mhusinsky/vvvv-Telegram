using System;
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

using VVVV.Packs.Time;


namespace VVVV.Nodes
{

    public abstract class TelegramReceiveNode : IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        #region fields & pins
        [Input("Bots")]
        public ISpread<BotClient> FBotClient;

        [Output("User Name", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FUserName;
        [Output("First Name", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FFirstName;
        [Output("Last Name", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FLastName;
        [Output("User", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<User>> FUser;
        [Output("Timestamp", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<Time>> FDate;        // TODO: check for compatibility with tmp's Time-Pack
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

        protected void setMessagesSliceCount(int botCount)
        {
            FReceived.SliceCount = botCount;
            setMessageInfoSliceCount(botCount);
            setMessageTypeSliceCount(botCount);
        }

        protected void setMessageInfoSliceCount(int botCount)
        {
            FUser.SliceCount = botCount;
            FUserName.SliceCount = botCount;
            FFirstName.SliceCount = botCount;
            FLastName.SliceCount = botCount;

            FDate.SliceCount = botCount;
        }

        protected void initMessageInfoSlices(int index, int SliceCount)
        {
            FUser[index] = new Spread<User>();
            FUserName[index] = new Spread<string>();
            FFirstName[index] = new Spread<string>();
            FLastName[index] = new Spread<string>();

            FDate[index] = new Spread<Time>();
        }

        protected void setMessageInfoData(int index, VTelegramMessage tm)
        {
            User u = tm.message.From;
            FUser[index].Add(u);

            FUserName[index].Add(u.Username);
            FFirstName[index].Add(u.FirstName);
            FLastName[index].Add(u.LastName);
            
            FDate[index].Add(tm.created);
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

            setMessageTypeData(i, messageCount);
            setMessageSpecialsData(i, messageCount);
            initMessageInfoSlices(i, messageCount);

            foreach (VTelegramMessage tm in myMessages)
            {
                var m = tm.message;

                int fileCount = SetOutputs(i, m);

                setFileCount(i, fileCount);
                setMessageInfoData(i, tm);
                FLogger.Log(LogType.Debug, "Bot \"" + FBotClient[i].Username + "\": " + getMyMessageType().ToString() + " received");
            }

            FReceived[i] = true;
        }

        protected virtual void setFileCount(int i, int count) { }
        protected virtual void setMessageSpecialsSliceCount(int SliceCount) { }
        protected virtual void setMessageSpecialsData(int index, int SliceCount) { }

        protected abstract MessageType getMyMessageType();
        protected abstract void setMessageTypeSliceCount(int botCount);
        protected abstract void setMessageTypeData(int index, int SliceCount);

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
            return MessageType.TextMessage;
        }

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FTextMessage.SliceCount = botCount;
        }

        protected override void setMessageTypeData(int index, int SliceCount)
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
        [Output(" Contact Phone", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FContactPhone;
        [Output("Contact First Name", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FContactFirstname;
        [Output("Contact Last Name", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FContactLastname;

        protected override MessageType getMyMessageType()
        {
            return MessageType.ContactMessage;
        }

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FContactPhone.SliceCount = botCount;
            FContactFirstname.SliceCount = botCount;
            FContactLastname.SliceCount = botCount;
        }

        protected override void setMessageTypeData(int index, int SliceCount)
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
            return MessageType.LocationMessage;
        }

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FLong.SliceCount = botCount;
            FLat.SliceCount = botCount;
        }

        protected override void setMessageTypeData(int index, int SliceCount)
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
            return MessageType.VenueMessage;
        }

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FLong.SliceCount = botCount;
            FLat.SliceCount = botCount;
            FTitle.SliceCount = botCount;
            FAdress.SliceCount = botCount;
        }

        protected override void setMessageTypeData(int index, int SliceCount)
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
            return MessageType.StickerMessage;
        }

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FEmoji.SliceCount = botCount;
            FDimensions.SliceCount = botCount;
            FThumb.SliceCount = botCount;
        }

        protected override void setMessageTypeData(int index, int SliceCount)
        {
            FEmoji[index] = new Spread<string>(); ;
            FDimensions[index] = new Spread<Vector2D>(); ;
            FThumb[index] = new Spread<TelegramFile>(); ;
        }
        

        protected override void setMessageSpecialsSliceCount(int botCount)
        { }

        protected override int SetOutputs(int i, Message m)
        {
            Sticker s = m.Sticker;
            int count = 0;
            FEmoji[i].Add(s.Emoji);
            FDimensions[i].Add(new Vector2D(Double.Parse(s.Height), Double.Parse(s.Width)));
            FThumb[i].Add(new TelegramFile(s.Thumb, FBotClient[i].BC));

            return ++count;
        }
    }


    public abstract class TelegramReceiveFileMessageNode : TelegramReceiveNode
    {
        [Output("File")]
        public ISpread<ISpread<TelegramFile>> FFile;
        [Output("Files per Message", BinVisibility = PinVisibility.Hidden)]
        public ISpread<ISpread<int>> FFileCount;

        protected override void setFileCount(int i, int count)
        {
            FFileCount[i].Add(count);
        }

        public override void OnImportsSatisfied()
        {
            FFile.SliceCount = 0;
            FFileCount.SliceCount = 0;
            setMessageSpecialsSliceCount(0);
        }

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FFile.SliceCount = botCount;
            FFileCount.SliceCount = botCount;
            setMessageSpecialsSliceCount(botCount);
        }

        protected override void setMessageTypeData(int index, int SliceCount)
        {
            FFile[index].SliceCount = SliceCount;
            FFile[index] = new Spread<TelegramFile>();

            FFileCount[index].SliceCount = SliceCount;
            FFileCount[index] = new Spread<int>();
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
            return MessageType.PhotoMessage;
        }

        protected override void setMessageSpecialsSliceCount(int botCount)
        {
            FDimensions.SliceCount = botCount;
            FCaption.SliceCount = botCount;
        }

        protected override void setMessageSpecialsData(int index, int SliceCount)
        {
            FDimensions[index] = new Spread<Vector2D>();
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
        [Output("File Path", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FFilePath;
        [Output("MimeType", Visibility = PinVisibility.OnlyInspector, BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<string>> FMimeType;
        [Output("File Size", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<int>> FSize;
        [Output("Thumbnail", BinVisibility = PinVisibility.OnlyInspector)]
        public ISpread<ISpread<TelegramFile>> FThumb;

        protected override MessageType getMyMessageType()
        {
            return MessageType.DocumentMessage;
        }


        protected override void setMessageSpecialsData(int index, int SliceCount)
        {
            FCaption.SliceCount = SliceCount;
            FFilename.SliceCount = SliceCount;
            FFilePath.SliceCount = SliceCount;
            FMimeType.SliceCount = SliceCount;
            FSize.SliceCount = SliceCount;
            FThumb.SliceCount = SliceCount;
        }


        protected override int SetOutputs(int i, Message m)
        {
            Document d = m.Document;

            FCaption[i].Add(m.Caption);
            FFilename[i].Add(d.FileName);
            FFilePath[i].Add(d.FilePath);
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
            return MessageType.AudioMessage;
        }

        protected override void setMessageSpecialsSliceCount(int botCount)
        {
            FTitle.SliceCount = botCount;
            FPerformer.SliceCount = botCount;
            FDuration.SliceCount = botCount;
        }


        protected override int SetOutputs(int i, Message m)
        {
            Audio a = m.Audio;
            int count = 0;
            FTitle[i].Add(a.Title);
            FPerformer[i].Add(a.Performer);
            FDuration[i].Add(a.Duration);
            FFile[i].Add(new TelegramFile(m.Audio, FBotClient[i].BC));

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
            return MessageType.VideoMessage;
        }


        protected override void setMessageSpecialsData(int index, int SliceCount)
        {
            FCaption.SliceCount = SliceCount;
            FMimeType.SliceCount = SliceCount;
            FDuration.SliceCount = SliceCount;
            FDimensions.SliceCount = SliceCount;
            FThumb.SliceCount = SliceCount;
        }


        protected override int SetOutputs(int i, Message m)
        {
            Video v = m.Video;
            
            FCaption[i].Add(m.Caption);
            FMimeType[i].Add(v.MimeType);
            FDuration[i].Add(v.Duration);
            FDimensions[i].Add(new Vector2D(Double.Parse(v.Height), Double.Parse(v.Width)));
            FThumb[i].Add(new TelegramFile(m.Video.Thumb, FBotClient[i].BC));

            FFile[i].Add(new TelegramFile(m.Video, FBotClient[i].BC));
            
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

        [Output("FileID")]
        public ISpread<string> FFileId;
        [Output("File Path")]
        public ISpread<string> FFilePath;
        [Output("File Size")]
        public ISpread<int> FFileSize;
        [Output("Data")]
        public ISpread<Stream> FFileData;
        [Output("Received", IsBang = true)]
        public ISpread<bool> FReceived;

        readonly byte[] FBuffer = new byte[1024];

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
            FReceived.SliceCount = FFile.SliceCount;
            FFileId.SliceCount = FFile.SliceCount;
            FFilePath.SliceCount = FFile.SliceCount;
            FFileSize.SliceCount = FFile.SliceCount;
            isNew.SliceCount = FFile.SliceCount;
            FFileData.ResizeAndDispose(FFile.SliceCount, () => new MemoryStream());

            //int max = Math.Max(FFile.SliceCount, FGet.SliceCount);

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

                DownloadFileAsync(i, f, FFileData[i]);
            }
                
        }

        public async void DownloadFileAsync(int index, TelegramFile f, Stream output)
        {
            
            Telegram.Bot.Types.File thisFile = await f.botClient.GetFileAsync(f.file.FileId);

            FFileId[index] = f.file.FileId;
            FFilePath[index] = f.file.FilePath;
            FFileSize[index] = f.file.FileSize;

            var inputStream = thisFile.FileStream;
            var outputStream = output;


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
            outputStream.Position = 0;
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
