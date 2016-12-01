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
    public enum ReplyMarkupEnum
    {
        none,
        ForceReply,
        ReplyKeyboardHide,
        UseKeyboard
    }

    public abstract class TelegramKeyboardNode : IPluginEvaluate
    {
        [Input("String", Order = 0)]
        public ISpread<ISpread<String>> FText;

        [Input("Update", IsBang = true, IsSingle = true, Order = 100)]
        public IDiffSpread<Boolean> FUpdate;

        [Output("Keyboard")]
        public ISpread<IReplyMarkup> FMarkup;

        public void OnImportsSatisfied()
        {
            FMarkup.SliceCount = 1;
            FMarkup[0] = new ReplyKeyboardMarkup();
        }

        public void Evaluate(int SpreadMax)
        {
            FMarkup.SliceCount = 1;

            if (FUpdate[0])
            {
                createKeyboard();
            }
        }
        protected abstract void createKeyboard();
    }

    #region PluginInfo
    [PluginInfo(Name = "ReplyKeyboard", Category = "Telegram", Version = "", Help = "Creates Custom Keyboards", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramReplyKeyboardNode : TelegramKeyboardNode
    {
        [Input("ResizeKeyboard", IsSingle = true, DefaultBoolean = true, Order = 10)]
        public IDiffSpread<Boolean> FResize;

        [Input("OneTimeKeyboard", IsSingle = true, Order = 20)]
        public IDiffSpread<Boolean> FOneTime;

        
        protected override void createKeyboard()
        {
            FMarkup.SliceCount = 1;

            if (FUpdate[0])
            {
                KeyboardButton[][] buttons = new KeyboardButton[FText.SliceCount][];

                for (int i = 0; i < FText.SliceCount; i++)
                {
                    buttons[i] = new KeyboardButton[FText[i].SliceCount];
                    for (int j = 0; j < FText[i].SliceCount; j++)
                    {
                        buttons[i][j] = new KeyboardButton(FText[i][j]);
                    }
                }
                FMarkup[0] = new ReplyKeyboardMarkup(buttons, FResize[0], FOneTime[0]); // TODO: Parameter SelectiveKeyboard
            }
        }
    }

    #region PluginInfo
    [PluginInfo(Name = "InlineKeyboard", Category = "Telegram", Version = "", Help = "Creates Custom Keyboards", Credits = "Based on telegram.bot", Tags = "Network, Bot", Author = "motzi", AutoEvaluate = true)]
    #endregion PluginInfo
    public class TelegramInlineKeyboardNode : TelegramKeyboardNode
    {


        protected override void createKeyboard()
        {
            FMarkup.SliceCount = 1;

            if (FUpdate[0])
            {
                InlineKeyboardButton[][] inlineButtons = new InlineKeyboardButton[FText.SliceCount][];

                for (int i = 0; i < FText.SliceCount; i++)
                {
                    inlineButtons[i] = new InlineKeyboardButton[FText[i].SliceCount];
                    for (int j = 0; j < FText[i].SliceCount; j++)
                    {
                        inlineButtons[i][j] = new InlineKeyboardButton(FText[i][j]);
                    }
                }
                FMarkup[0] = new InlineKeyboardMarkup(inlineButtons);
            }
        }
    }
}
