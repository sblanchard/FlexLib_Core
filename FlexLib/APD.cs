// ****************************************************************************
///*!	\file APD.cs
// *	\brief APD model
// *
// *	\copyright	Copyright 2025 FlexRadio Systems.  All Rights Reserved.
// *				Unauthorized use, duplication or distribution of this software is
// *				strictly prohibited by law.
// */
// ****************************************************************************

using Flex.Smoothlake.FlexLib.Mvvm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Flex.Smoothlake.FlexLib;

public enum APDSamplerPorts
{
    INTERNAL,
    RX_A,
    XVTA,
    RX_B,
    XVTB
}

public record EqualizerStatus(bool enable, string ant, double freq, int rfpower);

public class APD(Radio radio) : ObservableObject
{
    private Radio _radio = radio;
    private static readonly Queue _statusQueue = new();
    private static System.Threading.Timer _statusApplyTimer = null;

    // Stops the static apply timer so a stale Radio reference is not invoked
    // after Disconnect(). Called from Radio.Disconnect().
    public void Exit()
    {
        _statusApplyTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        _statusApplyTimer = null;
    }

    public void ParseStatus(string s)
    {
        bool? activeSet = null;
        double freq = double.NaN;
        // Sentinel: -1 means "rfpower not reported" (older firmware). 0 is a valid radio reading.
        int rfpower = -1;
        string ant = null;
        string[] words = s.Split(' ');
        if (words.Length == 0) { return; }

        foreach (string kv in words)
        {
            string[] tokens = kv.Split('=');
            if (tokens[0] == "equalizer_reset") // Boolean flag - no value
            {
                Debug.WriteLine("Clearing all APD equalizers!");
                EqualizerActive = false;
                continue;
            }
            else if (tokens[0] == "sampler") // Subfield: "apd sampler tx_ant=… selected_sampler=… [valid_samplers=…]"
            {
                ParseSamplerStatus(s.Substring("sampler ".Length));
                return;
            }
            else if (tokens.Length != 2)
            {
                if (!string.IsNullOrEmpty(kv)) Debug.WriteLine($"APD::ParseStatus: Invalid key/value pair ({kv})");
                continue;
            }
            string key = tokens[0];
            string value = tokens[1];
            switch (key.ToLower())
            {
                case "ant":
                    ant = value;
                    break;
                case "configurable":
                    if (!byte.TryParse(value, out var configurableVal))
                    {
                        Debug.WriteLine($"APD::ParseStatus - configurable: Invalid value ({kv})");
                        continue;
                    }
                    _configurable = Convert.ToBoolean(configurableVal);
                    RaisePropertyChanged(nameof(Configurable));
                    break;
                case "enable":
                    if (!byte.TryParse(value, out var enableVal))
                    {
                        Debug.WriteLine($"APD::ParseStatus - enable: Invalid value ({kv})");
                        continue;
                    }
                    var enabled = Convert.ToBoolean(enableVal);
                    if (_enabled == enabled) break;
                    _enabled = enabled;
                    RaisePropertyChanged(nameof(Enabled));
                    break;
                case "equalizer_active":
                    if (!byte.TryParse(value, out var activeVal))
                    {
                        Debug.WriteLine($"APD::ParseStatus - equalizer_active: invalid value ({kv})");
                        continue;
                    }
                    activeSet = Convert.ToBoolean(activeVal);
                    break;
                case "freq":
                    if (!double.TryParse(value, out freq))
                    {
                        Debug.WriteLine($"APD::ParseStatus: Invalid frequency ({value})");
                        freq = double.NaN;
                        continue;
                    }
                    break;
                case "rfpower":
                    if (!int.TryParse(value, out rfpower))
                    {
                        Debug.WriteLine($"APD::ParseStatus - rfpower: value ({kv}) is invalid");
                        rfpower = -1;
                        continue;
                    }
                    break;
                case "sample_index":
                    if (!int.TryParse(value, out int index))
                    {
                        Debug.WriteLine($"Invalid APD Index: {value}");
                        continue;
                    }
                    if (GatherApdLogs)
                    {
                        FireAndForgetApdLog(index);
                    }
                    break;
            }
        }

        // Queue status messages and check after an interval; rapid slice movements otherwise lose sync.
        if (!(activeSet is null || double.IsNaN(freq) || string.IsNullOrEmpty(ant)))
        {
            QueueEqualizerActiveStatus(activeSet.Value, ant, freq, rfpower);
        }
    }

    #region APD log download (Alpha-licensed only)

    private bool _gatherApdLogs;
    public bool GatherApdLogs
    {
        get => _gatherApdLogs;
        set
        {
            if (_gatherApdLogs == value) return;
            _gatherApdLogs = value;
            RaisePropertyChanged(nameof(GatherApdLogs));
        }
    }

