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
        bool Success;
        enum TradeAction { CancelTrade, AddItem, RemoveItem, SetReady, AcceptTrade, SendMessage };

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

        /// <summary>
        /// Adds all items from the given list.
        /// </summary>
        /// <returns>Number of items added.</returns>
        private uint AddItemsFromList(List<Inventory.Item> items)
        {
            Log.Debug("Method called");
            Log.Debug("" + items.Count);
            uint added = 0;

            foreach (Inventory.Item item in items)
            {
                if (item != null && !item.IsNotTradeable)
                {
                    if (TryAction(TradeAction.AddItem, item))
                    {
                        Log.Debug("Item successfully added");
                        added++;
                    }
                    else
                    {
                        Log.Debug("ADDING FAILED, returning to cancel");
                        return 0;
                    }
                }
            }
            return added;
        }

        /// <summary>
        /// Gets all tradeable items other than normal crates.
        /// </summary>
        /// <returns>List of items to add.</returns>
        public List<Inventory.Item> GetAllNonCrates(Inventory inv)
        {
            var items = new List<Inventory.Item>();
            foreach (Inventory.Item item in inv.Items)
            {
                if (item.Defindex != 5022 && item.Defindex != 5041 && item.Defindex != 5045 && item != null && !item.IsNotTradeable)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        private bool TryAction(TradeAction action, Inventory.Item item = null, bool ready = false, string message = null)
        {
            Log.Debug("Action: " + action);
            int x = 0;
            Success = false;
            while (Success == false && x < 5)
            {
                x++;
                try
                {
                    switch (action)
                    {
                        case TradeAction.CancelTrade:
                            Success = Trade.CancelTrade(); break;
                        case TradeAction.AddItem:
                            Success = Trade.AddItem(item.Id); break;
                        case TradeAction.RemoveItem:
                            Success = Trade.RemoveItem(item.Id); break;
                        case TradeAction.SetReady:
                            Success = Trade.SetReady(ready); break;
                        case TradeAction.AcceptTrade:
                            Success = Trade.AcceptTrade(); break;
                        case TradeAction.SendMessage:
                            Success = Trade.SendMessage(message); break;
                        default: Log.Error("Invalid trade action: " + action); break;
                    }
                }
                catch (TradeException te)
                {
                    Log.Warn(action + " failed.");
                    Log.Debug(string.Format("Loop #{0}/nException:{1}", x, te));
                }
                catch (Exception e)
                {
                    Log.Warn(action + " failed.");
                    Log.Debug(string.Format("Loop #{0}/nException:{1}", x, e));
                }
            }
            if (!Success)
            {
                Log.Error("Could not " + action);
                if (action != TradeAction.CancelTrade)
                    TryAction(TradeAction.CancelTrade);
            }
            return Success;
        }
    }

}