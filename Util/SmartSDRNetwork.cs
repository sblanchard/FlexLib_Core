using System.ComponentModel;
using System.Runtime.CompilerServices;
using Netify;

namespace Util;

public class SmartSDRNetwork : INotifyPropertyChanged, INetworkObserver
{
    private bool _isInternetAvailable;
    private readonly NetworkStatusNotifier _networkStatusNotifier = new();

    public SmartSDRNetwork()
    {
        _networkStatusNotifier.AddObserver(this);
        IsInternetAvailable = _networkStatusNotifier.CheckNow() == ConnectivityStatus.Connected;
        _networkStatusNotifier.Start();
    }

    public bool IsInternetAvailable
    {
        get => _isInternetAvailable;
        set
        {
            if (_isInternetAvailable == value)
            {
                return;
            }

            _isInternetAvailable = value;
            OnPropertyChanged();
        }
    }

    public void ConnectivityChanged(ConnectivityStatus status)
    {
        IsInternetAvailable = status == ConnectivityStatus.Connected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
