// ****************************************************************************
///*!	\file HAAPI.cs
// *	\brief Ham-Aided API: external amplifier monitoring + fault aggregator
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

namespace Flex.Smoothlake.FlexLib
{
    public enum AmplifierMode
    {
        STANDBY,
        OPERATE
    }

    public class HAAPI : ObservableObject
    {
        private readonly Radio _radio;

        public delegate void HaapiFaultEventHandler(string noun, string reason);
        public event HaapiFaultEventHandler HaapiFault;
        private void OnHaapiFault(string noun, string reason)
        {
            HaapiFault?.Invoke(noun, reason);
        }

        public delegate void HaapiWarningEventHandler(string noun, string reason);
        public event HaapiWarningEventHandler HaapiWarning;
        private void OnHaapiWarning(string noun, string reason)
        {
            HaapiWarning?.Invoke(noun, reason);
        }

        public delegate void HaapiWarningClearedEventHandler(string noun);
        public event HaapiWarningClearedEventHandler HaapiWarningCleared;
        private void OnHaapiWarningCleared(string noun)
        {
            HaapiWarningCleared?.Invoke(noun);
        }

        public HAAPI(Radio radio)
        {
            _radio = radio;
            _meterList = new List<Meter>();
        }

        /// <summary>
        /// Sends a ha_api command. Do NOT include the "ha_api" prefix; this method prepends it.
        /// </summary>
        public void SendHaapiCommand(string haapi_cmd)
        {
            _radio.SendCommand($"ha_api {haapi_cmd}");
        }

        private void HandleHaapiModeReply(int seq, uint resp_val, string s)
        {
            if (resp_val == 0)
            {
                RaisePropertyChanged(nameof(AmpMode));
            }
            else
            {
                // Mode change failed — revert to standby and propagate.
                AmpMode = AmplifierMode.STANDBY;
            }
        }

        public void HaapiChangeMode(AmplifierMode mode)
        {
            _radio.SendReplyCommand(new ReplyHandler(HandleHaapiModeReply),
                $"ha_api amplifier set mode={mode.ToString().ToLower()}");

            // Save new mode but defer PropertyChanged until command success is confirmed.
            _ampMode = mode;
        }

        #region Parse Routines

        public void ParseStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                Debug.WriteLine("HAAPI::ParseStatus: Empty HAAPI status message");
                return;
            }

