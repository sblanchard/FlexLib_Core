// ****************************************************************************
///*!	\file DisplayMarker.cs
// *	\brief Represents a single Display Marker (band plan segment or user marker)
// *
// *	\copyright	Copyright 2026 FlexRadio Systems.  All Rights Reserved.
// *				Unauthorized use, duplication or distribution of this software is
// *				strictly prohibited by law.
// */
// ****************************************************************************

using System;
using System.Diagnostics;
using Flex.Smoothlake.FlexLib.Mvvm;
using Util;

namespace Flex.Smoothlake.FlexLib;

public class DisplayMarker : ObservableObject
{
    private readonly Radio _radio;

    internal DisplayMarker(Radio radio, string group, uint id)
    {
        _radio = radio;
        _group = group;
        _id = id;
    }

    private readonly string _group;
    public string Group => _group;

    private readonly uint _id;
    public uint ID => _id;

    private string _label = string.Empty;
    public string Label
    {
        get => _label;
        set
        {
            if (_label == value) return;
            _label = value;
            RaisePropertyChanged(nameof(Label));
        }
    }

    private double _startFreq;
    public double StartFreq
    {
        get => _startFreq;
        set
        {
            if (_startFreq == value) return;
            _startFreq = value;
            RaisePropertyChanged(nameof(StartFreq));
        }
    }

    private double _stopFreq;
    public double StopFreq
    {
        get => _stopFreq;
        set
        {
            if (_stopFreq == value) return;
            _stopFreq = value;
            RaisePropertyChanged(nameof(StopFreq));
        }
    }

    private string _colorName = string.Empty;
    public string ColorName
    {
        get => _colorName;
        set
        {
            if (_colorName == value) return;
            _colorName = value;
            RaisePropertyChanged(nameof(ColorName));
        }
    }

    private uint _opacity;
    public uint Opacity
    {
        get => _opacity;
        set
        {
            if (_opacity == value) return;
            _opacity = value;
            RaisePropertyChanged(nameof(Opacity));
        }
    }

    public bool IsIARUGroup =>
        _group != null && _group.StartsWith("IARU", StringComparison.OrdinalIgnoreCase);

    public override string ToString() =>
        $"DisplayMarker: {Group}/{ID} \"{Label}\" {StartFreq:F6}-{StopFreq:F6} MHz {ColorName}";

    public void StatusUpdate(string s)
    {
        string[] words = s.Split(' ');

        foreach (string kv in words)
        {
            string[] tokens = kv.Split('=');
            if (tokens.Length != 2)
            {
                Debug.WriteLine($"DisplayMarker::StatusUpdate: Invalid key/value pair ({kv})");
                continue;
            }

            string key = tokens[0];
            string value = tokens[1];

            switch (key.ToLower())
            {
                case "label":
                    _label = value.Trim('"');
                    RaisePropertyChanged(nameof(Label));
                    break;
                case "start_freq":
                    if (!StringHelper.TryParseDouble(value, out double startTemp))
                    {
                        Debug.WriteLine($"DisplayMarker::StatusUpdate - start_freq: Invalid value ({kv})");
                        continue;
                    }
                    _startFreq = startTemp;
                    RaisePropertyChanged(nameof(StartFreq));
                    break;
                case "stop_freq":
                    if (!StringHelper.TryParseDouble(value, out double stopTemp))
                    {
                        Debug.WriteLine($"DisplayMarker::StatusUpdate - stop_freq: Invalid value ({kv})");
                        continue;
                    }
                    _stopFreq = stopTemp;
                    RaisePropertyChanged(nameof(StopFreq));
                    break;
                case "color":
                    _colorName = value;
                    RaisePropertyChanged(nameof(ColorName));
                    break;
                case "opacity":
                    if (!uint.TryParse(value, out uint opTemp))
                    {
                        Debug.WriteLine($"DisplayMarker::StatusUpdate - opacity: Invalid value ({kv})");
                        continue;
                    }
                    _opacity = opTemp;
                    RaisePropertyChanged(nameof(Opacity));
                    break;
                default:
                    Debug.WriteLine($"DisplayMarker::StatusUpdate: Unknown key ({key})");
                    break;
            }
        }
    }
}
