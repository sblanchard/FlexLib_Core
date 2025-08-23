﻿// ****************************************************************************
///*!	\file RapidM.cs
// *	\brief Represents a RapidM modem interface
// *
// *	\copyright	Copyright 2020 FlexRadio Systems.  All Rights Reserved.
// *				Unauthorized use, duplication or distribution of this software is
// *				strictly prohibited by law.
// *
// *	\date 2020-11-11
// *	\author Eric Wachsmann, KE5DTO
// */
// ****************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Flex.Smoothlake.FlexLib.Mvvm;


namespace Flex.Smoothlake.FlexLib
{
    public class RapidM : ObservableObject
    {
        private Radio _radio;

        public RapidM(Radio radio)
        {
            _radio = radio;
        }

        #region Properties

        private string _selectedPskWaveform;
        /// <summary>
        /// The RapidM PSK Waveform Mode selected in the drop down
        /// </summary>
        public string SelectedPskWaveform
        {
            get { return _selectedPskWaveform; }
            set
            {
                if (_selectedPskWaveform != value)
                {
                    _selectedPskWaveform = value;
                    //SendCommand("rapidm psk set wf=" + _waveform);
                    RaisePropertyChanged("SelectedPskWaveform");
                }
            }
        }

        private string _selectedMs110cWaveform;
        /// <summary>
        /// The RapidM MS110C Waveform Mode selected in the drop down
        /// </summary>
        public string SelectedMs110cWaveform
        {
            get { return _selectedMs110cWaveform; }
            set
            {
                if (_selectedMs110cWaveform != value)
                {
                    _selectedMs110cWaveform = value;
                    RaisePropertyChanged("SelectedMs110cWaveform");
                }
            }
        }

        private string _selectedRate;
        /// <summary>
        /// The RapidM baud rate selected in the drop down
        /// </summary>
        public string SelectedRate
        {
            get { return _selectedRate; }
            set
            {
                if (_selectedRate != value)
                {
                    _selectedRate = value;
                    //SendCommand("rapidm psk set rate=" + _rate);
                    RaisePropertyChanged("SelectedRate");
                }
            }
        }

        private string _selectedBandwidth;
        /// <summary>
        /// The RapidM bandwidth selected in the drop down
        /// </summary>
        public string SelectedBandwidth
        {
            get { return _selectedBandwidth; }
            set
            {
                if (_selectedBandwidth != value)
                {
                    _selectedBandwidth = value;
                    //SendCommand("rapidm psk set bw=" + _bandwidth);
                    RaisePropertyChanged("SelectedBandwidth");
                }
            }
        }

        private string _selectedInterleaver;
        /// <summary>
        /// The RapidM Interleaver selected in the drop down
        /// </summary>
        public string SelectedInterleaver
        {
            get { return _selectedInterleaver; }
            set
            {
                if (_selectedInterleaver != value)
                {
                    _selectedInterleaver = value;
                    //SendCommand("rapidm psk set il=" + _interleaver);
                    RaisePropertyChanged("SelectedInterleaver");
                }
            }
        }

        private bool _selectedPSKENC;
        /// <summary>
        /// The PSK ENC selected in the drop down
        /// </summary>
        public bool SelectedPSKENC
        {
            get { return _selectedPSKENC; }
            set
            {
                if (_selectedPSKENC != value)
                {
                    _selectedPSKENC = value;
                    RaisePropertyChanged("SelectedPSKENC");
                }
            }
        }

        private bool _selectedPSKISB;
        /// <summary>
        /// The PSK ISB selected in the drop down
        /// </summary>
        public bool SelectedPSKISB
        {
            get { return _selectedPSKISB; }
            set
            {
                if (_selectedPSKISB != value)
                {
                    _selectedPSKISB = value;
                    RaisePropertyChanged("SelectedPSKISB");
                }
            }
        }

        private string _selectedMS110CInterleaver;
        /// <summary>
        /// The RapidM Interleaver for MS110C in the drop down
        /// </summary>
        public string SelectedMS110CInterleaver
        {
            get { return _selectedMS110CInterleaver; }
            set
            {
                if (_selectedMS110CInterleaver != value)
                {
                    _selectedMS110CInterleaver = value;
                    RaisePropertyChanged("SelectedMS110CInterleaver");
                }
            }
        }

