namespace Flex.Smoothlake.FlexLib.Interface
{
    public interface IUsbBcdCable : IUsbCable
    {
        UsbCableFreqSource Source { get; set; }
        BcdCableType BcdType { get; set; }
        bool IsActiveHigh { get; set; }
        string SelectedRxAnt { get; set; }
        string SelectedTxAnt { get; set; }
        string SelectedSlice { get; set; }
    }
}