    private readonly string _apdLogDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\FlexRadio Systems\APD Logs\";

    private void FireAndForgetApdLog(int index)
    {
        _ = Task.Run(async () =>
        {
            try { await DownloadApdLog(index); }
            catch (Exception ex) { Debug.WriteLine($"APD log download failed: {ex}"); }
        });
    }

    private async Task DownloadApdLog(int index)
    {
        if (!_radio.IsAlphaLicensed)
        {
            return;
        }

        Debug.WriteLine($"Downloading APD log at index {index}");

        string portString;

        try
        {
            portString = await _radio.SendCommandAsync($"file download apd_log {index}");
        }
        catch (SmartSdrCommandErrorException ex)
        {
            Debug.WriteLine($"Failed to execute APD Download Command: {ex}");
            return;
        }

        if (!int.TryParse(portString, out int port))
        {
            Debug.WriteLine($"Invalid Port Number: {portString}");
            return;
        }

        var server = new TcpListener(IPAddress.Any, port);
        server.Start();

        Debug.WriteLine("Waiting for the APD Log download connection from the radio");
        using TcpClient client = await server.AcceptTcpClientAsync();

        Debug.WriteLine("Got a TCP connection from the radio");

        using NetworkStream stream = client.GetStream();

        Debug.WriteLine($"Downloading APD index {index}");
        Directory.CreateDirectory(_apdLogDirectory);
        var apdLogFile = $@"{_apdLogDirectory}\apd_log-{index}.zip";
        using FileStream outputFile = File.Open(apdLogFile, FileMode.Create);
        await stream.CopyToAsync(outputFile);

        Debug.WriteLine($"APD index {index} complete");

        server.Stop();
    }

    #endregion

    public void EqualizerActiveStatusApplyTimerTaskFunction(object state)
    {
        while (_statusQueue.Count != 0)
        {
            // Defensive cast: the static queue can outlive a Radio instance,
            // and a stale entry of a non-EqualizerStatus type would crash an unconditional cast.
            if (_statusQueue.Dequeue() is not EqualizerStatus temp)
                continue;
            Debug.WriteLine($"Parsing queued eq status - enable={temp.enable}, ant={temp.ant}, freq={temp.freq}, rfpower={temp.rfpower}.");
            ApplyEqualizerActiveStatus(temp.enable, temp.ant, temp.freq, temp.rfpower);
        }
    }

    private void ApplyEqualizerActiveStatus(bool enable, string ant, double freq, int rfpower)
    {
        if (ant == null || _radio == null || _radio.SliceList == null)
        {
            return;
        }

        // Prefer transmit-enabled slice; fall back to active slice.
        Slice temp = _radio.SliceList.FirstOrDefault(s => s.IsTransmitSlice) ?? _radio.ActiveSlice;

        if (temp is null)
        {
            Debug.WriteLine("APD::ApplyEqualizerActiveStatus: No slices to apply to.");
            return;
        }

        // Round to Hz precision (radio reports Hz; slice may carry sub-Hz).
        double roundedFreq = Math.Round(temp.Freq, 6);

        // rfpower==-1 means "not reported" (older firmware) — skip that part of the match.
        bool rfPowerMatches = rfpower == -1 || rfpower == _radio.RFPower;

        if (ant.Equals(temp.TXAnt, StringComparison.OrdinalIgnoreCase) && freq == roundedFreq && rfPowerMatches)
        {
            Debug.WriteLine($"APD::ApplyEqualizerActiveStatus: Updating APD status for slice {temp.Index}, freq={freq}, ant={ant}, rfpower={rfpower}.");
            if (enable)
                OnEqualizerActiveHeartbeat();
            else
                OnEqualizerCalibratingHeartbeat();
            EqualizerActive = enable;
        }
        else
        {
            Debug.WriteLine($"APD::ApplyEqualizerActiveStatus: No matching slice with freq={freq}, ant={ant}, rfpower={rfpower}. Current freq={temp.Freq}.");
        }
    }

