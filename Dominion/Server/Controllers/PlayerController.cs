﻿// Dominion - Copyright (C) Timothy Ings
// PlayerController.cs
// This file defines classes that defines the player controller

using ArwicEngine.Core;
using ArwicEngine.Net;
using Dominion.Common.Data;
using Dominion.Common.Entities;
using Dominion.Common.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dominion.Server.Controllers
{
    public class PlayerEventArgs : EventArgs
    {
        public Player Player { get; }

        public PlayerEventArgs(Player player)
        {
            Player = player;
        }
    }

    public class PlayerController : Controller
    {
        private List<Player> players;

        /// <summary>
        /// Occurs when a player is added to the player controller
        /// </summary>
        public event EventHandler<PlayerEventArgs> PlayerAdded;

        /// <summary>
        /// Occurs when a player is removed from the player controller
        /// </summary>
        public event EventHandler<PlayerEventArgs> PlayerRemoved;

        /// <summary>
        /// Occurs when a player managed by the player controller is updated
        /// </summary>
        public event EventHandler<PlayerEventArgs> PlayerUpdated;

        /// <summary>
        /// Occurs when the turn state of a player managed by the player controller has changed
        /// </summary>
        public event EventHandler<PlayerEventArgs> PlayerTurnStateChanged;

        protected virtual void OnPlayerAdded(PlayerEventArgs e)
        {
            if (PlayerAdded != null)
                PlayerAdded(this, e);
        }

        protected virtual void OnPlayerRemoved(PlayerEventArgs e)
        {
            if (PlayerRemoved != null)
                PlayerRemoved(this, e);
        }

        protected virtual void OnPlayerUpdated(PlayerEventArgs e)
        {
            if (PlayerUpdated != null)
                PlayerUpdated(this, e);
        }

        protected virtual void OnTurnStateChanged(PlayerEventArgs e)
        {
            if (PlayerTurnStateChanged != null)
                PlayerTurnStateChanged(this, e);
        }

        public PlayerController(ControllerManager manager)
            : base(manager)
        {
            players = new List<Player>();
            Controllers.City.CitySettled += City_CitySettled;
            Controllers.City.CityUpdated += City_CityUpdated;
            Controllers.City.CityCaptured += City_CityCaptured;
        }

        private void City_CitySettled(object sender, CityEventArgs e)
        {
            Player player = GetPlayer(e.City.PlayerID);
            CalculateIncome(player);
            OnPlayerUpdated(new PlayerEventArgs(player));
        }
        
        private void City_CityCaptured(object sender, CityEventArgs e)
        {
            Player player = GetPlayer(e.City.PlayerID);
            CalculateIncome(player);
            OnPlayerUpdated(new PlayerEventArgs(player));
        }

        private void City_CityUpdated(object sender, CityEventArgs e)
        {
            Player player = GetPlayer(e.City.PlayerID);
            CalculateIncome(player);
            OnPlayerUpdated(new PlayerEventArgs(player));
        }

        /// <summary>
        /// Prepares the players managed by the player manager for the next turn
        /// </summary>
        public override void ProcessTurn()
        {
            foreach (Player player in players)
            {
                CalculateIncome(player);
                player.EndedTurn = false;
                player.Gold += player.IncomeGold;
                player.Culture += player.IncomeCulture;
                ProcessPlayerResearch(player);
                OnPlayerUpdated(new PlayerEventArgs(player));
            }
        }

        /// <summary>
        /// Issues a command to a player 
        /// </summary>
        /// <param name="cmd"></param>
        public void CommandPlayer(PlayerCommand cmd)
        {
            Player player = GetPlayer(cmd.PlayerID);
            if (player == null)
                return;

            try
            {
                switch (cmd.CommandID)
                {
                    case PlayerCommandID.SelectTech:
                        player.SelectedTechNodeID = (string)cmd.Arguments[0];
                        break;
                    case PlayerCommandID.UnlockPolicy:
                        UnlockSocialPolicy(player, (string)cmd.Arguments[0]);
                        break;
                }
            }
            catch (Exception)
            {
                //Engine.Console.WriteLine($"CityCommand.{cmd.CommandID.ToString()} Error: malformed data", MsgType.ServerWarning);
            }

            OnPlayerUpdated(new PlayerEventArgs(player));
        }

        private void UnlockSocialPolicy(Player player, string policyID)
        {
            // get the policy
            SocialPolicy policy = player.SocialPolicyInstance.GetSocialPolicy(policyID);
            
            // check if we have already unlocked it
            if (policy.Unlocked)
                return;
            
            // check for prereqs
            foreach (string prereqID in policy.Prerequisites)
            {
                SocialPolicy prereq = Controllers.Data.SocialPolicy.GetSocialPolicy(prereqID);
                if (prereq != null && !prereq.Unlocked)
                    return; // prereqs are still locked
            }

            // try and use free social policy points
            if (player.FreeSocialPoliciesAvailable > 0)
            {
                policy.Unlocked = true;
                player.FreeSocialPoliciesAvailable--;
                OnPlayerUpdated(new PlayerEventArgs(player));
                return;
            }

            // unlock the policy with culture
            const int baseCost = 25;
            int playerCityCount = Controllers.City.GetPlayerCities(player.InstanceID).Count;
            int playerPolicyCount = player.SocialPolicyInstance.GetAllSocialPolicies().Count(sp => sp.Unlocked);
            double cityCountMultiplier = Math.Max(1 + 0.1 * (playerCityCount - 1), 1);
            double exactCost = (baseCost + Math.Pow(3 * playerPolicyCount, 2.01)) * cityCountMultiplier * player.PolicyCostModifier;
            int finalCost = (int)Math.Floor(exactCost / 5) * 5;

            if (player.Culture >= finalCost)
            {
                player.Culture -= finalCost;
                policy.Unlocked = true;
                OnPlayerUpdated(new PlayerEventArgs(player));
            }
        }

        // calculates the income of the given player
        private void CalculateIncome(Player player)
        {
            player.IncomeCulture = 0;
            player.IncomeGold = 0;
            player.Happiness = 0;
            player.IncomeScience = 0;

            List<City> playerCities = Controllers.City.GetPlayerCities(player.InstanceID);
            foreach (City city in playerCities)
            {
                player.IncomeCulture += city.IncomeCulture;
                player.IncomeGold += city.IncomeGold;
                //player.Happiness += city.IncomeHappiness;
                player.IncomeScience += city.IncomeScience;
            }
        }

        // processes the given players research
        private void ProcessPlayerResearch(Player player)
        {
            if (player.SelectedTechNodeID == "TECH_NULL") // don't process a research node if it doesn't exist
                return;

            // get the selected node
            Technology currentTech = player.TechTreeInstance.GetTech(player.SelectedTechNodeID);
            // add the player's overflow and income to the nodes progress
            currentTech.Progress += player.ScienceOverflow;
            currentTech.Progress += player.IncomeScience;
            // check if the node has been completed
            if (currentTech.Progress >= currentTech.ResearchCost)
            {
                currentTech.Unlocked = true; // mark the node as unlocked
                player.SelectedTechNodeID = "TECH_NULL"; // TODO make this work with multiple tech nodes selected
                player.ScienceOverflow += currentTech.Progress - currentTech.ResearchCost; // add the left over science to the player's overflow
            }
        }

        /// <summary>
        /// Creates a new player that is managed by the player controller
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="userName"></param>
        public void AddPlayer(Connection connection, string userName)
        {
            Player player = new Player(connection, players.Count, "NULL", userName, Controllers.Data.Tech.GetNewTree(), Controllers.Data.SocialPolicy.GetNewInstance());
            player.EmpireID =  Controllers.Data.Empire.GetAllEmpires().ElementAt(RandomHelper.Next(0, Controllers.Data.Empire.EmpireCount)).ID;
            player.TechTreeInstance.GetTech("TECH_AGRICULTURE").Unlocked = true;
            players.Add(player);
            OnPlayerAdded(new PlayerEventArgs(player));
        }

        /// <summary>
        /// Removes a player from the player controller
        /// </summary>
        /// <param name="playerID"></param>
        public void RemovePlayer(int playerID)
        {
            Player player = GetPlayer(playerID);
            players.Remove(player);
            OnPlayerRemoved(new PlayerEventArgs(player));
        }

        /// <summary>
        /// Gets all the players managed by the player controller
        /// </summary>
        /// <returns></returns>
        public List<Player> GetAllPlayers()
        {
            return players;
        }

        /// <summary>
        /// Gets the player with the given connection
        /// </summary>
        /// <param name="conn"></param>
        /// <returns></returns>
        public Player GetPlayer(Connection conn)
        {
            return players.Find(p => p.Connection == conn);
        }

        /// <summary>
        /// Gets the player with the given id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Player GetPlayer(int id)
        {
            return players.Find(p => p.InstanceID == id);
        }

        /// <summary>
        /// Updates the turn state of the given player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="turnEnded"></param>
        public void UpdateTurnState(Player player, bool turnEnded)
        {
            player.EndedTurn = turnEnded;
            OnTurnStateChanged(new PlayerEventArgs(player));
        }
    }
}
