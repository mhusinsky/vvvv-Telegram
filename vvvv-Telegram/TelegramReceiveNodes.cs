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
            var textMessageCount = last.Where(textMessage => textMessage.message.Type == MessageType.TextMessage).Count();

            if (textMessageCount < 1) return;

            initClientReceivedMessages(i, textMessageCount);
            initClientUserSliceCount(i, textMessageCount);
            
            for(int m=0; m < FBotClient[i].lastMessages.Count; i++ )
            {
                var current = FBotClient[i].lastMessages[m].message;
                if (current.Type == MessageType.TextMessage)
                {
                    FTextMessage[i].Add(current.Text);
                    setUserData(i, current.From);

                    FLogger.Log(LogType.Debug, "Bot " + i + ": Text Message received");
                    last.RemoveAt(m);
                }
            }
            
            FReceived[i] = true;
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveLocation", Category = "Telegram", Version = "", Help = "Receives Location Messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
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
            var locationMessageCount = last.Where(locationMessage => locationMessage.message.Type == MessageType.LocationMessage).Count();

            if (locationMessageCount < 1) return;

            initClientReceivedMessages(i, locationMessageCount);
            initClientUserSliceCount(i, locationMessageCount);

            for (int m = 0; m < FBotClient[i].lastMessages.Count; i++)
            {
                var current = FBotClient[i].lastMessages[m].message;
                if (current.Type == MessageType.LocationMessage)
                {
                    FLong[i].Add(current.Location.Longitude);
                    FLat[i].Add(current.Location.Latitude);
                    setUserData(i, current.From);

                    FLogger.Log(LogType.Debug, "Bot " + i + ": Location Message received");
                    last.RemoveAt(m);
    #region PluginInfo
    [PluginInfo(Name = "ReceivePhoto", Category = "Telegram", Version = "", Help = "Receives photo messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceivePhotoNode : TelegramReceiveNode
    {
        
        [Output("File")]
        public ISpread<ISpread<Telegram.Bot.Types.File>> FFile;
        [Output("Dimensions")]
        public ISpread<ISpread<Vector2D>> FDimensions;
        [Output("Message Bin Size")]
        public ISpread<int> FBin;



        protected override void setMessageTypeSliceCount(int botCount)
        {
            FDimensions.SliceCount = botCount;
            FFile.SliceCount = botCount;
        }

        protected override void initClientReceivedMessages(int index, int SliceCount)
        {
            FDimensions[index].SliceCount = SliceCount;
            FDimensions[index] = new Spread<Vector2D>();

            FFile[index].SliceCount = SliceCount;
            FFile[index] = new Spread<Telegram.Bot.Types.File>();
        }

        protected override void checkForMessage(int i)
        {
            var last = FBotClient[i].lastMessages;
            var photoMessages = last.Where(photoMessage => photoMessage.message.Type == MessageType.PhotoMessage);
            int count = photoMessages.Count();

            if (photoMessages.Count() < 1) return;

            initClientReceivedMessages(i, count);
            initClientUserSliceCount(i, count);
            FBin.SliceCount = count;
            int c = 0;

            foreach(TelegramMessage tm in photoMessages)
            {
                var m = tm.message;
                PhotoSize[] ps = m.Photo;
                FBin[c] = 0;

                foreach (PhotoSize p in ps)
                {
                    FDimensions[i].Add(new Vector2D((double)p.Width, (double)p.Height));
                    FFile[i].Add(p);
                    FBin[c]++;
                }

                setUserData(i, m.From);

                c++;
            }

            FLogger.Log(LogType.Debug, "Bot " + i + ": Photo message with " + c+1 + " photos received");

            //for (int m = FBotClient[i].lastMessages.Count; m >= 0 ; m--)
            //{
            //    var current = FBotClient[i].lastMessages[m].message;
            //    if (current.Type == MessageType.PhotoMessage)
            //    {
            //        PhotoSize[] ps = current.Photo;
            //        FBin[m] = 0;

            //        foreach(PhotoSize p in ps)
            //        {
            //            FDimensions[i].Add(new Vector2D((double)p.Width, (double)p.Height));
            //            FFile[i].Add(p);
            //            FBin[m]++;
            //        }

            //        setUserData(i, current.From);

            //        FLogger.Log(LogType.Debug, "Bot " + i + ": Photo message received");
            //        last.RemoveAt(m);
            //    }
            //}

            FReceived[i] = true;
        }
    }

}
