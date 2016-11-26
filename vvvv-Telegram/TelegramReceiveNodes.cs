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


namespace VVVV.Nodes
{

    public abstract class TelegramReceiveNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("Bots")]
        public ISpread<BotClient> FBotClient;

        [Output("User Name")]
        public ISpread<String> FUserName;
        [Output("First Name")]
        public ISpread<String> FFirstName;
        [Output("Last Name")]
        public ISpread<String> FLastName;
        [Output("Received", IsBang = true)]
        public ISpread<bool> FReceived;


        [Import()]
        public ILogger FLogger;
        #endregion fields & pins


        public void Evaluate(int SpreadMax)
        {

            FReceived.SliceCount = FBotClient.SliceCount;
            FUserName.SliceCount = FBotClient.SliceCount;
            FFirstName.SliceCount = FBotClient.SliceCount;
            FLastName.SliceCount = FBotClient.SliceCount;
            setReceivedMessagesSliceCount(FBotClient.SliceCount);

            for (int i = 0; i < FBotClient.SliceCount; i++)
            {
                FReceived[i] = false;
                checkForMessage(i);
            }

        }

        protected abstract void checkForMessage(int i);
        protected abstract void setReceivedMessagesSliceCount(int SliceCount);

    }

    #region PluginInfo
    [PluginInfo(Name = "ReceiveText", Category = "Telegram", Version = "", Help = "Receives Text Messages", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReceiveTextNode : TelegramReceiveNode
    {
        [Output("Text", DefaultString = "Your Message")]
        public ISpread<string> FTextMessage;

        protected override void setReceivedMessagesSliceCount(int SliceCount)
        {
            FTextMessage.SliceCount = SliceCount;
        }

        protected override void checkForMessage(int i)
        {

            if (FBotClient[i] == null)
                return;

            var message = FBotClient[i].RetrieveTextMessage();

            if(message != null)
            {
                FTextMessage[i] = message.Text;
                FUserName[i] = message.From.Username;
                FFirstName[i] = message.From.FirstName;
                FLastName[i] = message.From.LastName;
                FReceived[i] = true;
                FLogger.Log(LogType.Debug, "Bot " + i + ": Text Message received");
            }

        }


    }

}