            switch (status.Split(' ')[0].ToLower())
            {
                case "amplifier":
                    ParseAmplifierStatus(status.Substring("amplifier ".Length));
                    break;

                case "fault":
                    ParseFaultStatus(status.Substring("fault ".Length));
                    break;

                case "warning":
                    ParseWarningStatus(status.Substring("warning ".Length));
                    break;

                case "bit":
                case "combiner":
                case "module":
                case "pdu":
                case "system":
                    // Reserved subroutes; not yet bound.
                    break;
            }
        }

        private void ParseAmplifierStatus(string status)
        {
            string[] kvs = status.Split(' ');
            if (kvs.Length < 1)
            {
                Debug.WriteLine("HAAPI::ParseAmplifierStatus: No amplifier status to parse");
                return;
            }

            foreach (string kv in kvs)
            {
                string[] parts = kv.Split('=');
                if (parts.Length != 2) continue;
                string key = parts[0];
                string val = parts[1];

                switch (key.ToLower())
                {
                    case "frequency":
                        if (!float.TryParse(val, out float freq) || float.IsNaN(freq))
                        {
                            Debug.WriteLine($"HAAPI::ParseAmplifierStatus: Invalid frequency value {val}");
                            continue;
                        }
                        _ampFrequency = freq;
                        RaisePropertyChanged(nameof(AmpFrequency));
                        break;

                    case "module_gain":
                        if (!float.TryParse(val, out float gain) || float.IsNaN(gain))
                        {
                            Debug.WriteLine($"HAAPI::ParseAmplifierStatus: Invalid module gain value {val}");
                            continue;
                        }
                        _ampModuleGain = gain;
                        RaisePropertyChanged(nameof(AmpModuleGain));
                        break;

                    case "xmit_state":
                        if (!uint.TryParse(val, out uint xs))
                        {
                            Debug.WriteLine($"HAAPI::ParseAmplifierStatus: Invalid xmit_state {val}");
                            continue;
                        }
                        _ampXmitState = Convert.ToBoolean(xs);
                        RaisePropertyChanged(nameof(AmpXmitState));
                        break;

                    case "mode":
                        if (val == "standby") _ampMode = AmplifierMode.STANDBY;
                        else if (val == "operate") _ampMode = AmplifierMode.OPERATE;
                        else
                        {
                            Debug.WriteLine($"HAAPI::ParseAmplifierStatus: Invalid amplifier mode {val}");
                            _ampMode = AmplifierMode.STANDBY;
                        }
                        RaisePropertyChanged(nameof(AmpMode));
                        break;
                }
            }
        }

        private void ParseFaultStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                Debug.WriteLine("HAAPI::ParseFaultStatus: Empty fault status message");
                return;
            }

            // Format: "type=detection source=combiner state=OK|FAULTED"
            string[] kvs = status.Split(' ');
            string fault_type = null, fault_source = null, fault_state = null;

            foreach (string kv in kvs)
            {
                if (string.IsNullOrEmpty(kv)) continue;
                string[] parts = kv.Split('=');
                if (parts.Length != 2) continue;

                switch (parts[0].ToLower())
                {
                    case "type": fault_type = parts[1]; break;
                    case "source": fault_source = parts[1]; break;
                    case "state": fault_state = parts[1]; break;
                }
            }

            if (string.IsNullOrEmpty(fault_state) || !fault_state.Equals("FAULTED", StringComparison.OrdinalIgnoreCase))
                return;

            string noun = string.IsNullOrEmpty(fault_source)
                ? (string.IsNullOrEmpty(fault_type) ? "Unknown" : fault_type)
                : (string.IsNullOrEmpty(fault_type) ? fault_source : $"{fault_source} ({fault_type})");

            string reason = $"Fault detected - {fault_state}";
            if (!string.IsNullOrEmpty(fault_type) && !string.IsNullOrEmpty(fault_source))
                reason = $"{fault_type} fault on {fault_source}";
            else if (!string.IsNullOrEmpty(fault_type))
                reason = $"{fault_type} fault";
            else if (!string.IsNullOrEmpty(fault_source))
                reason = $"Fault on {fault_source}";

            OnHaapiFault(noun, reason);
        }

        private void ParseWarningStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                Debug.WriteLine("HAAPI::ParseWarningStatus: Empty warning status message");
                return;
            }

            // Format: "type=detection source=combiner state=OK|WARNING"
            string[] kvs = status.Split(' ');
            string warning_type = null, warning_source = null, warning_state = null;

            foreach (string kv in kvs)
            {
                if (string.IsNullOrEmpty(kv)) continue;
                string[] parts = kv.Split('=');
                if (parts.Length != 2) continue;

                switch (parts[0].ToLower())
                {
                    case "type": warning_type = parts[1]; break;
                    case "source": warning_source = parts[1]; break;
                    case "state": warning_state = parts[1]; break;
                }
            }

            string noun = string.IsNullOrEmpty(warning_source)
                ? (string.IsNullOrEmpty(warning_type) ? "Unknown" : warning_type)
                : (string.IsNullOrEmpty(warning_type) ? warning_source : $"{warning_source} ({warning_type})");

            // OK clears any prior warning for this noun.
            if (!string.IsNullOrEmpty(warning_state) && warning_state.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                OnHaapiWarningCleared(noun);
                return;
            }

            if (string.IsNullOrEmpty(warning_state) || !warning_state.Equals("WARNING", StringComparison.OrdinalIgnoreCase))
                return;

            string reason = $"Warning detected - {warning_state}";
            if (!string.IsNullOrEmpty(warning_type) && !string.IsNullOrEmpty(warning_source))
                reason = $"{warning_type} warning on {warning_source}";
            else if (!string.IsNullOrEmpty(warning_type))
                reason = $"{warning_type} warning";
            else if (!string.IsNullOrEmpty(warning_source))
                reason = $"Warning on {warning_source}";

            OnHaapiWarning(noun, reason);
        }

        #endregion

        #region Metering

        private readonly List<Meter> _meterList;

        public Meter FindMeterByIndex(int index)
        {
            lock (_meterList)
                return _meterList.FirstOrDefault(m => m.Index == index);
        }

        public Meter FindMeterByName(string s)
        {
            lock (_meterList)
                return _meterList.FirstOrDefault(m => m.Name == s);
        }

        internal void AddMeter(Meter m)
        {
            lock (_meterList)
            {
                if (!_meterList.Contains(m))
                    _meterList.Add(m);
            }

            switch (m.Name.ToLower())
            {
                case "lpf_fwd_pwr":         m.DataReady += HaapiFwdPwr_DataReady; break;
                case "lpf_swr":             m.DataReady += HaapiVswr_DataReady; break;
                case "hv_sply_out_volt":    m.DataReady += HaapiHv_DataReady; break;
                case "hv_sply_out_current": m.DataReady += HaapiCurrent_DataReady; break;
                case "hv_sply_temp":        m.DataReady += HaapiTempPsu_DataReady; break;
                case "pa_0_temp":           m.DataReady += HaapiTempPa0_DataReady; break;
                case "pa_1_temp":           m.DataReady += HaapiTempPa1_DataReady; break;
                case "drv_temp":            m.DataReady += HaapiTempDrvA_DataReady; break;
                case "comb_bal_load_temp":  m.DataReady += HaapiTempComb_DataReady; break;
                case "comb_hpf_load_tmp":   m.DataReady += HaapiTempHpf_DataReady; break;
            }
        }

        internal void RemoveMeter(Meter m)
        {
            lock (_meterList)
            {
                if (!_meterList.Contains(m)) return;
                _meterList.Remove(m);

                switch (m.Name.ToLower())
                {
                    case "lpf_fwd_pwr":         m.DataReady -= HaapiFwdPwr_DataReady; break;
                    case "lpf_swr":             m.DataReady -= HaapiVswr_DataReady; break;
                    case "hv_sply_out_volt":    m.DataReady -= HaapiHv_DataReady; break;
                    case "hv_sply_out_current": m.DataReady -= HaapiCurrent_DataReady; break;
                    case "hv_sply_temp":        m.DataReady -= HaapiTempPsu_DataReady; break;
                    case "pa_0_temp":           m.DataReady -= HaapiTempPa0_DataReady; break;
                    case "pa_1_temp":           m.DataReady -= HaapiTempPa1_DataReady; break;
                    case "drv_temp":            m.DataReady -= HaapiTempDrvA_DataReady; break;
                    case "comb_bal_load_temp":  m.DataReady -= HaapiTempComb_DataReady; break;
                    case "comb_hpf_load_tmp":   m.DataReady -= HaapiTempHpf_DataReady; break;
                }
            }
        }

        private void HaapiHv_DataReady(Meter meter, float data) => OnHaapiHVDataReady(data);
        private void HaapiCurrent_DataReady(Meter meter, float data) => OnHaapiCurrentDataReady(data);
        private void HaapiTempPsu_DataReady(Meter meter, float data) => OnHaapiTempPsuDataReady(data);
        private void HaapiTempPa0_DataReady(Meter meter, float data) => OnHaapiTempPa0DataReady(data);
        private void HaapiTempPa1_DataReady(Meter meter, float data) => OnHaapiTempPa1DataReady(data);
        private void HaapiTempDrvA_DataReady(Meter meter, float data) => OnHaapiTempDrvADataReady(data);
        private void HaapiTempComb_DataReady(Meter meter, float data) => OnHaapiTempCombDataReady(data);
        private void HaapiTempHpf_DataReady(Meter meter, float data) => OnHaapiTempHpfDataReady(data);
        private void HaapiFwdPwr_DataReady(Meter meter, float data) => OnHaapiFwdPwrDataReady(data);
        private void HaapiVswr_DataReady(Meter meter, float data) => OnHaapiVswrDataReady(data);

        public delegate void MeterDataReadyEventHandler(float data);

        public event MeterDataReadyEventHandler HaapiFwdPwrDataReady;
        private void OnHaapiFwdPwrDataReady(float data) => HaapiFwdPwrDataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiVswrDataReady;
        private void OnHaapiVswrDataReady(float data) => HaapiVswrDataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiHVDataReady;
        private void OnHaapiHVDataReady(float data) => HaapiHVDataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiCurrentDataReady;
        private void OnHaapiCurrentDataReady(float data) => HaapiCurrentDataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiTempPsuDataReady;
        private void OnHaapiTempPsuDataReady(float data) => HaapiTempPsuDataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiTempPa0DataReady;
        private void OnHaapiTempPa0DataReady(float data) => HaapiTempPa0DataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiTempPa1DataReady;
        private void OnHaapiTempPa1DataReady(float data) => HaapiTempPa1DataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiTempDrvADataReady;
        private void OnHaapiTempDrvADataReady(float data) => HaapiTempDrvADataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiTempCombDataReady;
        private void OnHaapiTempCombDataReady(float data) => HaapiTempCombDataReady?.Invoke(data);

        public event MeterDataReadyEventHandler HaapiTempHpfDataReady;
        private void OnHaapiTempHpfDataReady(float data) => HaapiTempHpfDataReady?.Invoke(data);

        #endregion

        #region TX Amplifier

        private float _ampFrequency;
        public float AmpFrequency
        {
            get => _ampFrequency;
            set
            {
                if (value == _ampFrequency) return;
                _ampFrequency = value;
                RaisePropertyChanged(nameof(AmpFrequency));
            }
        }

        private float _ampModuleGain;
        public float AmpModuleGain
        {
            get => _ampModuleGain;
            set
            {
                if (value == _ampModuleGain) return;
                _ampModuleGain = value;
                RaisePropertyChanged(nameof(AmpModuleGain));
            }
        }

        private bool _ampXmitState;
        public bool AmpXmitState
        {
            get => _ampXmitState;
            set
            {
                if (value == _ampXmitState) return;
                _ampXmitState = value;
                RaisePropertyChanged(nameof(AmpXmitState));
            }
        }

        private AmplifierMode _ampMode = AmplifierMode.STANDBY;
        public AmplifierMode AmpMode
        {
            get => _ampMode;
            set
            {
                if (value == _ampMode) return;
                _ampMode = value;
                RaisePropertyChanged(nameof(AmpMode));
            }
        }

        #endregion
    }
}
