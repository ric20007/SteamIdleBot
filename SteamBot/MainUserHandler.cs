using System;
using SteamKit2;
using System.Collections.Generic;
using SteamTrade;
using SteamTrade.Exceptions;
using System.Threading;

namespace SteamBot
{
    public class MainUserHandler : UserHandler
    {
        public MainUserHandler(Bot bot, SteamID sid) : base(bot, sid) 
        {
            mySteamID = Bot.SteamUser.SteamID;
            Bot.Admins.Add(mySteamID);
        }

        public override bool OnFriendAdd()
        {
            return false;
        }

        public override void OnLoginCompleted()
        {
            Log.Info("[Main] SteamID: " + mySteamID + " checking in.");
            Admins.Add(mySteamID);
            MainSID = Bot.SteamUser.SteamID;
        }

        public override void OnChatRoomMessage(SteamID chatID, SteamID sender, string message) {  }

        public override void OnFriendRemove() { }

        public override void OnMessage(string message, EChatEntryType type) {  }

        public override bool OnTradeRequest()
        {
            if (PrimaryAltSID == OtherSID)
            {
                return true;
            }
            return false;
        }

        public override void OnTradeError(string error)
        {
            Log.Warn(error);
        }

        public override void OnTradeTimeout()
        {
            Log.Info("User was kicked because he was AFK.");
        }

        public override void OnTradeInit() {  }

        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeMessage(string message) 
        {
            System.Threading.Thread.Sleep(100);
            Log.Debug("Message Received: " + message);
            if (message == "ready")
            {
                if (!SendMessage("ready"))
                {
                    CancelTrade();
                    OnTradeClose();
                }
            }
        }

        public override void OnTradeReady(bool ready)
        {
            if (OtherSID == PrimaryAltSID)
            {
                SetReady(true);
            }
        }

        public override void OnTradeAccept()
        {
            bool success = AcceptTrade();

            if (success)
            {
                Log.Success("Trade was Successful!");
                OnTradeClose();
                Bot.StopBot();
            }
            else
            {
                Log.Warn("Trade might have failed.");
                OnTradeClose();
            }
        }

        #region Basic Trade Functions
        /// <summary>
        /// Cancels Trade, retrying 10 times if neccessary
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool CancelTrade()
        {
            Log.Debug("Cancelling trade");
            int x = 0;
            Success = false;
            while (Success == false && x < 10)
            {
                try
                {
                    Log.Debug("Loop #" + x);
                    x++;
                    Success = Trade.CancelTrade();
                }
                catch (TradeException te)
                {
                    Log.Debug("Loop #" + x);
                    Log.Warn("Cancel Trade failed.");
                    Log.Debug("" + te);
                }
                catch (Exception e)
                {
                    Log.Warn("Cancel Trade attempt failed");
                    var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
                    Log.Debug(s);
                }
            }
            if (!Success)
            {
                Log.Error("Could not cancel trade");
            }
            return Success;
        }
        /// <summary>
        /// Sets Ready, retrying 10 times if neccessary
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool SetReady(bool ready)
        {
            Log.Debug("Setting ready");
            int x = 0;
            Success = false;
            while (Success == false && x < 10)
            {
                try
                {
                    Log.Debug("Loop #" + x);
                    x++;
                    Success = Trade.SetReady(ready);
                }
                catch (TradeException te)
                {
                    Log.Debug("Loop #" + x);
                    Log.Warn("Setting ready failed.");
                    Log.Debug("" + te);
                }
                catch (Exception e)
                {
                    Log.Warn("Setting ready failed");
                    var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
                    Log.Debug(s);
                }
            }
            if (!Success)
            {
                Log.Error("Could not set ready");
            }
            return Success;
        }
        /// <summary>
        /// Sends a trade message, retrying 10 times if neccessary
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool SendMessage(string message)
        {
            Log.Debug("Sending message:" + message);
            int x = 0;
            Success = false;
            while (Success == false && x < 10)
            {
                try
                {
                    Log.Debug("Loop #" + x);
                    x++;
                    Success = Trade.SendMessage(message);
                }
                catch (TradeException te)
                {
                    Log.Debug("Loop #" + x);
                    Log.Warn("Sending message failed.");
                    Log.Debug("" + te);
                }
                catch (Exception e)
                {
                    Log.Warn("Sending message failed");
                    var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
                    Log.Debug(s);
                }
            }
            if (!Success)
            {
                Log.Error("Could not send message");
            }
            return Success;
        }
        /// <summary>
        /// Accepts a trade, retrying 10 times if neccessary
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool AcceptTrade()
        {
            Log.Debug("Accepting Trade");
            int x = 0;
            Success = false;
            while (Success == false && x < 10)
            {
                try
                {
                    Log.Debug("Loop #" + x);
                    x++;
                    Success = Trade.AcceptTrade();
                }
                catch (TradeException te)
                {
                    Log.Debug("Loop #" + x);
                    Log.Warn("Trade Accept failed.");
                    Log.Debug("" + te);
                }
                catch (Exception e)
                {
                    Log.Warn("Trade Accept failed");
                    var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
                    Log.Debug(s);
                }
            }
            if (!Success)
            {
                Log.Error("Could not accept trade");
            }
            return Success;
        }
        /// <summary>
        /// Adds an item, retrying 10 times if neccessary
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool AddItem(Inventory.Item item)
        {
            int x = 0;
            Success = false;
            Log.Debug("Adding item");
            while (Success == false && x < 10)
            {
                try
                {
                    Log.Debug("Loop #" + x);
                    x++;
                    Success = Trade.AddItem(item.Id);
                }
                catch (TradeException te)
                {
                    Log.Debug("Loop #" + x);
                    Log.Warn("Add Item failed.");
                    Log.Debug("" + te);
                }
                catch (Exception e)
                {
                    Log.Warn("Add Item failed.");
                    var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
                    Log.Debug(s);
                }
            }
            if (!Success)
            {
                Log.Error("Could not add item");
            }
            return Success;
        }
        /// <summary>
        /// Removes an item, retrying 10 times if neccessary
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool RemoveItem(Inventory.Item item)
        {
            int x = 0;
            Success = false;
            Log.Debug("Removing item");
            while (Success == false && x < 10)
            {
                try
                {
                    Log.Debug("Loop #" + x);
                    x++;
                    Success = Trade.RemoveItem(item.Id);
                }
                catch (TradeException te)
                {
                    Log.Debug("Loop #" + x);
                    Log.Warn("Remove Item failed.");
                    Log.Debug("" + te);
                }
                catch (Exception e)
                {
                    Log.Warn("Remove Item failed");
                    var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
                    Log.Debug(s);
                }
            }
            if (!Success)
            {
                Log.Error("Could not remove item");
            }
            return Success;
        }
        #endregion

    }

}
