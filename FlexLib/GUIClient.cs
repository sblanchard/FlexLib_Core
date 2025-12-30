// ****************************************************************************
///*!	\file GUIClient.cs
// *	\brief Represents a single GUI Client for the radio
// *
// *	\copyright	Copyright 2018 FlexRadio Systems.  All Rights Reserved.
// *				Unauthorized use, duplication or distribution of this software is
// *				strictly prohibited by law.
// *
// *	\date 2018-10-18
// *	\author Eric Wachsmann, KE5DTO
// */
// ****************************************************************************

using Flex.Smoothlake.FlexLib.Mvvm;


namespace Flex.Smoothlake.FlexLib
{
    public class GUIClient(
        uint handle,
        string clientId,
        string program,
        string station,
        bool isLocalPtt,
        bool isAvailable = true)
        : ObservableObject
    {
        private bool _isThisClient;
        public bool IsThisClient
        {
            get { return _isThisClient; }
            internal set
            {
                if (_isThisClient != value)
                {
                    _isThisClient = value;
                    RaisePropertyChanged("IsThisClient");
                }
            }
        }


        private uint _clientHandle = handle;
        public uint ClientHandle
        {
            get { return _clientHandle; }
            set
            {
                if (_clientHandle != value)
                {
                    _clientHandle = value;
                    RaisePropertyChanged("ClientHandle");
                }
            }
        }

        private string _clientID = clientId;
        public string ClientID
        {
            get { return _clientID; }
            set
            {
                if (_clientID != value)
                {
                    _clientID = value;
                    RaisePropertyChanged("ClientID");
                }
            }
        }

        private string _program = program;
        public string Program
        {
            get { return _program; }
            set
            {
                if (_program != value)
                {
                    _program = value;
                    RaisePropertyChanged("Program");
                }
            }
        }

        private string _station = station;
        public string Station
        {
            get { return _station; }
            set
            {
                if (_station != value)
                {
                    _station = value;
                    RaisePropertyChanged("Station");
                }
            }
        }

        private bool _isLocalPtt = isLocalPtt;
        public bool IsLocalPtt
        {
            get { return _isLocalPtt; }
            set
            {
                if (_isLocalPtt != value)
                {
                    _isLocalPtt = value;
                    RaisePropertyChanged("IsLocalPtt");
                }
            }
        }

        public bool IsAvailable { get; } = isAvailable;

        private Slice _transmitSlice;
        public Slice TransmitSlice
        {
            get { return _transmitSlice; }
            set
            {
                if (_transmitSlice != value)
                {
                    _transmitSlice = value;
                    RaisePropertyChanged("TransmitSlice");
                }
            }
        }

    }
}