        private string _selectedMS110CConstraint;
        /// <summary>
        /// The RapidM Constraint for MS110C in the drop down
        /// </summary>
        public string SelectedMS110CConstraint
        {
            get { return _selectedMS110CConstraint; }
            set
            {
                if (_selectedMS110CConstraint != value)
                {
                    _selectedMS110CConstraint = value;
                    RaisePropertyChanged("SelectedMS110CConstraint");
                }
            }
        }

        private string _selectedMS110CTLC;
        /// <summary>
        /// The MS110C TLC value (0-255) selected in the UI
        /// </summary>
        public string SelectedMS110CTLC
        {
            get { return _selectedMS110CTLC; }
            set
            {
                if (_selectedMS110CTLC != value)
                {
                    _selectedMS110CTLC = value;
                    RaisePropertyChanged("SelectedMS110CTLC");
                }
            }
        }

        private string _selectedMS110CPreamble;
        /// <summary>
        /// The MS110C preamble value (0-32) selected in the UI
        /// </summary>
        public string SelectedMS110CPreamble
        {
            get { return _selectedMS110CPreamble; }
            set
            {
                if (_selectedMS110CPreamble != value)
                {
                    _selectedMS110CPreamble = value;
                    RaisePropertyChanged("SelectedMS110CPreamble");
                }
            }
        }

        private string _snr;
        /// <summary>
        /// The RapidM SNR
        /// </summary>
        public string SNR
        {
            get { return _snr; }
        }

        private string _ber;
        /// <summary>
        /// The RapidM bit error rate
        /// </summary>
        public string BER
        {
            get { return _ber; }
        }

        private string _modulation;
        /// <summary>
        /// The RapidM current waveform mode active in the radio (psk/ms110c/etc)
        /// </summary>
        public string Modulation
        {
            get { return _modulation; }
            set
            {
                if (_modulation != value)
                {
                    _modulation = value;
                    RaisePropertyChanged("Modulation");
                }
            }
        }

        private string _currentPSKWaveform;
        /// <summary>
        /// The RapidM PSK Waveform Mode that is currently active in the radio
        /// </summary>
        public string CurrentPSKWaveform
        {
            get { return _currentPSKWaveform; }
            set
            {
                if (_currentPSKWaveform != value)
                {
                    _currentPSKWaveform = value;
                    RaisePropertyChanged("CurrentPSKWaveform");
                }
            }
        }

        private string _currentPSKRate;
        /// <summary>
        /// The RapidM PSK baud rate that is currently active in the radio
        /// </summary>
        public string CurrentPSKRate
        {
            get { return _currentPSKRate; }
            set
            {
                if (_currentPSKRate != value)
                {
                    _currentPSKRate = value;
                    RaisePropertyChanged("CurrentPSKRate");
                }
            }
        }

        private string _currentPSKInterleaver;
        /// <summary>
        /// The RapidM Interleaver that is currently active in the radio
        /// </summary>
        public string CurrentPSKInterleaver
        {
            get { return _currentPSKInterleaver; }
            set
            {
                if (_currentPSKInterleaver != value)
                {
                    _currentPSKInterleaver = value;
                    RaisePropertyChanged("CurrentPSKInterleaver");
                }
            }
        }

        private string _currentMS110CWaveform;
        /// <summary>
        /// The RapidM MS110C Waveform Mode that is currently active in the radio
        /// </summary>
        public string CurrentMS110CWaveform
        {
            get { return _currentMS110CWaveform; }
            set
            {
                if (_currentMS110CWaveform != value)
                {
                    _currentMS110CWaveform = value;
                    RaisePropertyChanged("CurrentMS110CWaveform");
                }
            }
        }

        private string _currentMS110CBandwidth;
        /// <summary>
        /// The RapidM MS110c bandwidth that is curently active in the radio
        /// </summary>
        public string CurrentMS110CBandwidth
        {
            get { return _currentMS110CBandwidth; }
            set
            {
                if (_currentMS110CBandwidth != value)
                {
                    _currentMS110CBandwidth = value;
                    RaisePropertyChanged("CurrentMS110CBandwidth");
                }
            }
        }

        private string _currentPSKENC;
        /// <summary>
        /// The PSK ENC value that is curently active in the radio
        /// </summary>
        public string CurrentPSKENC
        {
            get { return _currentPSKENC; }
            set
            {
                if (_currentPSKENC != value)
                {
                    _currentPSKENC = value;
                    RaisePropertyChanged("CurrentPSKENC");
                }
            }
        }

