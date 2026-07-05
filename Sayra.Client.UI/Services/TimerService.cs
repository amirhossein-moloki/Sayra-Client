using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Sayra.Client.UI.Services;

public class TimerService : IDisposable
{
    private readonly Subject<long> _tickSubject = new();
    private IDisposable? _timerSubscription;

    public TimerService()
    {
        _timerSubscription = Observable.Interval(TimeSpan.FromSeconds(1))
            .Subscribe(_tickSubject);
    }

    public IObservable<long> Ticks => _tickSubject.AsObservable();

    public void Dispose()
    {
        _timerSubscription?.Dispose();
        _tickSubject.Dispose();
    }
}