    private void QueueEqualizerActiveStatus(bool enable, string ant, double freq, int rfpower)
    {
        if (null == _statusApplyTimer)
        {
            _statusApplyTimer = new System.Threading.Timer(EqualizerActiveStatusApplyTimerTaskFunction, null, 100, 150);
        }
        EqualizerStatus temp = new(enable, ant, freq, rfpower);
        _statusQueue.Enqueue(temp);
    }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            _radio.SendCommand("apd enable=" + Convert.ToByte(_enabled));
            RaisePropertyChanged(nameof(Enabled));
        }
    }

    private bool _configurable;
    public bool Configurable
    {
        get => _configurable;
        internal set
        {
            if (_configurable == value) return;
            _configurable = value;
            RaisePropertyChanged(nameof(Configurable));
        }
    }

    private bool _equalizerActive;
    public bool EqualizerActive
    {
        get => _equalizerActive;
        set
        {
            if (_equalizerActive == value) return;
            _equalizerActive = value;
            RaisePropertyChanged(nameof(EqualizerActive));
            RaisePropertyChanged(nameof(EqualizerCalibrating));
        }
    }

    public bool EqualizerCalibrating => !EqualizerActive;

    private bool _available;
    public bool Available
    {
        get => _available;
        set
        {
            if (_available == value) return;
            _available = value;
            RaisePropertyChanged(nameof(Available));
        }
    }

    public void EqualizerReset()
    {
        _radio?.SendCommand("apd reset");
    }

    #region Sampling (SmartSignal external-amplifier feedback path selection)

    public List<string> AvailableSamplerPortListANT1 { get; } = new() { nameof(APDSamplerPorts.INTERNAL) };
    public List<string> AvailableSamplerPortListANT2 { get; } = new() { nameof(APDSamplerPorts.INTERNAL) };
    public List<string> AvailableSamplerPortListXVTA { get; } = new() { nameof(APDSamplerPorts.INTERNAL) };
    public List<string> AvailableSamplerPortListXVTB { get; } = new() { nameof(APDSamplerPorts.INTERNAL) };

    private string _selectedSamplerPortANT1 = nameof(APDSamplerPorts.INTERNAL);
    public string SelectedSamplerPortANT1
    {
        get => _selectedSamplerPortANT1;
        set
        {
            if (value is null || _selectedSamplerPortANT1 == value) return;
            _selectedSamplerPortANT1 = value;
            _radio.SendCommand($"apd sampler tx_ant=ANT1 sample_port={_selectedSamplerPortANT1}");
            RaisePropertyChanged(nameof(SelectedSamplerPortANT1));
        }
    }

    private string _selectedSamplerPortANT2 = nameof(APDSamplerPorts.INTERNAL);
    public string SelectedSamplerPortANT2
    {
        get => _selectedSamplerPortANT2;
        set
        {
            if (value is null || _selectedSamplerPortANT2 == value) return;
            _selectedSamplerPortANT2 = value;
            _radio.SendCommand($"apd sampler tx_ant=ANT2 sample_port={_selectedSamplerPortANT2}");
            RaisePropertyChanged(nameof(SelectedSamplerPortANT2));
        }
    }

    private string _selectedSamplerPortXVTA = nameof(APDSamplerPorts.INTERNAL);
    public string SelectedSamplerPortXVTA
    {
        get => _selectedSamplerPortXVTA;
        set
        {
            if (value is null || _selectedSamplerPortXVTA == value) return;
            _selectedSamplerPortXVTA = value;
            _radio.SendCommand($"apd sampler tx_ant=XVTA sample_port={_selectedSamplerPortXVTA}");
            RaisePropertyChanged(nameof(SelectedSamplerPortXVTA));
        }
    }

    private string _selectedSamplerPortXVTB = nameof(APDSamplerPorts.INTERNAL);
    public string SelectedSamplerPortXVTB
    {
        get => _selectedSamplerPortXVTB;
        set
        {
            if (value is null || _selectedSamplerPortXVTB == value) return;
            _selectedSamplerPortXVTB = value;
            _radio.SendCommand($"apd sampler tx_ant=XVTB sample_port={_selectedSamplerPortXVTB}");
            RaisePropertyChanged(nameof(SelectedSamplerPortXVTB));
        }
    }

    private void ParseSamplerStatus(string status)
    {
        string[] tokens = status.Split(' ');
        string txAnt = null;
        string currentSampler = null;
        string[] availableSamplers = Array.Empty<string>();

        if (tokens.Length == 0) return;

        foreach (var token in tokens)
        {
            string[] kv = token.Split('=');
            if (kv.Length != 2)
            {
                if (!string.IsNullOrEmpty(token)) Trace.WriteLine($"Invalid key-value pair: {token}");
                continue;
            }

            string key = kv[0];
            string val = kv[1];

            switch (key.ToLower())
            {
                case "tx_ant":
                    if (!string.IsNullOrEmpty(val)) txAnt = val;
                    else
                    {
                        Trace.WriteLine("Empty tx_ant value.");
                        Trace.WriteLine("Expected: apd sampler tx_ant=<ANT1|ANT2|XVTA|XVTB> " +
                            "selected_sampler=<INVALID|XVTA|RX_A|XVTB|RX_B>" +
                            "[valid_samplers=<XVTA|RX_A/XVTB|RX_B>]");
                        return;
                    }
                    break;

                case "selected_sampler":
                    if (!string.IsNullOrEmpty(val)) currentSampler = val;
                    else
                    {
                        Trace.WriteLine("Empty selected_sampler value.");
                        Trace.WriteLine("Expected: apd sampler tx_ant=<ANT1|ANT2|XVTA|XVTB> " +
                            "selected_sampler=<INVALID|XVTA|RX_A|XVTB|RX_B>" +
                            "[valid_samplers=<XVTA|RX_A/XVTB|RX_B>]");
                        return;
                    }
                    break;

                case "valid_samplers":
                    {
                        string[] samplers = val.Split(',');
                        if (samplers.Length > 0) availableSamplers = samplers;
                        else
                        {
                            Trace.WriteLine("No valid_samplers list");
                            continue;
                        }
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(txAnt))
        {
            Trace.WriteLine("APD sampler status missing tx_ant");
            return;
        }

        if (availableSamplers.Length > 0)
        {
            ChangeAvailableSamplerList(txAnt, availableSamplers);
        }

        switch (txAnt.ToLower())
        {
            case "ant1":
                _selectedSamplerPortANT1 = AvailableSamplerPortListANT1.Contains(currentSampler)
                    ? currentSampler : nameof(APDSamplerPorts.INTERNAL);
                RaisePropertyChanged(nameof(SelectedSamplerPortANT1));
                break;

            case "ant2":
                _selectedSamplerPortANT2 = AvailableSamplerPortListANT2.Contains(currentSampler)
                    ? currentSampler : nameof(APDSamplerPorts.INTERNAL);
                RaisePropertyChanged(nameof(SelectedSamplerPortANT2));
                break;

            case "xvta":
                _selectedSamplerPortXVTA = AvailableSamplerPortListXVTA.Contains(currentSampler)
                    ? currentSampler : nameof(APDSamplerPorts.INTERNAL);
                RaisePropertyChanged(nameof(SelectedSamplerPortXVTA));
                break;

            case "xvtb":
                _selectedSamplerPortXVTB = AvailableSamplerPortListXVTB.Contains(currentSampler)
                    ? currentSampler : nameof(APDSamplerPorts.INTERNAL);
                RaisePropertyChanged(nameof(SelectedSamplerPortXVTB));
                break;

            default:
                Trace.WriteLine($"Unknown tx_ant value: {txAnt}");
                break;
        }
    }

    private void ChangeAvailableSamplerList(string tx_ant, string[] samplers)
    {
        if (string.IsNullOrEmpty(tx_ant))
        {
            Trace.WriteLine("No entry for transmit antenna");
            return;
        }

        switch (tx_ant.ToLower())
        {
            case "ant1":
                AvailableSamplerPortListANT1.Clear();
                AvailableSamplerPortListANT1.Add(nameof(APDSamplerPorts.INTERNAL));
                foreach (var port in samplers) AvailableSamplerPortListANT1.Add(port);
                RaisePropertyChanged(nameof(AvailableSamplerPortListANT1));
                break;

            case "ant2":
                AvailableSamplerPortListANT2.Clear();
                AvailableSamplerPortListANT2.Add(nameof(APDSamplerPorts.INTERNAL));
                foreach (var port in samplers) AvailableSamplerPortListANT2.Add(port);
                RaisePropertyChanged(nameof(AvailableSamplerPortListANT2));
                break;

            case "xvta":
                AvailableSamplerPortListXVTA.Clear();
                AvailableSamplerPortListXVTA.Add(nameof(APDSamplerPorts.INTERNAL));
                foreach (var port in samplers) AvailableSamplerPortListXVTA.Add(port);
                RaisePropertyChanged(nameof(AvailableSamplerPortListXVTA));
                break;

            case "xvtb":
                AvailableSamplerPortListXVTB.Clear();
                AvailableSamplerPortListXVTB.Add(nameof(APDSamplerPorts.INTERNAL));
                foreach (var port in samplers) AvailableSamplerPortListXVTB.Add(port);
                RaisePropertyChanged(nameof(AvailableSamplerPortListXVTB));
                break;
        }
    }

    #endregion

    #region Heartbeat events (SMART-11907)

    public delegate void EqualizerActiveHeartbeatEventHandler();
    public event EqualizerActiveHeartbeatEventHandler EqualizerActiveHeartbeat;
    private void OnEqualizerActiveHeartbeat() => EqualizerActiveHeartbeat?.Invoke();

    public delegate void EqualizerCalibratingHeartbeatEventHandler();
    public event EqualizerCalibratingHeartbeatEventHandler EqualizerCalibratingHeartbeat;
    private void OnEqualizerCalibratingHeartbeat() => EqualizerCalibratingHeartbeat?.Invoke();

    #endregion
}
