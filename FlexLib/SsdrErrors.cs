using System.ComponentModel;

namespace Flex.Smoothlake.FlexLib;

public enum SsdrErrors : uint
{
    [Description("IP port is in use")]
    IpPortInUse = 0x500000A9,
}
