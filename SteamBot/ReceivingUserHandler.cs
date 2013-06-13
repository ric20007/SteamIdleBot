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
        bool Success;
        int totalAdded = 0;
        enum TradeAction { CancelTrade, AddItem, RemoveItem, SetReady, AcceptTrade, SendMessage };

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

        #region Trading
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

        #region Old Code
        // Old method below, kept just in case.

        ///// <summary>
        ///// Gets all friends who are not 'offline' and are not 'in game' and then calls FilterEmptyInventories().
        ///// </summary>
        ///// <returns>A list of trade ready SteamIDs.</returns>
        //private void GetTradeReadyFriendsList(SteamID master)
        //{
        //    Log.Debug("Getting Trade-Ready Friends");
        //    Log.Debug("Friend count: " + Bot.SteamFriends.GetFriendCount());
        //    int i = 0;
        //    while (i < Bot.SteamFriends.GetFriendCount())
        //    {
        //        tmpList.Add(Bot.SteamFriends.GetFriendByIndex(i));
        //        i++;
        //    }
        //    foreach (SteamID sid in tmpList)
        //    {
        //        if (sid != master && Bot.SteamFriends.GetFriendPersonaState(sid) == EPersonaState.Online && Bot.SteamFriends.GetFriendGamePlayed(sid) == 0)
        //        {
        //            availableFriends.Add(sid);
        //        }
        //    }
        //    Log.Info("Online Friends: " + availableFriends.Count);
        //    FilterEmptyInventories();
        //    ListEstablished = true;
        //}
        ///// <summary>
        ///// Removes SteamIDs with no tradable item from availableFriends
        ///// </summary>
        //private void FilterEmptyInventories()
        //{
        //    Log.Debug("Removing friends without items to give");
        //    tmpList = new List<SteamID>(availableFriends);
        //    foreach (SteamID sid in tmpList)
        //    {
        //        Bot.GetOtherInventory(sid);
        //        if (!HasTradableNonCrate(Bot.OtherInventory))
        //        {
        //            Log.Debug(OtherSID + " did not have a trade-worthy item.");
        //            availableFriends.Remove(sid);
        //            Log.Debug("Friend Count:" + availableFriends.Count);
        //        }
        //    }
        //    Log.Info("Number of friends to trade with: " + availableFriends.Count);
        //}
        #endregion

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

        /// <summary>
        /// Attempts the specified trade action, retrying 5 times if neccessary.
        /// Attempts to cancel the trade if unsuccesful.
        /// </summary>
        /// <param name="action">The desired in-trade action.</param>
        /// <param name="ready">Required only for SetReady</param>
        /// <param name="message">Required only for SendMessage</param>
        /// <param name="item">Required only for Add/Remove Item</param>
        /// <returns>True if successful.</returns>
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
                    switch(action)
                    {
                        case TradeAction.CancelTrade: 
                            Success = Trade.CancelTrade();        break;
                        case TradeAction.AddItem:     
                            Success = Trade.AddItem(item.Id);     break;
                        case TradeAction.RemoveItem:  
                            Success = Trade.RemoveItem(item.Id);  break;
                        case TradeAction.SetReady:    
                            Success = Trade.SetReady(ready);      break;
                        case TradeAction.AcceptTrade: 
                            Success = Trade.AcceptTrade();        break;
                        case TradeAction.SendMessage: 
                            Success = Trade.SendMessage(message); break;
                        default: Log.Error("Invalid trade action: " + action); break;
                    }
                }
                catch (TradeException te)
                {
                    Log.Warn(action + " failed.");
                    Log.Debug(string.Format("Loop #{0}\nException:{1}", x, te));
                }
                catch (Exception e)
                {
                    Log.Warn(action + " failed.");
                    Log.Debug(string.Format("Loop #{0}\nException:{1}", x, e));
                }
            }
            if (!Success)
            {
                Log.Error("Could not " + action);
                if (action != TradeAction.CancelTrade)
                {
                    TryAction(TradeAction.CancelTrade);
                }
                else
                {
                    Thread.Sleep(500);
                }
            }
            return Success;
        }

        #region Old Trade Functions
        ///// <summary>
        ///// Sets Ready, retrying 10 times if neccessary
        ///// </summary>
        ///// <returns>True if successful.</returns>
        //private bool SetReady(bool ready)
        //{
        //    Log.Debug("Setting ready");
        //    int x = 0;
        //    Success = false;
        //    while (Success == false && x < 10)
        //    {
        //        try
        //        {
        //            Log.Debug("Loop #" + x);
        //            x++;
        //            Success = Trade.SetReady(ready);
        //        }
        //        catch (TradeException te)
        //        {
        //            Log.Debug("Loop #" + x);
        //            Log.Warn("Setting ready failed.");
        //            Log.Debug("" + te);
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Warn("Setting ready failed");
        //            var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
        //            Log.Debug(s);
        //        }
        //    }
        //    if (!Success)
        //    {
        //        Log.Error("Could not set ready");
        //    }
        //    return Success;
        //}
        ///// <summary>
        ///// Sends a trade message, retrying 10 times if neccessary
        ///// </summary>
        ///// <returns>True if successful.</returns>
        //private bool SendMessage(string message)
        //{
        //    Log.Debug("Sending message:" + message);
        //    int x = 0;
        //    Success = false;
        //    while (Success == false && x < 10)
        //    {
        //        try
        //        {
        //            Log.Debug("Loop #" + x);
        //            x++;
        //            Success = Trade.SendMessage(message);
        //        }
        //        catch (TradeException te)
        //        {
        //            Log.Debug("Loop #" + x);
        //            Log.Warn("Sending message failed.");
        //            Log.Debug("" + te);
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Warn("Sending message failed");
        //            var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
        //            Log.Debug(s);
        //        }
        //    }
        //    if (!Success)
        //    {
        //        Log.Error("Could not send message");
        //    }
        //    return Success;
        //}
        ///// <summary>
        ///// Accepts a trade, retrying 10 times if neccessary
        ///// </summary>
        ///// <returns>True if successful.</returns>
        //private bool AcceptTrade()
        //{
        //    Log.Debug("Accepting Trade");
        //    int x = 0;
        //    Success = false;
        //    while (Success == false && x < 10)
        //    {
        //        try
        //        {
        //            Log.Debug("Loop #" + x);
        //            x++;
        //            Success = Trade.AcceptTrade();
        //        }
        //        catch (TradeException te)
        //        {
        //            Log.Debug("Loop #" + x);
        //            Log.Warn("Trade Accept failed.");
        //            Log.Debug("" + te);
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Warn("Trade Accept failed");
        //            var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
        //            Log.Debug(s);
        //        }
        //    }
        //    if (!Success)
        //    {
        //        Log.Error("Could not accept trade");
        //    }
        //    return Success;
        //}
        ///// <summary>
        ///// Adds an item, retrying 10 times if neccessary
        ///// </summary>
        ///// <returns>True if successful.</returns>
        //private bool AddItem(Inventory.Item item)
        //{
        //    int x = 0;
        //    Success = false;
        //    Log.Debug("Adding item");
        //    while (Success == false && x < 10)
        //    {
        //        try
        //        {
        //            Log.Debug("Loop #" + x);
        //            x++;
        //            Success = Trade.AddItem(item.Id);
        //        }
        //        catch (TradeException te)
        //        {
        //            Log.Debug("Loop #" + x);
        //            Log.Warn("Add Item failed.");
        //            Log.Debug("" + te);
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Warn("Add Item failed.");
        //            var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
        //            Log.Debug(s);
        //        }
        //    }
        //    if (!Success)
        //    {
        //        Log.Error("Could not add item");
        //    }
        //    return Success;
        //}
        ///// <summary>
        ///// Removes an item, retrying 10 times if neccessary
        ///// </summary>
        ///// <returns>True if successful.</returns>
        //private bool RemoveItem(Inventory.Item item)
        //{
        //    int x = 0;
        //    Success = false;
        //    Log.Debug("Removing item");
        //    while (Success == false && x < 10)
        //    {
        //        try
        //        {
        //            Log.Debug("Loop #" + x);
        //            x++;
        //            Success = Trade.RemoveItem(item.Id);
        //        }
        //        catch (TradeException te)
        //        {
        //            Log.Debug("Loop #" + x);
        //            Log.Warn("Remove Item failed.");
        //            Log.Debug("" + te);
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Warn("Remove Item failed");
        //            var s = string.Format("Loop #{0}{1}Exception:{2}", x, Environment.NewLine, e);
        //            Log.Debug(s);
        //        }
        //    }
        //    if (!Success)
        //    {
        //        Log.Error("Could not remove item");
        //    }
        //    return Success;
        //}
        #endregion
        #endregion

        #region Crafting
        /// <summary>
        /// Crafts Items
        /// </summary>
        private void Craft(ulong [] craftItems)
        {
            Log.Info("Crafting " + craftItems.Length + " items.");
            TF2GC.Crafting.CraftItems(Bot, craftItems);
            // Give time for callbacks to update, otherwise backpack may not be up-to-date
            // after a large amount of crafting.
            Thread.Sleep(100);
        }

        /// <summary>
        /// Crafts all weapons to ref (doesn't check for item quality ie stranges, or anything for that matter)
        /// </summary>
        private void AutoCraftAll()
        {
            Log.Info("Setting Game State to Playing TF2.");
            Bot.SetGamePlaying(440);
            SmeltAllWeapons();
            CombineAllMetal();
            Log.Info("Resetting Game State");
            Bot.SetGamePlaying(0);
        }
        /// <summary>
        /// Crafts all weapons into metal. (doesn't check for item quality ie stranges, or anything for that matter)
        /// </summary>
        private void SmeltAllWeapons()
        {
            // Will hold all craftable/tradable weapons
            List<Inventory.Item> myCleanWeapons = new List<Inventory.Item>();

            Log.Info("Smelting Weapons");
            Log.Debug("Getting Inventory");
            Bot.GetInventory();

            myCleanWeapons = GetCleanItemsOfMaterial("weapon");
            Log.Info("Number of weapons to craft: " + myCleanWeapons.Count);

            Log.Info("Sorting items by class.");
            List<List<Inventory.Item>> allWeapons = SortItemsByClass(myCleanWeapons);

            int scrapMade = ScrapWeapons(allWeapons);
            Log.Info("Scrap Made: " + scrapMade);
        }
        /// <summary>
        /// Crafts all scrap into reclaimed, then all reclaimed into refined.
        /// </summary>
        private void CombineAllMetal()
        {   
            // May use for inventory management
            //List<Inventory.Item> myScrap = new List<Inventory.Item>();
            //List<Inventory.Item> myReclaimed = new List<Inventory.Item>();
            //List<Inventory.Item> myRefined = new List<Inventory.Item>();

            Log.Info("Combining all metal");
            
            // Scrap, Reclaimed, and Refined are defindex 5000, 5001, 5002 respectively
            for (int defindex = 5000; defindex < 5002; defindex++)
            {
                List<Inventory.Item> metalToCraft = new List<Inventory.Item>();
                Log.Debug("Getting Inventory");
                Thread.Sleep(300); // Just another pause to be sure inventory has updated.
                Bot.GetInventory();
                foreach (Inventory.Item invItem in Bot.MyInventory.Items)
                {
                    if (invItem.Defindex == defindex)
                    {
                        metalToCraft.Add(invItem);
                    }
                }
                Log.Debug("Combining Metal. Defindex: " + defindex);
                while (metalToCraft.Count > 2)
                {
                    ulong[] craftIds = new ulong[3];
                    craftIds[0] = metalToCraft[0].Id;
                    craftIds[1] = metalToCraft[1].Id;
                    craftIds[2] = metalToCraft[2].Id;
                    Craft(craftIds);
                    for (int x = 0; x < 3; x++)
                    {
                        metalToCraft.RemoveAt(0);
                    }
                }
            }
        }

        /// <summary>
        /// Gets all craftable and tradable items of the specified crafting material
        /// </summary>
        /// <param name="CraftingMaterial">CraftingMaterial from schema</param>
        /// <returns>List of items in inventory that are craftable, tradable, and match
        /// the crafting material.</returns>
        private List<Inventory.Item> GetCleanItemsOfMaterial(String CraftingMaterial)
        {
            Log.Debug("Getting list of craftable weapons from schema");
            var CraftableItemList = Trade.CurrentSchema.GetItemsByCraftingMaterial(CraftingMaterial);
            List<Inventory.Item> myItems = new List<Inventory.Item>();

            foreach (Inventory.Item invItem in Bot.MyInventory.Items)
            {
                if (invItem.IsNotTradeable || invItem.IsNotCraftable)
                {
                    continue;
                }
                foreach (Schema.Item scItem in CraftableItemList)
                {
                    if (invItem.Defindex == scItem.Defindex)
                    {
                        myItems.Add(invItem);
                        break;
                    }
                }
            }
            return myItems;
        }

        /// <summary>
        /// Sorts items based on which of the 9 classes can use them
        /// </summary>
        /// <param name="items">List of items to sort</param>
        /// <returns>Lists of Lists of all the weapons in appropriate class lists, the 10th
        /// list is for multi-class weapons.</returns>
        private List<List<Inventory.Item>> SortItemsByClass(List<Inventory.Item> items)
        {
            // List for each class' weps
            List<Inventory.Item> scoutWeps = new List<Inventory.Item>();
            List<Inventory.Item> soldierWeps = new List<Inventory.Item>();
            List<Inventory.Item> pyroWeps = new List<Inventory.Item>();
            List<Inventory.Item> demoWeps = new List<Inventory.Item>();
            List<Inventory.Item> heavyWeps = new List<Inventory.Item>();
            List<Inventory.Item> engyWeps = new List<Inventory.Item>();
            List<Inventory.Item> medicWeps = new List<Inventory.Item>();
            List<Inventory.Item> sniperWeps = new List<Inventory.Item>();
            List<Inventory.Item> spyWeps = new List<Inventory.Item>();
            List<Inventory.Item> multiWeps = new List<Inventory.Item>();

            // List of the above lists
            List<List<Inventory.Item>> allWeps = new List<List<Inventory.Item>>(10);

            allWeps.Add(scoutWeps);
            allWeps.Add(soldierWeps);
            allWeps.Add(pyroWeps);
            allWeps.Add(demoWeps);
            allWeps.Add(heavyWeps);
            allWeps.Add(engyWeps);
            allWeps.Add(medicWeps);
            allWeps.Add(sniperWeps);
            allWeps.Add(spyWeps);

            foreach (Inventory.Item item in items)
            {
                var classes = Trade.CurrentSchema.GetItem(item.Defindex).UsableByClasses;
                if (classes.Length > 1)
                {
                    multiWeps.Add(item);
                }
                else
                    switch (classes[0])
                    {
                        case ("Scout"): scoutWeps.Add(item); break;
                        case ("Soldier"): soldierWeps.Add(item); break;
                        case ("Pyro"): pyroWeps.Add(item); break;
                        case ("Demoman"): demoWeps.Add(item); break;
                        case ("Heavy"): heavyWeps.Add(item); break;
                        case ("Engineer"): engyWeps.Add(item); break;
                        case ("Medic"): medicWeps.Add(item); break;
                        case ("Sniper"): sniperWeps.Add(item); break;
                        case ("Spy"): spyWeps.Add(item); break;
                        default: Log.Debug("what happened? 10th class? idk"); break;
                    }
            }
            Log.Debug("Number of...");
            Log.Debug("Scout items: " + scoutWeps.Count);
            Log.Debug("Soldier items: " + soldierWeps.Count);
            Log.Debug("Pyro items: " + pyroWeps.Count);
            Log.Debug("Demoman items: " + demoWeps.Count);
            Log.Debug("Heavy items: " + heavyWeps.Count);
            Log.Debug("Engineer items: " + engyWeps.Count);
            Log.Debug("Medic items: " + medicWeps.Count);
            Log.Debug("Sniper items: " + sniperWeps.Count);
            Log.Debug("Spy items: " + spyWeps.Count);
            Log.Debug("Multi-class items (pain train + half-zatoichi): " + multiWeps.Count);

            // Add multiWeps back to return allWeps
            allWeps.Add(multiWeps);

            return allWeps;
        }

        /// <summary>
        /// Scraps a List of lists of weapons sorted into of their classes
        /// </summary>
        /// <param name="allWeapons">List of Lists containing all weapons sorted by class</param>
        /// <returns>Number of scrap made.</returns>
        private int ScrapWeapons(List<List<Inventory.Item>> allWeapons)
        {
            List<Inventory.Item> multiWeps = allWeapons[9];

            // Seperate the multi-class weapons again
            allWeapons.RemoveAt(9);

            ulong[] craftIds;
            int scrapMade = 0;
            Log.Info("Beginning smelt sequence.");

            // Crafting off pairs of weapons in class lists
            foreach (List<Inventory.Item> list in allWeapons)
            {
                while (list.Count > 1)
                {
                    craftIds = new ulong[2];
                    craftIds[0] = list[0].Id;
                    list.RemoveAt(0);
                    craftIds[1] = list[0].Id;
                    list.RemoveAt(0);
                    Craft(craftIds);
                    scrapMade++;
                }
            }

            //(Still needs to be optimised) Crafting the remaining multi-class weapons
            Log.Info("Scrapping multi-class weps");
            foreach (List<Inventory.Item> list in allWeapons)
            {
                craftIds = new ulong[2];
                if (list.Count > 0)
                {
                    foreach (Inventory.Item item in multiWeps)
                    {
                        List <string> classes = new List<string>(Trade.CurrentSchema.GetItem(item.Defindex).UsableByClasses);
                        string [] itemClass = Trade.CurrentSchema.GetItem(list[0].Defindex).UsableByClasses;
                        if (classes.Contains(itemClass[0]))
                        {
                            craftIds[0] = item.Id;
                            // I can remove item from this foreach because I'm going to break anyway
                            multiWeps.Remove(item);
                            craftIds[1] = list[0].Id;
                            list.RemoveAt(0);
                            Craft(craftIds);
                            scrapMade++;
                            break;
                        }
                    }
                }
            }
            // Clean up any leftover
            while (multiWeps.Count > 1)
            {
                craftIds = new ulong[2];
                craftIds[0] = multiWeps[0].Id;
                multiWeps.RemoveAt(0);
                craftIds[1] = multiWeps[0].Id;
                multiWeps.RemoveAt(0);
                Craft(craftIds);
                scrapMade++;
            }
            return scrapMade;
        }
        #endregion
    }
}

