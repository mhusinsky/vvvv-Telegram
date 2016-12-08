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
    #region PluginInfo
    [PluginInfo(Name = "Callback", Category = "Telegram", Version = "", Help = "Callback node", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramCallbackNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("Bots")]
        public ISpread<BotClient> FClient;

        [Output("Chat Instance")]
        public ISpread<string> FChatInstance;
        [Output("Data")]
        public ISpread<string> FData;
        [Output("Id")]
        public ISpread<string> FId;
        [Output("From")]
        public ISpread<User> FUser;
        [Output("Received", IsBang = true)]
        public ISpread<bool> FReceived;

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        public void Evaluate(int SpreadMax)
        {
            setCallbacksSliceCount(FClient.SliceCount);

            for (int i = 0; i < FClient.SliceCount; i++)
            {
                FReceived[i] = false;
                if (FClient[i] == null)
                    return;
                checkForCallback(i);
            }

        }

        protected void setCallbacksSliceCount(int botCount)
        {
            FChatInstance.SliceCount = botCount;
            FData.SliceCount = botCount;
            FId.SliceCount = botCount;
            FUser.SliceCount = botCount;
            FReceived.SliceCount = botCount;
        }

        protected async void checkForCallback(int i)
        {
            var callbacks = getCallbackList(i);

            foreach (VTelegramCallback cb in callbacks)
            {
                var c = cb.callback;
                FData[i] = c.Data;
                FId[i] = c.Id;
                FUser[i] = c.From;
                autoAction(i, c);
            }
        }

        protected virtual List<VTelegramCallback> getCallbackList(int i)
        {
            return FClient[i].Callbacks.ToList();
        }

        protected virtual void autoAction(int i, CallbackQuery c) { }

    }

    #region PluginInfo
    [PluginInfo(Name = "AnswerCallback", Category = "Telegram", Version = "", Help = "Displays a notification as a result for an inline keyboard", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramInlineKeyboardNotificationNode : TelegramCallbackNode
    {
        [Input("Text")]
        public ISpread<string> FText;

        [Input("Echo selection")]
        public ISpread<bool> FEcho;

        protected override async void autoAction(int i, CallbackQuery c) 
        {
            try
            {
                string echo = FEcho[i] ? c.Data : "";
                await FClient[i].BC.AnswerCallbackQueryAsync(c.Id, FText[i] + echo);
            }
            catch (Exception e) { }
        }
        
    }
}