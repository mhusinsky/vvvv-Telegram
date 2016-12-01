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


namespace VVVV.Nodes
{

    public abstract class TelegramReceiveNode : IPluginEvaluate
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
        [Output("User")]
        public ISpread<ISpread<User>> FUser;
        [Output("Received", IsBang = true)]
        public ISpread<bool> FReceived;

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        public void Evaluate(int SpreadMax)
        {
            
            setMessagesSliceCount(FBotClient.SliceCount);
            setUserSliceCount(FBotClient.SliceCount);
            FReceived.SliceCount = FBotClient.SliceCount;

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
            setMessageTypeSliceCount(botCount);
        }

        protected void setUserSliceCount(int botCount)
        {
            FUser.SliceCount = botCount;
            FUserName.SliceCount = botCount;
            FFirstName.SliceCount = botCount;
            FLastName.SliceCount = botCount;
        }

        protected void initClientUserSliceCount(int index, int SliceCount)
        {
            FUserName[index].SliceCount = SliceCount;
            FFirstName[index].SliceCount = SliceCount;
            FLastName[index].SliceCount = SliceCount;
            FUser[index].SliceCount = SliceCount;

            FUser[index] = new Spread<User>();
            FUserName[index] = new Spread<string>();
            FFirstName[index] = new Spread<string>();
            FLastName[index] = new Spread<string>();
        }

        protected void setUserData(int index, User from)
        {
            FUserName[index].Add(from.Username);
            FFirstName[index].Add(from.FirstName);
            FLastName[index].Add(from.LastName);

            FUser[index].Add(from);
        }

        protected abstract void checkForMessage(int i);
        protected abstract void setMessageTypeSliceCount(int botCount);
        protected abstract void initClientReceivedMessages(int index, int SliceCount);

    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveText", Category = "Telegram", Version = "", Help = "Receives Text Messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveTextNode : TelegramReceiveNode
    {
        [Output("Text")]
        public ISpread<ISpread<string>> FTextMessage;

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FTextMessage.SliceCount = botCount;
        }

        protected override void initClientReceivedMessages(int index, int SliceCount)
        {
            FTextMessage[index].SliceCount = SliceCount;
            FTextMessage[index] = new Spread<string>();
        }

        protected override void checkForMessage(int i)
        {
            var last = FBotClient[i].Messages;
            var textMessages = last.Where(textMessage => textMessage.message.Type == MessageType.TextMessage);
            int messageCount = textMessages.Count();

            if (messageCount < 1) return;

            initClientReceivedMessages(i, messageCount);
            initClientUserSliceCount(i, messageCount);

            foreach (VTelegramMessage tm in textMessages)
            {
                var m = tm.message;
                FTextMessage[i].Add(m.Text);

                setUserData(i, m.From);
                FLogger.Log(LogType.Debug, "Bot " + i + ": Text message received");
            }

            FReceived[i] = true;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveLocation", Category = "Telegram", Version = "", Help = "Receives location messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveLocationNode : TelegramReceiveNode
    {
        [Output("Longitude")]
        public ISpread<ISpread<double>> FLong;
        [Output("Latitude")]
        public ISpread<ISpread<double>> FLat;

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FLong.SliceCount = botCount;
            FLat.SliceCount = botCount;
        }

        protected override void initClientReceivedMessages(int index, int SliceCount)
        {
            FLong[index].SliceCount = SliceCount;
            FLat[index].SliceCount = SliceCount;
            FLong[index] = new Spread<double>();
            FLat[index] = new Spread<double>();
        }

        protected override void checkForMessage(int i)
        {
            var last = FBotClient[i].Messages;
            var locationMessages = last.Where(locationMessage => locationMessage.message.Type == MessageType.LocationMessage);
            int messageCount = locationMessages.Count();

            if (messageCount < 1) return;

            initClientReceivedMessages(i, messageCount);
            initClientUserSliceCount(i, messageCount);

            foreach (VTelegramMessage tm in locationMessages)
            {
                var m = tm.message;
                FLong[i].Add(m.Location.Longitude);
                FLat[i].Add(m.Location.Latitude);

                setUserData(i, m.From);
                FLogger.Log(LogType.Debug, "Bot " + i + ": Location message received");
            }
            
            FReceived[i] = true;
        }
    }

    public class TelegramFile
    {
        public Telegram.Bot.Types.File file;
        public TelegramBotClient botClient;

        public TelegramFile(Telegram.Bot.Types.File f, TelegramBotClient bc)
        {
            file = f;
            botClient = bc;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceivePhoto", Category = "Telegram", Version = "", Help = "Receives photo messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceivePhotoNode : TelegramReceiveNode
    {
        
        [Output("File")]
        public ISpread<ISpread<TelegramFile>> FFile;
        [Output("Dimensions")]
        public ISpread<ISpread<Vector2D>> FDimensions;
        [Output("Photo Count")]
        public ISpread<ISpread<int>> FPhotoCount;

        protected override void setMessageTypeSliceCount(int botCount)
        {
            FDimensions.SliceCount = botCount;
            FFile.SliceCount = botCount;
            FPhotoCount.SliceCount = botCount;
        }

        protected override void initClientReceivedMessages(int index, int SliceCount)
        {
            FDimensions[index].SliceCount = SliceCount;
            FDimensions[index] = new Spread<Vector2D>();

            FFile[index].SliceCount = SliceCount;
            FFile[index] = new Spread<TelegramFile>();

            FPhotoCount[index].SliceCount = SliceCount;
            FPhotoCount[index] = new Spread<int>();
        }

        protected override void checkForMessage(int i)
        {
            var last = FBotClient[i].Messages;
            List<VTelegramMessage> photoMessages = getFileMessages(i);
            int messageCount = photoMessages.Count();

            if (messageCount < 1) return;

            initClientReceivedMessages(i, messageCount);
            initClientUserSliceCount(i, messageCount);
            

            foreach(TelegramMessage tm in photoMessages)
            {
                var m = tm.message;
                PhotoSize[] ps = m.Photo;
                int photoCount = 0;
                
                foreach (PhotoSize p in ps)
                {
                    FDimensions[i].Add(new Vector2D((double)p.Width, (double)p.Height));
                    FFile[i].Add(new TelegramFile(p, FBotClient[i].BC));
                    photoCount++;
                }

                FPhotoCount[i].Add(photoCount);
                setUserData(i, m.From);
                FLogger.Log(LogType.Debug, "Bot " + i + ": Photo message with " + photoCount + " photos received");

            }

            FReceived[i] = true;
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
        [Input("Get")]
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
		

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        public void OnImportsSatisfied()
        {
            FFileData.SliceCount = 0;
        }

        public void Evaluate(int SpreadMax)
        {
            FFileId.SliceCount = FFile.SliceCount;
            FFilePath.SliceCount = FFile.SliceCount;
            FFileSize.SliceCount = FFile.SliceCount;
            FFileData.ResizeAndDispose(FFile.SliceCount, () => new MemoryStream());
            int count = 0;

            int max = Math.Max(FFile.SliceCount, FGet.SliceCount);

            for(int i=0; i < FFile.SliceCount; i++)
            {
                var f = FFile[i];
                if (f == null || FGet[i] == false)
                    continue;

                DownloadFileAsync(f, FFileData[i]);
            }
                
        }

        public async void DownloadFileAsync(TelegramFile f, Stream output)
        {
            
            Telegram.Bot.Types.File thisFile = await f.botClient.GetFileAsync(f.file.FileId);

            FFileId.Add(f.file.FileId);
            FFilePath.Add(f.file.FilePath);
            FFileSize.Add(f.file.FileSize);

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
