﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Chatter
{
    public class MultiChannel
    {
        public event EventHandler<string> MessageDisplayEventHandler;

        List<Channel> channelList;

        string globalDisplayName = "";
        string globalConnectionIP = "localhost";

        int activeChannelIndex = 0;

        public MultiChannel()
        {
            channelList = new List<Channel>();
            // TODO: Get and read channel settings info
            // TODO: Get and read global settings info
            // TODO: instantiate channels
            // TODO: Ask for channel info if no channel exists.
        }

        public void ShutDown()
        {
            QuitChannels();
            // TODO: Save channel settings
            // 
        }

        /// <summary>
        /// Class to call to send a message out. All parsing on the outgoing side goes here.
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(string message)
        {
            // Check to see if this is a command message
            if (message.StartsWith("/", StringComparison.Ordinal))
            {
                message = message.TrimStart('/');
                HandleOutgoingCommandText(message);
            }
            else
            {
                // Send The message
                channelList[0]?.SendMessage(message);
            }
        }

        /// <summary>
        /// Parse and handle commands on the outgoing side that would react back to the user.
        /// </summary>
        /// <param name="message"></param>
        private void HandleOutgoingCommandText(string message)
        {
            string command = message.Split(' ')[0];
            switch (command.ToLower())
            {
                // Help request
                case CommandList.HELP_S:
                case CommandList.HELP:
                    // MAke a string with info on all command options
                    string helptext = $"\nYou are currently connected to {channelList[activeChannelIndex].ChannelName} \n Command syntax and their function is listed below:\n\n";
                    helptext += $"/{CommandList.HELP}       or      /{CommandList.HELP_S}\n               Provides this help documentation\n";
                    helptext += $"/{CommandList.QUIT}       or      /{CommandList.QUIT_S}\n               Quit the application\n";
                    helptext += $"/{CommandList.USER_LIST}\n               Get a listing of users currently connected\n";
                    helptext += $"/{CommandList.PM} [username] [message]\n               Message ONLY the specified user.\n";
                    helptext += "               Does NOT inform if user not online\n";
                    helptext += $"/{CommandList.CHANGE_NAME} [username]\n               Changes your currently display name\n";
                    helptext += $"/{CommandList.CHANGE_MULTICAST} [IP address]\n               Changes to a different multicast group.\n";
                    helptext += $"/{CommandList.CHANGE_PORT} [Port number]\n               Changes to a different port on the current multicast IP address\n";
                    helptext += "\nThis software is provided under the GNU AGPL3.0 license.\n";
                    helptext += @"The source code can be found at https://github.com/trashbros/Chatter/";
                    helptext += "\n";
                    DisplayMessage(helptext);
                    break;
                // Quit command
                case CommandList.QUIT_S:
                case CommandList.QUIT:
                    QuitChannels(message);
                    break;
                // Change active channel command
                case CommandList.CHANGE_CHANNEL:
                    string channelname = message.Substring(CommandList.CHANGE_CHANNEL.Length + 1);
                    int newchan = channelList.FindIndex(channel => channel.ChannelName == channelname);
                    if (newchan > -1)
                    {
                        activeChannelIndex = newchan;
                    }
                    else
                    {
                        DisplayMessage($"No channel with name {channelname} could be found.\n");
                    }
                    break;
                // Add a channel
                case CommandList.ADD_CHANNEL:
                    AddNewChannel(message);
                    break;
                // Get a listing of connected channels
                case CommandList.CHANNEL_LIST:
                    PrintChannelList(message);
                    break;
                // Get info on a specific channel
                case CommandList.CHANNEL_INFO:
                    PrintChannelInfo(message);
                    break;
                // Not a valid command string
                default:
                    // Send The message
                    channelList[0]?.SendMessage("/" + message);
                    break;
            }
        }

        private void QuitChannels(string message)
        {
            if (message.Trim() == CommandList.QUIT || message.Trim() == CommandList.QUIT_S)
            {
                QuitChannels();
            }
            else
            {
                var cnames = message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < cnames.Length; i++)
                {
                    var chan = channelList.FindIndex(c => c.ChannelName == cnames[i]);
                    channelList[chan].ShutDown();
                    // TODO: Handle closing channel and switching to others if availabel
                }
            }
        }

        private void QuitChannels()
        {
            foreach (var channel in channelList)
            {
                channel.ShutDown();
            }
        }

        private void AddNewChannel(string message)
        {
            string channelinfo = message.Substring(CommandList.ADD_CHANNEL.Length + 1).Trim();
            AddNewChannel(new ChannelSettings(channelinfo), false);
        }

        private void AddNewChannel(ChannelSettings channelSettings, bool silent = true)
        {
            if(string.IsNullOrWhiteSpace(channelSettings.DisplayName))
            {
                channelSettings.DisplayName = globalDisplayName;
            }
            if(string.IsNullOrWhiteSpace(channelSettings.ChannelName))
            {
                // TODO: Handle missing channel name... Generate name? Or abort?
            }
            if(string.IsNullOrWhiteSpace(channelSettings.ConnectionIP))
            {
                channelSettings.ConnectionIP = globalConnectionIP;
            }

            // TODO: Add silent option to limit logging of channel connection stuff
            channelList.Add(new Channel(channelSettings));
            channelList[channelList.Count - 1].Init();
        }

        private void DeleteChannel()
        {
            // TODO: Entirely remove channel
        }

        private void PrintChannelList(string message)
        {
            bool allChannels = CommandList.CHANNEL_LIST.Length != message.Trim().Length;
            string channelnames = "\nChannels are:\n";
            foreach (var channel in channelList)
            {
                if (channel.IsConnected || allChannels)
                {
                    channelnames += $"{channel.ChannelName}\n";
                }
            }
            DisplayMessage(channelnames);
        }

        private void PrintChannelInfo(string message)
        {
            string channelName = message.Substring(CommandList.CHANNEL_INFO.Length + 1);
            try
            {
                var channelInfo = channelList.Find(channel => channel.ChannelName == channelName);
                DisplayMessage(channelInfo.ToString());
            }
            catch
            {
                DisplayMessage("Not a valid channel name.\n");
            }
        }

        /// <summary>
        /// Wrapper function for invoking the message display event handler
        /// </summary>
        /// <param name="message"></param>
        private void DisplayMessage(string message)
        {
            MessageDisplayEventHandler?.Invoke(this, message);
        }
    }
}
