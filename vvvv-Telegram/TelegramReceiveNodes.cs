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

        [Output("User Name")]
        public ISpread<ISpread<string>> FUserName;
        [Output("First Name")]
        public ISpread<ISpread<string>> FFirstName;
        [Output("Last Name")]
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
            var last = FBotClient[i].lastMessages;
            var textMessages = last.Where(textMessage => textMessage.message.Type == MessageType.TextMessage);
            int messageCount = textMessages.Count();

            if (messageCount < 1) return;

            initClientReceivedMessages(i, messageCount);
            initClientUserSliceCount(i, messageCount);

            foreach (TelegramMessage tm in textMessages)
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
            var last = FBotClient[i].lastMessages;
            var locationMessages = last.Where(locationMessage => locationMessage.message.Type == MessageType.LocationMessage);
            int messageCount = locationMessages.Count();

            if (messageCount < 1) return;

            initClientReceivedMessages(i, messageCount);
            initClientUserSliceCount(i, messageCount);

            foreach (TelegramMessage tm in locationMessages)
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

    #region PluginInfo
    [PluginInfo(Name = "ReceivePhoto", Category = "Telegram", Version = "", Help = "Receives photo messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceivePhotoNode : TelegramReceiveNode
    {
        
        [Output("File")]
        public ISpread<ISpread<Telegram.Bot.Types.File>> FFile;
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
            FFile[index] = new Spread<Telegram.Bot.Types.File>();

            FPhotoCount[index].SliceCount = SliceCount;
            FPhotoCount[index] = new Spread<int>();
        }

        protected override void checkForMessage(int i)
        {
            var last = FBotClient[i].lastMessages;
            var photoMessages = last.Where(photoMessage => photoMessage.message.Type == MessageType.PhotoMessage);
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
                    FFile[i].Add(p);
                    photoCount++;
                }

                FPhotoCount[i].Add(photoCount);
                setUserData(i, m.From);
                FLogger.Log(LogType.Debug, "Bot " + i + ": Photo message with " + photoCount + " photos received");

            }

            FReceived[i] = true;
        }
    }

}
