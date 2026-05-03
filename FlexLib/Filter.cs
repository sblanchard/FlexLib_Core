// ****************************************************************************
///*!	\file Filter.cs
// *	\brief Filter Helper class to ease databinding
// *
// *	\copyright	Copyright 2012-2026 FlexRadio Systems.  All Rights Reserved.
// *				Unauthorized use, duplication or distribution of this software is
// *				strictly prohibited by law.
// */
// ****************************************************************************

using System;

namespace Flex.Smoothlake.FlexLib
{
    /// <summary>
    /// Filter Preset Mode group enums
    /// </summary>
    public enum FilterPresetModeGroup
    {
        SSB = 0,
        CW = 1,
        AM = 2, // Includes DFM as well, other FM modes do not allow changeable filter widths
        Digital = 3,
        RTTY = 4,
    }

    public static class FilterPresetEnumHelpers
    {
        /// <summary>
        /// Helper function to convert slice mode to mode group enum
        /// </summary>
        public static FilterPresetModeGroup GetModeGroupFromSliceMode(string slice_mode)
        {
            return slice_mode.Trim().ToLowerInvariant() switch
            {
                "usb" or "lsb" => FilterPresetModeGroup.SSB,
                "cw" => FilterPresetModeGroup.CW,
                "am" or "ame" or "dfm" or "dsb" or "dstr" or "sam" => FilterPresetModeGroup.AM,
                "digl" or "digu" or "fdv" => FilterPresetModeGroup.Digital,
                "rtty" => FilterPresetModeGroup.RTTY,
                _ => throw new ArgumentException($"Invalid Slice mode: {slice_mode}")
            };
        }

        /// <summary>
        /// Helper function to convert mode group string to mode group enum
        /// </summary>
        public static FilterPresetModeGroup GetModeGroupFromString(string mode_group)
        {
            return mode_group.Trim().ToLowerInvariant() switch
            {
                "ssb" => FilterPresetModeGroup.SSB,
                "cw" => FilterPresetModeGroup.CW,
                "am" => FilterPresetModeGroup.AM,
                "digital" => FilterPresetModeGroup.Digital,
                "rtty" => FilterPresetModeGroup.RTTY,
                _ => throw new ArgumentException($"Invalid mode group string: {mode_group}")
            };
        }

        /// <summary>
        /// Gets the mode group string from the mode group to be sent in save and reset commands
        /// </summary>
        public static string GetModeGroupString(FilterPresetModeGroup mode_group)
        {
            return mode_group switch
            {
                FilterPresetModeGroup.SSB => "ssb",
                FilterPresetModeGroup.CW => "cw",
                FilterPresetModeGroup.AM => "am",
                FilterPresetModeGroup.Digital => "digital",
                FilterPresetModeGroup.RTTY => "rtty",
                _ => "Invalid",
            };
        }
    }

    [Serializable]
    public class Filter
    {
        public string Name { get; set; } = string.Empty;
        public int LowCut { get; set; }
        public int HighCut { get; set; }
        public bool IsFavorite { get; set; }

        public Filter(string name, int low, int high)
        {
            Name = name;
            LowCut = low;
            HighCut = high;
        }

        // Parameterless constructor for serialization purposes
        private Filter()
        {
        }

        /// <summary>
        /// Updates the filter fields with the input parameters.
        /// </summary>
        public void Update(string presetName, int low, int high)
        {
            if (low > high) return;
            if (presetName.Length > 4) return;
            if (Name == presetName && LowCut == low && HighCut == high) return;

            Name = presetName;
            LowCut = low;
            HighCut = high;
        }
    }
}
