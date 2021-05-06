﻿/*******************************************************************************
* Copyright (c) 2018 Elhay Rauper
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted (subject to the limitations in the disclaimer
* below) provided that the following conditions are met:
*
*     * Redistributions of source code must retain the above copyright notice,
*     this list of conditions and the following disclaimer.
*
*     * Redistributions in binary form must reproduce the above copyright
*     notice, this list of conditions and the following disclaimer in the
*     documentation and/or other materials provided with the distribution.
*
*     * Neither the name of the copyright holder nor the names of its
*     contributors may be used to endorse or promote products derived from this
*     software without specific prior written permission.
*
* NO EXPRESS OR IMPLIED LICENSES TO ANY PARTY'S PATENT RIGHTS ARE GRANTED BY
* THIS LICENSE. THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
* CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
* LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
* PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
* CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
* EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
* PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR
* BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER
* IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
* ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
* POSSIBILITY OF SUCH DAMAGE.
*******************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Ports;
using System.Management;
using System.Threading;
using NLog;

namespace Falcon.Com
{
    public class SerialCom : SerialPort
    {
        private static ILogger Logger = LogManager.GetCurrentClassLogger();
        private bool connected_ = false;
        List<Action<byte[]>> subsList_ = new List<Action<byte[]>>();

        public bool Connect(string port, int baudRate)
        {
            return Connect(port, baudRate, Parity.None, 8, StopBits.One);
        }

        public bool Connect(string port,
                            int baudRate,
                            Parity parity,
                            int dataBits,
                            StopBits stopBits)
        {
            BaudRate = baudRate;
            PortName = port;
            Parity = parity;
            DataBits = dataBits;
            StopBits = stopBits;


            try
            {
                DataReceived += new SerialDataReceivedEventHandler(OnDataReceived);
                Open();
                connected_ = true;
            }
            catch (Exception e)
            {
                Logger.Error(e,"Error to open serial port");
                string err = e.ToString();
                connected_ = false;
            }
            return connected_;
        }

        public bool Send(byte[] bytes)
        {
            if (connected_)
            {
                Write(bytes, 0, bytes.Length);
                return true;
            }
            return false;
        }

        public void CloseMe()
        {
            if (connected_)
            {
                connected_ = false;
                Close();
            }
        }

        public bool IsConnected()
        {
            return connected_;
        }

        public void Subscribe(Action<byte[]> func)
        {
            subsList_.Add(func);
        }

        public void Unsubscribe(Action<byte[]> func)
        {
            subsList_.Remove(func);
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (connected_)
            {
                int numBytes = BytesToRead;
                var buff = new byte[numBytes];
                int bytesRead = Read(buff, 0, numBytes);
                if (bytesRead != numBytes)
                    return;
                try
                {
                    foreach (var func in subsList_)
                    {
                        func(buff);
                    }
                }
                catch (InvalidOperationException exp) // connection closed
                {

                }
                
            }
        }


        /// <summary>
        /// Get connected COM port names
        /// </summary>
        /// <returns>array of connected COM ports</returns>
        public static string[] GetConnectedPorts()
        {
            return GetPortNames().Distinct().ToArray();
        }

        /// <summary>
        /// Get detailed collection of connected COM port objects
        /// </summary>
        /// <returns>collection of connected COM ports objects</returns>
        public static List<ManagementBaseObject> GetConnectedPortsDetailedList()
        {
            ManagementObjectCollection portsCollection;
            List<ManagementBaseObject> comPortsList;

            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
            {
                portsCollection = searcher.Get();
                comPortsList = new List<ManagementBaseObject>();

                foreach (var device in portsCollection)
                {
                    // verify port is COM port
                    string name = (String)device.GetPropertyValue("Name");
                    if (name != null && name.Contains("(COM"))
                    {
                        comPortsList.Add(device);
                    }
                }
            }

            return comPortsList;
        }

        /// <summary>
        /// Get port simplefied name. i.e. 'COM3'
        /// </summary>
        /// <param name="detailedPort">string of port details</param>
        /// <returns>port simplefied name</returns>
        public static string DetailedToSimplefiedPortName(string detailedPort)
        {
            int startIndx = detailedPort.IndexOf('(');
            int endIndx = detailedPort.IndexOf(')');
            int portNameLength = endIndx - startIndx - 1;
            string simplefiedPortName = detailedPort.Substring(startIndx + 1, portNameLength);

            if (startIndx == -1 ||
               endIndx == -1 ||
               startIndx >= endIndx ||
               portNameLength < 4 ||
               !simplefiedPortName.Contains("COM"))
            {
                return null;
            }

            return simplefiedPortName;
        }

        /// <summary>
        /// Get detailed list of connected COM ports strings
        /// </summary>
        /// <returns>List of connected COM ports strings</returns>
        public static List<string> GetConnectedPortsDetailedStrings()
        {
            List<string> portsStrings = new List<string>();
            List<ManagementBaseObject> comPorts = GetConnectedPortsDetailedList();

            foreach (var comDevice in comPorts)
            {
                string detailedPort = (String)comDevice.GetPropertyValue("Name");

                portsStrings.Add(detailedPort);
            }

            return portsStrings;
        }

        public static Parity StringToParity(string parity)
        {
            switch(parity)
            {
                case "None":
                    return Parity.None;
                case "Odd":
                    return Parity.Odd;
                case "Even":
                    return Parity.Even;
                case "Mark":
                    return Parity.Mark;
                case "Space":
                    return Parity.Space;
                default:
                    return Parity.None;
            }
        }

        public static StopBits StringToStopBits(string stopBits)
        {
            switch(stopBits)
            {
                case "0":
                    return StopBits.None;
                case "1":
                    return StopBits.One;
                case "1.5":
                    return StopBits.OnePointFive;
                case "2":
                    return StopBits.Two;
                default:
                    return StopBits.None;
            }
        }

    }
}

