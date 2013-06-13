using System;
using SteamKit2;
using System.Collections.Generic;
using SteamTrade;
using SteamTrade.Exceptions;
using System.Threading;

namespace SteamBot
{
    public class ReceivingUserHandler : UserHandler
    {
        int totalAdded = 0;

        public ReceivingUserHandler(Bot bot, SteamID sid) : base(bot, sid) 
        {
            Success = false;
            mySteamID = Bot.SteamUser.SteamID;
            PrimaryAltSID = mySteamID;

            // Check for any admins to add other than Bots
            if (Bot.Admins != null)
            {
                foreach (ulong admin in Bot.Admins)
                {
                    Admins.Add(admin);
                }
            }
        }

        public override void OnLoginCompleted()
        {
            Log.Debug("attend: " + botItemMap.Count);
            Log.Debug("num: " + Bot.numBots);
            Log.Info("[Receiving] SteamID: " + mySteamID + " checking in.");
            Admins.Add(mySteamID);

            // Loop until another bot is ready to trade, or all bots have fully loaded.
            while (tradeReadyBots.Count == 0 && (botItemMap.Count < Bot.numBots))
            {
                Log.Info("Waiting for bots...");
                Log.Debug("attend: " + botItemMap.Count);
                Log.Debug("num: " + Bot.numBots);
                Thread.Sleep(1000);
            }
            if (tradeReadyBots.Count > 0)
            {
                BeginNextTrade(tradeReadyBots[0]);
            }
            else
            {
                Log.Error("No Bots available for trade.");
                Bot.StopBot();
            }
        }

        public override void OnChatRoomMessage(SteamID chatID, SteamID sender, string message)
        {
            Log.Info(Bot.SteamFriends.GetFriendPersonaName(sender) + ": " + message);
            base.OnChatRoomMessage(chatID, sender, message);
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
            if (IsAdmin)
            {
                if (message == "failed")
                {
                    TryAction(TradeAction.CancelTrade);
                    OnTradeClose();
                }
            }
            else
            {
                Bot.SteamFriends.SendChatMessage(OtherSID, type, "Invalid: User is not Admin");
            }
        }

        public override bool OnTradeRequest()
        {
            if (IsAdmin)
            {
                return true;
            }
            return false;
        }

        public override void OnTradeError(string error)
        {
            Log.Warn("OnTradeError: " + error);
            if (OtherSID != MainSID)
            {
                TryAction(TradeAction.CancelTrade);
                OnTradeClose();
            }
            else
            {
                Log.Error("Trade with Main account failed. Not retrying.");
            }
        }

        public override void OnTradeTimeout()
        {
            //Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg,
            //                                  "Trade timeout.");
            Log.Warn("Trade timeout.");
            Log.Debug("Something's gone wrong.");
            Bot.GetOtherInventory(OtherSID);
            if (GetAllNonCrates(Bot.OtherInventory).Count > 0)
            {
                Log.Debug("Still has items to trade");
                //errorOcccured = true;
            }
            else
            {
                Log.Debug("No items in inventory, removing");
                if (tradeReadyBots.Contains(OtherSID))
                { 
                    tradeReadyBots.Remove(OtherSID); 
                }
            }
            if (OtherSID != MainSID)
            {
                TryAction(TradeAction.CancelTrade);
                OnTradeClose();
            }
        }

        public override void OnTradeClose()
        {
            Log.Warn("[Receiving] TRADE CLOSED");
            Bot.CloseTrade();
            Thread.Sleep(150);

            if (OtherSID != MainSID)
            {
                if (tradeReadyBots.Count > 0)
                {
                    BeginNextTrade(tradeReadyBots[0]);
                }
                else if (botItemMap.Count < Bot.numBots)
                {
                    // Wait for the rest of the bots
                    while ((tradeReadyBots.Count == 0) && (botItemMap.Count < Bot.numBots))
                    {
                        Log.Info("Waiting for bots...");
                        Log.Debug("Bot count: " + botItemMap.Count + " of " + Bot.numBots);
                        Thread.Sleep(1000);
                    }
                    if (tradeReadyBots.Count > 0)
                    {
                        BeginNextTrade(tradeReadyBots[0]);
                    }
                    else
                    {
                        Log.Info("Trade List is empty");
                        Log.Success("All Bots have traded. Items moved: " + totalAdded);
                        FinishTrades();
                    }
                }
                else
                {
                    Log.Info("Trade List is empty");
                    Log.Success("All Bots have traded. Items moved: " + totalAdded);
                    FinishTrades();
                }
            }
        }