        private string _currentPSKISB;
        /// <summary>
        /// The PSK ISB value that is curently active in the radio
        /// </summary>
        public string CurrentPSKISB
        {
            get { return _currentPSKISB; }
            set
            {
                if (_currentPSKISB != value)
                {
                    _currentPSKISB = value;
                    RaisePropertyChanged("CurrentPSKISB");
                }
            }
        }

        private string _currentMS110CInterleaver;
        /// <summary>
        /// The MS110C interleaver value that is curently active in the radio
        /// </summary>
        public string CurrentMS110CInterleaver
        {
            get { return _currentMS110CInterleaver; }
            set
            {
                if (_currentMS110CInterleaver != value)
                {
                    _currentMS110CInterleaver = value;
                    RaisePropertyChanged("CurrentMS110CInterleaver");
                }
            }
        }

        private string _currentMS110CConstraint;
        /// <summary>
        /// The MS110C Constraint value that is curently active in the radio
        /// </summary>
        public string CurrentMS110CConstraint
        {
            get { return _currentMS110CConstraint; }
            set
            {
                if (_currentMS110CConstraint != value)
                {
                    _currentMS110CConstraint = value;
                    RaisePropertyChanged("CurrentMS110CConstraint");
                }
            }
        }

        private string _currentMS110CTLC;
        /// <summary>
        /// The MS110C TLC value (0-255) that is curently active in the radio
        /// </summary>
        public string CurrentMS110CTLC
        {
            get { return _currentMS110CTLC; }
            set
            {
                if (_currentMS110CTLC != value)
                {
                    _currentMS110CTLC = value;
                    RaisePropertyChanged("CurrentMS110CTLC");
                }
            }
        }

        private string _currentMS110CPreamble;
        /// <summary>
        /// The MS110C preamble value (0-32) that is curently active in the radio
        /// </summary>
        public string CurrentMS110CPreamble
        {
            get { return _currentMS110CPreamble; }
            set
            {
                if (_currentMS110CPreamble != value)
                {
                    _currentMS110CPreamble = value;
                    RaisePropertyChanged("CurrentMS110CPreamble");
                }
            }
        }

        private string _currentMS110CDataRate;
        /// <summary>
        /// The MS110C data rate value that is curently active in the radio
        /// There is not a matching property for setting from the UI because
        /// that feature is not currently available
        /// </summary>
        public string CurrentMS110CDataRate
        {
            get { return _currentMS110CDataRate; }
            set
            {
                if (_currentMS110CDataRate != value)
                {
                    _currentMS110CDataRate = value;
                    RaisePropertyChanged("CurrentMS110CDataRate");
                }
            }
        }

        #endregion


        /// <summary>
        /// The delegate event handler for the RapidmMessageReceived event
        /// </summary>
        /// <param name="message">The string that was received</param>
        public delegate void MessageReceivedEventHandler(string message);
        /// <summary>
        /// This event is raised when a RapidM message is received
        /// </summary>
        public event MessageReceivedEventHandler MessageReceived;

        private void OnMessageReceived(string message)
        {
            if (MessageReceived != null)
                MessageReceived(message);
        }

        /// <summary>
        /// Sets up the RapidM waveform, rate and interleaver
        /// </summary>
        public void ConfigurePsk()
        {
            //enc and isb are usually disabled.
            //might come back to adjust this command to control what gets sent to the radio
            _radio.SendCommand("rapidm psk set wf=" + _selectedPskWaveform + " rate=" + _selectedRate + " il=" + _selectedInterleaver
                                + " enc=" + _selectedPSKENC + " isb=" + _selectedPSKISB);
        }

        public void ConfigureMs110c()
        {
            //tlc and preamble may need to: have fixed value/be set to radio defaults/not be sent to radio.
            //will revisit when decision is made
            _radio.SendCommand("rapidm ms110c set wf=" + _selectedMs110cWaveform + " bw=" + _selectedBandwidth + " il=" + _selectedMS110CInterleaver
                                + " tlc=" + _selectedMS110CTLC + " preamble=" + _selectedMS110CPreamble + " constraint=" + _selectedMS110CConstraint);
        }

        public void SendMessage(string message)
        {
            string encoded_string = message.Replace(' ', '\u007f');
            _radio.SendCommand("rapidm tx_message " + encoded_string);
        }

