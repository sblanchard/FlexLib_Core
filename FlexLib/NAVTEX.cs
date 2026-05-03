// ****************************************************************************
///*!	\file NAVTEX.cs
// *	\brief NAVTEX waveform client model
// *
// *	\copyright	Copyright 2025 FlexRadio Systems.  All Rights Reserved.
// *				Unauthorized use, duplication or distribution of this software is
// *				strictly prohibited by law.
// */
// ****************************************************************************

using Flex.Smoothlake.FlexLib.Mvvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Flex.Smoothlake.FlexLib;

public class NAVTEX : ObservableObject
{
    private readonly Radio _radio;
    private readonly List<NAVTEXMsg> _msgs;
    public List<NAVTEXMsg> Msgs => _msgs;
    private readonly Dictionary<int, NAVTEXMsg> _pendingMsgs;
    private readonly object _msgsLock = new();

    private NAVTEXStatus _status;
    public NAVTEXStatus Status
    {
        get => _status;
        set
        {
            if (value == _status) return;
            _status = value;
            RaisePropertyChanged(nameof(Status));
        }
    }

    public NAVTEX(Radio radio)
    {
        _radio = radio;
        Status = NAVTEXStatus.Inactive;
        _msgs = new List<NAVTEXMsg>();
        _pendingMsgs = new Dictionary<int, NAVTEXMsg>();
    }

    public bool TryToggleNAVTEX(uint broadcastFreqHz = INTERNATIONAL_BROADCAST_FREQ_HZ)
    {
        if (_radio is null) return false;

        // Deactivating: kill the one and only NAVTEX slice and leave nothing behind.
        if (NAVTEXStatus.Active == Status)
        {
            if (_radio.SliceList.Count == 0)
            {
                _radio.SendCommand("slice create");
                _radio.SendCommand("slice remove 0");
            }
            else
            {
                // Get out of NT mode first to force the WF callback to return to inactive.
                _radio.SendCommand("slice s 0 mode=DIGU");
                _radio.SendCommand("slice remove 0");
            }

            // Don't keep creating a fresh panadapter on each cycle.
            List<Panadapter> temp = _radio.PanadapterList;
            temp.ToList()?.ForEach(p => p?.Close());
            _radio.PanadapterList?.Clear();
            _radio.SendCommand("sub slice all"); // Force GUI updates
            return true;
        }

        // Activating is heavy-handed: a NAVTEX user typically wants exactly one slice.
        if (_radio.SliceList.Count == 0)
        {
            _radio.SendCommand("slice create");
        }
        else
        {
            for (uint i = 1; i < _radio.SliceList.Count; i++)
            {
                _radio.SendCommand($"slice remove {i}");
            }
        }
        _radio.SendCommand($"slice tune 0 {(double)broadcastFreqHz / 1000000}");
        _radio.SendCommand("slice s 0 mode=NT");
        _radio.SendCommand("sub slice 0"); // Force GUI update of the new slice freq
        return true;
    }

    public void SendResponseHandler(int seq, uint resp_val, string s)
    {
        Trace.WriteLine($"Got response to 'navtex send' - seq={seq}, resp_val={resp_val}, s={s}");
        if (string.IsNullOrEmpty(s))
        {
            Trace.WriteLine($"Response message with command seq num {seq} did not include an index");
            return;
        }

        lock (_msgsLock)
        {
            if (_pendingMsgs.TryGetValue(seq, out NAVTEXMsg m))
            {
                m.Status = NAVTEXMsgStatus.Queued;
                m.Idx = uint.Parse(s);
                Msgs.Add(m);
                _pendingMsgs.Remove(seq);
                RaisePropertyChanged(nameof(Msgs));
            }
            else
            {
                Trace.WriteLine($"Failed to find a pending message with command seq num {seq}");
            }
        }
    }

    public void Send(NAVTEXMsg m)
    {
        // Serial is optional — radio auto-increments / rolls over from the last used value.
        string serial = m.Serial.HasValue ? $"serial_num={m.Serial.Value}" : string.Empty;

        string msg = $"navtex send tx_ident={m.TxIdent} subject_indicator={m.SubjInd} {serial} msg_text=\"{m.MsgStr}\"";
        Trace.WriteLine($"Sending NAVTEX msg - {msg}");
        lock (_msgsLock)
        {
            int seq = _radio.SendReplyCommand(new ReplyHandler(SendResponseHandler), msg);
            Trace.WriteLine($"Storing as a pending msg with seq {seq}");
            _pendingMsgs[seq] = m;
        }
    }