        public override void OnTradeInit()
        {
            if (OtherSID == MainSID)
            {
                Thread.Sleep(500);
                Bot.GetInventory();
                Log.Debug("Adding all non crates");
                List<Inventory.Item> AllItems = GetAllNonCrates(Bot.MyInventory);
                uint added = AddItemsFromList(AllItems);
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
                        TryAction(TradeAction.CancelTrade);
                        OnTradeClose();
                    }
                    else
                    {
                        Log.Debug("No items in bot inventory. This shouldn't be possible.");
                        tradeReadyBots.Remove(mySteamID);
                        if (!TryAction(TradeAction.CancelTrade))
                        {
                            Log.Warn("[Receiving] TRADE CLOSED");
                            Bot.CloseTrade();
                            Bot.StopBot();
                        }
                    }
                }
            }
        }

        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem) 
        { 
            Log.Debug("Item has been added");
            totalAdded++;
        }

        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeMessage(string message) 
        {
            if (OtherSID == MainSID)
            {
                System.Threading.Thread.Sleep(100);
                Log.Debug("Message Received: " + message);
                if (message == "ready")
                    if (!TryAction(TradeAction.SetReady, null, true))
                        OnTradeClose();
            }
            else
            {
                System.Threading.Thread.Sleep(100);
                Log.Debug("Message Received: " + message);
                if (message == "ready")
                    if (!TryAction(TradeAction.SendMessage, null, false, "ready"))
                        OnTradeClose();
            }
        }

        public override void OnTradeReady(bool ready) 
        {
            Log.Debug("OnTradeReady");
            Thread.Sleep(100);
            if (OtherSID == MainSID)
            {
                TradeAccept();
            }
            else if (IsAdmin)
            {
                if (!TryAction(TradeAction.SetReady, null, true))
                    OnTradeClose();
            }
        }

        public void TradeAccept()
        {
            if (OtherSID == MainSID)
            {
                Thread.Sleep(100);
                Success = TryAction(TradeAction.AcceptTrade);
                if (Success)
                {
                    Log.Success("Trade was Successful!");
                    tradeReadyBots.Remove(mySteamID);
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
                        if (!TryAction(TradeAction.CancelTrade))
                        {
                            Log.Warn("[Receiving] TRADE CLOSED");
                            Bot.CloseTrade();
                            Bot.StopBot();
                        }
                    }
                }
            }
            else if (IsAdmin)
            {
                if (TryAction(TradeAction.AcceptTrade))
                {
                    Log.Success("Trade was Successful!");
                }
                else
                {
                    Log.Warn("Trade might have failed.");
                    // Going to wait a little while to give the other bot time to finish prep if necessary.
                    Thread.Sleep(1000);
                }
                OnTradeClose();
            }
        }

        public override void OnTradeAccept()
        {
            Log.Debug("OnTradeAccept");
            if (OtherSID == MainSID)
            {
                Log.Warn("[Receiving] TRADE CLOSED");
                Bot.CloseTrade();
                //Bot.StopBot();
            }
            else if (IsAdmin)
            {
                if (TryAction(TradeAction.AcceptTrade))
                {
                    Log.Success("Trade was Successful!");
                }
                else
                {
                    Log.Warn("Trade might have failed.");
                    // Going to wait a little while to give the other bot time to finish prep if necessary.
                    Thread.Sleep(1000);
                }
            }
            OnTradeClose();
        }

        /// <summary>
        /// Starts a new trade if a SteamID is available. If a trade is already open, it is closed and another is started.
        /// </summary>
        private void BeginNextTrade(SteamID tradeSID)
        {
            Thread.Sleep(100);
            Log.Info("Starting Trade with: " + tradeSID);
            if (!Bot.OpenTrade(tradeSID))
            {
                Log.Info("Bot already in trade, closing and starting another.");
                TryAction(TradeAction.CancelTrade);
                Log.Warn("[Receiving] TRADE CLOSED");
                Bot.CloseTrade();
                Bot.OpenTrade(tradeSID);
            }
        }

        /// <summary>
        /// Finishes minor tasks at the end of the normal trading sequence
        /// </summary>
        private void FinishTrades()
        {
            // Remove the Display name prefix
            Bot.SteamFriends.SetPersonaName(Bot.DisplayName);
            Log.Info("Starting craft sequence.");
            AutoCraftAll();
            if (MainSID != null)
            {
                Log.Info("Now moving items to main.");
                BeginNextTrade(MainSID);
            }
            else
            {
                Log.Info("Main account not found. All normal tasks complete.");
            }
        }
    }
}

