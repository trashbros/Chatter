﻿/*
Storage class for channel setting information
Copyright (C) 2020  Trash Bros (BlinkTheThings, Reakain)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Text;

namespace Chatter
{
    public class ChannelSettings
    {
        public string ChannelName { get { return m_channelName; } set { m_channelName = value.Replace(' ', '_'); } }
        private string m_channelName;
        public string DisplayName { get { return m_displayName; } set { m_displayName = value.Replace(' ', '_'); } }
        private string m_displayName;
        public string ConnectionIP { get; set; }
        public string MulticastIP { get; set; }
        public string PortString
        {
            get
            {
                return m_port.ToString();
            }
            set
            {
                if (Helpers.IsValidPort(value, out int port))
                {
                    m_port = port;
                }
            }
        }
        public int Port
        {
            get
            {
                return m_port;
            }
            set
            {
                if (Helpers.IsValidPort(value))
                {
                    m_port = value;
                }
            }
        }
        private int m_port = 1314;
        public string Password { get; set; }

        public ChannelSettings()
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            m_port = 1314;
            MulticastIP = "239.255.10.11";
            ConnectionIP = "";
            ChannelName = "";
            Password = $"{MulticastIP}:{m_port}";
            DisplayName = "";
        }

        public ChannelSettings(string channelinfo)
        {
            SetDefaults();

            string[] channelsplit = channelinfo.Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < channelsplit.Length; i++)
            {
                if(channelsplit[i].StartsWith("-", StringComparison.Ordinal))
                {
                    var command = channelsplit[i].TrimStart('-');
                    switch(command)
                    {
                        // Channel name value
                        case "cn":
                        case "channel":
                            ChannelName = channelsplit[i + 1];
                            break;
                        // Display name value
                        case "dn":
                        case "display":
                            DisplayName = channelsplit[i + 1];
                            break;
                        // Connection IP value
                        case "lip":
                        case "localip":
                            ConnectionIP = channelsplit[i + 1];
                            break;
                        // Multicast IP value
                        case "mip":
                        case "multicastip":
                            MulticastIP = channelsplit[i + 1];
                            break;
                        // Port value
                        case "p":
                        case "port":
                            PortString = channelsplit[i + 1];
                            break;
                        // Encryption password value
                        case "pw":
                        case "password":
                            Password = channelsplit[i + 1];
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        
    }
}
