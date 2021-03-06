﻿/*
Startup console application. Handle format and console read/write.

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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ChatrConsole
{
    internal class Program
    {
        #region Private Fields

        private static readonly string Prompt = SR.Prompt;
        private static readonly List<string> s_messageHistory = new List<string>();
        private static readonly object s_outputLock = new object();
        private static Chatr.Client s_chatrClient;
        private static string s_currentInput = string.Empty;
        private static int s_historyIndex = -1;
        private static string s_nextInput = string.Empty;

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Clear any user message input from the display
        /// </summary>
        private static void ClearInputLines()
        {
            Console.SetCursorPosition(0, Console.CursorTop - 1 * ((s_currentInput.Length + 1) / Console.WindowWidth));
            Console.Write(new string(' ', s_currentInput.Length + 2));
            Console.SetCursorPosition(0, Console.CursorTop - 1 * ((s_currentInput.Length + 1) / Console.WindowWidth));
        }

        /// <summary>
        /// Display the message input from the user
        /// </summary>
        private static void DisplayInput()
        {
            // synchronized console output
            lock (s_outputLock)
            {
                // Clear the previous message input
                ClearInputLines();

                // Display the new message input
                Console.Write(Prompt + s_nextInput);
            }
        }

        /// <summary>
        /// Display a recevied message
        /// </summary>
        /// <param name="message">Message to display</param>
        private static void DisplayMessage(string message, string textColor)
        {
            // synchronized console output
            lock (s_outputLock)
            {
                // Clear the previous message input
                ClearInputLines();

                // Set the text color
                if (Enum.TryParse<ConsoleColor>(textColor, true, out ConsoleColor consoleColor))
                {
                    Console.ForegroundColor = consoleColor;
                }

                // Display the messaage
                Console.Write(message);

                // Reset the text color
                Console.ResetColor();

                // Display any ongoing message input
                Console.Write(Environment.NewLine + Prompt + s_nextInput);
            }
        }

        /// <summary>
        /// Display the assembly version information
        /// </summary>
        private static void DisplayVersion()
        {
            // Get the version from currently executing assembly
            string assVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // Display the version to the console
            Console.WriteLine($"You are running version {assVersion} of Chatr!");
        }

        /// <summary>
        /// Get the settings path based on a suggested settings path Use the default path if invalid
        /// </summary>
        /// <param name="settingsPath">Settings path to use</param>
        /// <returns></returns>
        private static string GetSettingsPath(string settingsPath)
        {
            // Default path in case supplied path is invalid
            string path = System.IO.Path.GetFullPath(".chatrconfig");

            // Check to see if the path is valid
            if (IsPathValidRootedLocal(settingsPath))
            {
                // Use the supplied settings path
                path = System.IO.Path.GetFullPath(settingsPath);
            }

            if(!IsPathValidRootedLocal(path))
            {
                path = (Environment.OSVersion.Platform == PlatformID.Unix ||
                   Environment.OSVersion.Platform == PlatformID.MacOSX) ? 
                    Environment.GetEnvironmentVariable("HOME") : 
                    Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
                path = System.IO.Path.Combine(path, ".chatrconfig");
            }

            return path;
        }

        /// <summary>
        /// Validate that path is a rooted, local path
        /// </summary>
        /// <param name="pathString">Path to validate</param>
        /// <returns>true if path is rooted, local path, false otherwise</returns>
        private static bool IsPathValidRootedLocal(String pathString)
        {
            bool isValidUri = Uri.TryCreate(pathString, UriKind.Absolute, out Uri pathUri);
            return isValidUri && pathUri != null && pathUri.IsLoopback && File.Exists(pathString);
        }

        /// <summary>
        /// Check to see if a message is the quit message
        /// </summary>
        /// <param name="message">Message to check</param>
        /// <returns></returns>
        private static bool IsQuitMessage(string message)
        {
            string text = message.ToLower(System.Globalization.CultureInfo.CurrentCulture).TrimStart('/');
            return (text == Chatr.CommandList.QUIT || text == Chatr.CommandList.QUIT_S);
        }

        /// <summary>
        /// Entry point of the program.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        private static void Main(string[] args)
        {
            System.AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            // Display the assembly version information
            DisplayVersion();

            // Try and read the settings path from the command line arguments
            string settingsPath = (args == null || args.Length == 0) ? null : args[0];

            // Check if it actually has a config
            bool noSettings = !IsPathValidRootedLocal(settingsPath) && !IsPathValidRootedLocal(System.IO.Path.GetFullPath(".chatrconfig"));

            // Startup the Chatr client
            StartupChatr(GetSettingsPath(settingsPath), !noSettings);

            // Read and process commands entered by the user until the quit message is entered
            ReadAndProcessMessagesUntilQuit();

            // Shutdown the Chatr client
            ShutdownChatr();
        }

        /// <summary>
        /// Read messages entered by the user and send them to the client util the user enters the
        /// quit message
        /// </summary>
        private static void ReadAndProcessMessagesUntilQuit()
        {
            // Display intial prompt
            Console.Write(Environment.NewLine + Prompt);

            // Read the first message
            string message = ReadMessage();

            // Loop while it isn't the quit message
            while (!IsQuitMessage(message))
            {
                // Try and process it as console command first
                if (!TryProcessAsConsoleCommand(message))
                {
                    // Send the message to the client
                    s_chatrClient.SendMessage(message);
                }

                // Read the next message
                message = ReadMessage();
            };
        }

        /// <summary>
        /// Read a message from the console
        /// </summary>
        /// <returns>Message that was read</returns>
        private static string ReadMessage()
        {
            // Clear input strings
            s_currentInput = string.Empty;
            s_nextInput = string.Empty;

            // Reset the history index
            s_historyIndex = -1;

            // Read the first key from the console
            var key = Console.ReadKey(true);

            // Loop until the ENTER key is pressed
            while (key.Key != ConsoleKey.Enter)
            {
                // Intialize the next input to the current input
                s_nextInput = s_currentInput;

                // Check for a backspace
                if (key.Key == ConsoleKey.Backspace && s_currentInput.Length > 0)
                {
                    // Remote the last character from the input
                    s_nextInput = s_currentInput[0..^1];
                }
                // Check for the ESC key
                else if (key.Key == ConsoleKey.Escape)
                {
                    // Clear the input
                    s_nextInput = string.Empty;

                    // Reset the history index
                    s_historyIndex = -1;
                }
                // Check for UP arrow key
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    // If we can go forward in history
                    if (s_messageHistory.Count > 0 && s_historyIndex < s_messageHistory.Count - 1)
                    {
                        // Move forward in the history
                        s_historyIndex++;
                        s_nextInput = s_messageHistory[s_historyIndex];
                    }
                }
                // Check for DOWN arrow key
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    // If we can go back in history
                    if (s_messageHistory.Count > 0 && s_historyIndex > 0)
                    {
                        // Move back in history
                        s_historyIndex--;
                        s_nextInput = s_messageHistory[s_historyIndex];
                    }
                }
                // Check to see if this is not a control key
                else if (!char.IsControl(key.KeyChar))
                {
                    // Add the key character to the input
                    s_nextInput = s_currentInput + key.KeyChar;
                }

                // Display the input
                DisplayInput();

                // Current input now becomes the next input value
                s_currentInput = s_nextInput;

                // Read the next key
                key = Console.ReadKey(true);
            }

            // Trim any whitespace off the command
            s_currentInput = s_currentInput.Trim();

            // Check to see if we should add this input to the history
            if (!string.IsNullOrEmpty(s_currentInput) && (s_messageHistory.Count == 0 || !s_messageHistory[0].Equals(s_currentInput, StringComparison.Ordinal)))
            {
                // Insert this input to the top of the history
                s_messageHistory.Insert(0, s_currentInput);
            }

            // Get the message to return
            string message = s_currentInput;

            // Clear the input command from the console
            ClearInputLines();

            // Display input prompt
            Console.Write(Prompt);

            // Clear the input strings
            s_currentInput = string.Empty;
            s_nextInput = string.Empty;

            // Return the message entered in the console
            return message;
        }

        /// <summary>
        /// Shutdown the Chatr client
        /// </summary>
        private static void ShutdownChatr()
        {
            s_chatrClient?.ShutDown();
            s_chatrClient = null;
        }

        /// <summary>
        /// Start a new Chatr client using the settings from <paramref name="settingsPath"/>
        /// </summary>
        /// <param name="settingsPath">File path to chatr settings file</param>
        private static void StartupChatr(string settingsPath, bool hasSettings = true)
        {
            if (!hasSettings)
            {

                // Hello, new user!
                Console.WriteLine("No config file found!\n\nWelcome to Chatr!");

                bool goodUser = false;

                string username = "";

                while (!goodUser)
                {
                    // Ask for a username/ip address
                    Console.Write("What name do you want to show other users? ");

                    // Get user input for username
                    username = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(username) || username.StartsWith('/') || username == Chatr.CommandList.QUIT || username == Chatr.CommandList.QUIT_S)
                    {
                        Console.WriteLine("Bad username. Do you want to quit? (y/N)");

                        var yesno = Console.ReadLine().ToLower();
                        if (yesno == "y" || yesno == "yes")
                        {
                            Console.WriteLine("Okay, bye!");
                            Environment.Exit(0);
                        }
                    }
                    else
                    {
                        goodUser = true;
                    }
                }

                Console.WriteLine("Thanks!");
                Console.WriteLine();

                string ipaddr = null;

                var ipAddressList = Dns.GetHostAddresses(Dns.GetHostName())
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !ip.Equals(IPAddress.Loopback));

                while(string.IsNullOrEmpty(ipaddr))
                {
                    Console.WriteLine("Please select which network you want to communicate on:");

                    // List the networks available on this computer
                    int index = 0;
                    foreach (var ipAddress in ipAddressList)
                    {
                        Console.WriteLine($"   {++index}) {ipAddress}");
                    }

                    Console.WriteLine($"   {++index}) Manually enter IP");
                    Console.WriteLine($"   {++index}) Quit");

                    Console.WriteLine();
                    Console.Write($"Enter your choice [1-{index}] [Default: 1] : ");

                    // Get the user input
                    var choiceString = Console.ReadLine().Trim();

                    int choice;

                    if (string.IsNullOrEmpty(choiceString))
                    {
                        choice = 1;
                    }
                    else
                    {
                        if (!int.TryParse(choiceString, out choice))
                        {
                            choice = -1;
                        }
                    }

                    if (choice == ipAddressList.Count() + 1)
                    {
                        Console.Write("Please provide your desired IP address: ");

                        // Get the users input as an IP address
                        var ipString = Console.ReadLine();
                        if (!IPAddress.TryParse(ipString, out IPAddress addr))
                        {
                            Console.WriteLine("That's not a valid IP address!");
                            Console.WriteLine();
                            Console.WriteLine();
                        }
                        else
                        {
                            ipaddr = addr.ToString();
                        }
                    }
                    else if (choice == ipAddressList.Count() + 2)
                    {
                        Console.WriteLine("Okay, bye!");
                        Environment.Exit(0);
                    }
                    else if (choice > 0 && choice <= ipAddressList.Count())
                    {
                        ipaddr = ipAddressList.ElementAt(choice - 1).ToString();
                    }
                    else
                    {
                        // User provided invalid input
                        Console.WriteLine("That's not a valid choice!");
                        Console.WriteLine();
                        Console.WriteLine();
                    }
                }

                // Create a new Chatr client
                s_chatrClient = new Chatr.Client(username,ipaddr,settingsPath);
            }
            else
            {
                // Create a new Chatr client
                s_chatrClient = new Chatr.Client(settingsPath);
            }

            // Attach a message display handler
            s_chatrClient.MessageDisplayEventHandler += (sender, m) =>
            {
                DisplayMessage(m[0], m[1]);
            };
        }

        /// <summary>
        /// Try to process the message as a console command
        /// </summary>
        /// <param name="message">Message to process</param>
        /// <returns>true if the command was handled, otherwise false</returns>
        private static bool TryProcessAsConsoleCommand(string message)
        {
            // Check for empty message
            if (string.IsNullOrEmpty(message))
            {
                // Do nothing

                // command handled
                return true;
            }

            // Check for the clear command
            if (message.StartsWith("/clear", true, CultureInfo.CurrentCulture))
            {
                // Clear the console and display the prompt
                Console.Clear();
                Console.Write(Prompt);

                // command handled
                return true;
            }

            // command not handled
            return false;
        }

        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject.ToString());
            Console.WriteLine("Press Enter to continue");
            Console.ReadLine();
            ShutdownChatr();
            Environment.Exit(1);
        }

        #endregion Private Methods
    }
}