        internal void ParseStatus(string s)
        {
            string[] words = s.Split(' ');

            if (words.Length < 2)
            {
                Debug.WriteLine("RapidM::ParseStatus: Too few words -- min 2 (" + words + ")");
                return;
            }

            // handle non key/value pair type statuses
            if (words[0] == "rx_message")
            {
                string encoded_message = s.Substring("rx_message ".Length); // strip off the rx_message
                string message = encoded_message.Replace('\u007f', ' '); // decode the spaces
                OnMessageReceived(message); // fire the event
                return;
            }

            foreach (string kv in words)
            {

                string[] tokens = kv.Split('=');
                if (tokens.Length != 2)
                {
                    Debug.WriteLine("RapidM::ParseStatus: Invalid key/value pair (" + kv + ")");
                    continue;
                }

                string key = tokens[0];
                string value = tokens[1];

                switch (key.ToLower())
                {
                    case "wf":
                        {
                            //both psk and ms110c have waveforms
                            if (words[0] == "psk")
                            {
                                if (_currentPSKWaveform == value) continue;

                                _currentPSKWaveform = value;
                                RaisePropertyChanged("CurrentPSKWaveform");
                            }
                            if (words[0] == "ms110c")
                            {
                                if (_currentMS110CWaveform == value) continue;

                                _currentMS110CWaveform = value;
                                RaisePropertyChanged("CurrentMS110CWaveform");
                            }
                        }
                        break;

                    case "rate":
                        {
                            if (_currentPSKRate == value) continue;

                            _currentPSKRate = value;
                            RaisePropertyChanged("CurrentPSKRate");
                        }
                        break;

                    case "bw":
                        {
                            if (_currentMS110CBandwidth == value) continue;

                            _currentMS110CBandwidth = value;
                            RaisePropertyChanged("CurrentMS110CBandwidth");
                        }
                        break;

                    case "il": // interleaver
                        {
                            //both psk and ms110c have interleavers
                            if (words[0] == "psk")
                            {
                                if (_currentPSKInterleaver == value) continue;

                                _currentPSKInterleaver = value;
                                RaisePropertyChanged("CurrentPSKInterleaver");
                            }
                            if (words[0] == "ms110c")
                            {
                                if (_currentMS110CInterleaver == value) continue;

                                _currentMS110CInterleaver = value;
                                RaisePropertyChanged("CurrentMS110CInterleaver");
                            }
                        }
                        break;

                    case "constraint": // ms110c only
                        {
                            if (_currentMS110CConstraint == value) continue;

                            _currentMS110CConstraint = value;
                            RaisePropertyChanged("CurrentMS110CConstraint");
                        }
                        break;
                        
                    case "snr": // signal to noise ratio
                        {
                            if (_snr == value) continue;

                            _snr = value;
                            RaisePropertyChanged("SNR");
                        }
                        break;

                    case "ber": // bit error rate
                        {
                            if (_ber == value) continue;

                            _ber = value;
                            RaisePropertyChanged("BER");
                        }
                        break;

                    case "modulation":
                        {
                            if (_modulation == value) continue;

                            _modulation = value.ToLower();
                            RaisePropertyChanged("Modulation");
                        }
                        break;

                    case "enc":
                        {
                            if (_currentPSKENC == value) continue;

                            _currentPSKENC = value;
                            RaisePropertyChanged("CurrentPSKENC");
                        }
                        break;

                    case "isb":
                        {
                            if (_currentPSKISB == value) continue;

                            _currentPSKISB = value;
                            RaisePropertyChanged("CurrentPSKISB");
                        }
                        break;

                    case "tlc":
                        {
                            if (_currentMS110CTLC == value) continue;

                            _currentMS110CTLC = value;
                            RaisePropertyChanged("CurrentMS110CTLC");
                        }
                        break;

                    case "preamble":
                        {
                            if (_currentMS110CPreamble == value) continue;

                            _currentMS110CPreamble = value;
                            RaisePropertyChanged("CurrentMS110CPreamble");
                        }
                        break;

                    case "110c_data_rate":
                        {
                            if (_currentMS110CDataRate == value) continue;

                            _currentMS110CDataRate = value;
                            RaisePropertyChanged("CurrentMS110CDataRate");
                        }
                        break;
                }
            }
        }
    }
}