    private void ParseSentStatus(IEnumerable<string> s)
    {
        uint idx = 0;
        uint serial = 0;
        IEnumerable<string[]> temp = s.Where(str => !string.IsNullOrEmpty(str)).Select(strings => strings?.Split('='));
        Dictionary<string, string> kvs = temp?.ToDictionary(pair => pair[0], pair => pair[1]);

        // Each msg is identified by both index and serial: index is unique (radio-generated atomically),
        // serial may have rolled over if we asked for one already in use.
        if (kvs.TryGetValue("idx", out string idx_str))
        {
            if (!uint.TryParse(idx_str, out idx))
            {
                Trace.WriteLine($"Failed to parse index from {idx_str}");
                return;
            }
        }
        if (kvs.TryGetValue("serial_num", out string serial_str))
        {
            if (!uint.TryParse(serial_str, out serial))
            {
                Trace.WriteLine($"Failed to parse serial from {serial_str}");
                return;
            }
        }

        // Redundant status — each index is guaranteed unique, ignore if we already saw a Sent for it.
        if (Msgs?.Count(m => (m.Idx == idx) && m.Status == NAVTEXMsgStatus.Sent) != 0)
        {
            Trace.WriteLine($"Redundant status for index {idx}, which is already sent - discarding.");
            return;
        }

        string dateTime = DateTime.UtcNow.ToString("yyyy-MM-ddZHH:mm:ss");

        lock (_msgsLock)
        {
            if (Msgs?.Count(m => m.Idx == idx) == 0)
            {
                Trace.WriteLine($"No matching message with idx {idx}; recording as error.");
                Msgs.Add(new NAVTEXMsg(dateTime, idx, serial, null, null, null, NAVTEXMsgStatus.Error));
            }
            else
            {
                NAVTEXMsg msgInFlight = Msgs.First(m => (m.Idx == idx));
                if (msgInFlight.Status != NAVTEXMsgStatus.Queued)
                {
                    Trace.WriteLine($"Sent update for msg idx {idx} that wasn't queued; recording as error.");
                    msgInFlight.Status = NAVTEXMsgStatus.Error;
                }
                else
                {
                    msgInFlight.Status = NAVTEXMsgStatus.Sent;
                }
                msgInFlight.DateTime = dateTime;
                msgInFlight.Serial = serial;
            }
        }
        RaisePropertyChanged(nameof(Msgs));
    }

    public void ParseStatus(string s)
    {
        // Sent message update
        if (s.Contains("sent"))
        {
            string[] words = s.Split(' ');
            ParseSentStatus(words.Skip(1).Take(words.Length - 1).ToArray());
            return;
        }

        // Global NAVTEX status update
        foreach (string kv in s.Split(' '))
        {
            if (kv.StartsWith("status="))
            {
                string v = kv.Split('=')[1];
                if (!Enum.TryParse(v, true, out _status))
                {
                    Trace.WriteLine($"Failed to parse status value: {v}");
                    _status = NAVTEXStatus.Error;
                }
                RaisePropertyChanged(nameof(Status));
            }
            return;
        }
    }

    public const uint INTERNATIONAL_BROADCAST_FREQ_HZ = 518000;          // No FEC
    public const uint LOCAL_BROADCAST_FREQ_HZ = 490000;                  // No FEC
    public const uint MARINE_SAFETY_INFORMATION_BROADCAST_FREQ_HZ = 4209500; // FEC
}

public enum NAVTEXStatus
{
    Error,
    Inactive,
    Active,
    Transmitting,
    QueueFull,
    Unlicensed,
}

public enum NAVTEXMsgStatus
{
    Error,
    Pending,
    Queued,
    Sent,
}

public class NAVTEXMsg
{
    public NAVTEXMsg(string? dateTime, uint? idx, uint? serial, char? txIdent, char? subjInd, string? msgStr, NAVTEXMsgStatus status)
    {
        DateTime = dateTime;
        Idx = idx;
        Serial = serial;
        TxIdent = txIdent;
        SubjInd = subjInd;
        MsgStr = msgStr;
        Status = status;
    }
    public string? DateTime { get; set; }
    public uint? Idx { get; set; }
    public uint? Serial { get; set; }
    public char? TxIdent { get; set; }
    public char? SubjInd { get; set; }
    public string? MsgStr { get; set; }
    public NAVTEXMsgStatus Status { get; set; }
}
