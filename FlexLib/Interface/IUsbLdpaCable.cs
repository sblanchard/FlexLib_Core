namespace Flex.Smoothlake.FlexLib.Interface
{
    public interface IUsbLdpaCable : IUsbCable
    {
        LdpaBand Band { get; set; }
        bool IsPreampOn { get; set; }
    }
}
