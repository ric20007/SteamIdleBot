using System;
using SteamKit2;
using System.Collections.Generic;
using SteamTrade;
using SteamTrade.Exceptions;
using System.Threading;

namespace SteamBot
{
    public class GivingUserHandler : UserHandler
    {
        public GivingUserHandler(Bot bot, SteamID sid) : base(bot, sid) 
        {
            Success = false;
            //Just makes referencing the bot's own SID easier.
            mySteamID = Bot.SteamUser.SteamID;
        }

        public override void OnLoginCompleted()
        {
            Bot.GetInventory();
            List<Inventory.Item> itemsToTrade = new List<Inventory.Item>();
            itemsToTrade = GetAllNonCrates(Bot.MyInventory);
            botItemMap.Add(mySteamID, itemsToTrade);
            Log.Info("[Giving] SteamID: " + mySteamID + " checking in. " + botItemMap.Count + " of " + Bot.numBots + " Bots.");
            Admins.Add(mySteamID);
            if (botItemMap[mySteamID].Count > 0)
            {
                tradeReadyBots.Add(mySteamID);
                Log.Info("SteamID: " + mySteamID + " has items. Added to list." + tradeReadyBots.Count + " Bots waiting to trade.");
            }
            else
            {
                Log.Info("SteamID: " + mySteamID + " did not have a trade-worthy item.");
                Log.Info("Stopping bot.");
                Bot.StopBot();
            }
        }

        public override void OnChatRoomMessage(SteamID chatID, SteamID sender, string message)
        {
            //Log.Info(Bot.SteamFriends.GetFriendPersonaName(sender) + ": " + message);
            //base.OnChatRoomMessage(chatID, sender, message);
        }

        public override bool OnFriendAdd()
        {
            if (IsAdmin)
            {
                return true;
            }
            return false;
        }

        public override void OnFriendRemove() { }

        public override void OnMessage(string message, EChatEntryType type) 
        {
            if ((OtherSID == PrimaryAltSID) && (message == "ready"))
            {
                Bot.SteamFriends.SendChatMessage(PrimaryAltSID, EChatEntryType.ChatMsg, "ready");
            }
        }

        public override bool OnTradeRequest()
        {
            Thread.Sleep(1000);
            if (IsAdmin)
            {
                return true;
            }
            return false;
        }

        public override void OnTradeError(string error)
        {
            Bot.SteamFriends.SendChatMessage(OtherSID,
                                              EChatEntryType.ChatMsg,
                                              "Oh, there was an error: " + error + "."
                                              );
            Log.Warn(error);
        }

        public override void OnTradeTimeout()
        {
            //Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg,
            //                                  "Trade timeout.");
            Log.Warn("Trade timeout.");
            Log.Debug("Something's gone wrong.");
            Bot.GetInventory();
            if (GetAllNonCrates(Bot.MyInventory).Count > 0)
            {
                Log.Debug("Still have items to trade");
                //errorOcccured = true;
                TryAction(TradeAction.CancelTrade);
                OnTradeClose();
            }
            else
            {
                Log.Debug("No items in inventory, removing");
                tradeReadyBots.Remove(mySteamID);
                TryAction(TradeAction.CancelTrade);
                OnTradeClose();
                Bot.StopBot();
            }
        }

        public override void  OnTradeClose()
        {
            Log.Warn ("[Giving] TRADE CLOSED");
            Bot.CloseTrade ();
            // traded = true;
        }

        public override void OnTradeInit()
        {
            Thread.Sleep(500);
            Log.Debug("Adding all non crates");
            uint added = AddItemsFromList(botItemMap[mySteamID]);
            if (added > 0)
            {
                Log.Info("Added " + added + " items.");
                System.Threading.Thread.Sleep(50);
                if (!TryAction(TradeAction.SendMessage, null, false, "ready"))
                    OnTradeClose();
            }
            else
            {
                Log.Debug("Something's gone wrong.");
                Bot.GetInventory();
                if (GetAllNonCrates(Bot.MyInventory).Count > 0)
                {
                    Log.Debug("Still have items to trade, aborting trade.");
                    //errorOcccured = true;
                    Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "failed");
                    OnTradeClose();
                }
                else
                {
                    Log.Debug("No items in bot inventory. This shouldn't be possible.");
                    tradeReadyBots.Remove(mySteamID);
                    TryAction(TradeAction.CancelTrade);
                    OnTradeClose();
                    Bot.StopBot();
                }
            }
        }

        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeMessage(string message) 
        {
            System.Threading.Thread.Sleep(100);
            Log.Debug("Message Received: " + message);
            if (message == "ready")
            {
                if (!TryAction(TradeAction.SetReady, null, true))
                    OnTradeClose();
            }
        }

        public override void OnTradeReady(bool ready)
        {
            Log.Debug("OnTradeReady");
            Thread.Sleep(100);
            if (ready && IsAdmin)
            {
                TradeAccept();
            }
        }

        public override void OnTradeAccept()
        {
            tradeReadyBots.Remove(mySteamID);
            OnTradeClose();
        }
        public void TradeAccept()
        {
            Thread.Sleep(100);
            Success = TryAction(TradeAction.AcceptTrade);
            if (Success)
            {
                Log.Success("Trade was Successful!");
                //Trade.Poll();
                //Bot.StopBot();
            }
            else
            {
                Log.Warn("Trade might have failed.");
                Bot.GetInventory();
                if (GetAllNonCrates(Bot.MyInventory).Count == 0)
                {
                    Log.Warn("Bot has no items, trade may have succeeded. Removing bot.");
                    tradeReadyBots.Remove(mySteamID);
                    OnTradeClose();
                    Bot.StopBot();
                }
            }
        }
    }

}