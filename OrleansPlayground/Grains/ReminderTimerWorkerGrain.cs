using Orleans.Timers;

namespace OrleansPlayground.Grains;

public interface IReminderTimerWorkerGrain : IMyGrain
{
    Task RegisterReminderAsync(TimeSpan reminderDue, TimeSpan reminderPeriod, TimeSpan timerDue, TimeSpan timerPeriod);
}

public class ReminderTimerWorkerGrain(
    IGrainFactory grainFactory,
    IReminderRegistry remindersRegistry,
    ITimerRegistry timerRegistry,
    ILogger<ReminderTimerWorkerGrain> logger) : Grain, IReminderTimerWorkerGrain, IRemindable
{
    private IDisposable? _timer;
    private string? _reminderName;
    private const string ReminderName = "remindertimer";

    public async override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var reminders = await remindersRegistry.GetReminders(this.GetGrainId());
        var reminder = reminders.FirstOrDefault();

        if (reminder != null)
        {
            _reminderName = reminder.ReminderName;

            (TimeSpan timerDue, TimeSpan timerPeriod) = ParseTimerPeriodFromName(_reminderName);

            StartTimer(timerDue, timerPeriod);
        }

        logger.LogInformation(
            "[Activation] {Type} activated. GrainId={GrainId}, Time={Time:O}",
            GetType().Name,
            this.GetPrimaryKeyString(),
            DateTime.UtcNow);

        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        StopTimer();
        logger.LogInformation(
            "[Deactivation] {Type} deactivated. GrainId={GrainId}, Reason={Reason}, Time={Time:O}",
            GetType().Name,
            this.GetPrimaryKeyString(),
            reason.Description,
            DateTime.UtcNow);
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (_timer == null)
        {
            (TimeSpan timerDue, TimeSpan timerPeriod) = ParseTimerPeriodFromName(reminderName);
            StartTimer(timerDue, timerPeriod);
            logger.LogInformation("{Type}: Timer started from reminder tick.", GetType().Name);
        }
        else
        {
            logger.LogInformation("{Type}: Reminder tick received, but timer is already running.", GetType().Name);
        }

        return Task.CompletedTask;
    }

    public async Task RegisterReminderAsync(TimeSpan reminderDue, TimeSpan reminderPeriod, TimeSpan timerDue, TimeSpan timerPeriod)
    {
        _reminderName = $"{ReminderName}-{(int)timerDue.TotalSeconds}-{(int)timerPeriod.TotalSeconds}";

        await remindersRegistry.RegisterOrUpdateReminder(
            this.GetGrainId(), _reminderName, reminderDue, reminderPeriod);

        StartTimer(timerDue, timerPeriod);

        await grainFactory.GetGrain<IWorkerCatalogGrain>("stateless-with-timer-catalog")
            .AddAsync(this.GetPrimaryKeyString());

    }

    public async Task UnregisterReminderAsync()
    {
        var reminders = await remindersRegistry.GetReminders(this.GetGrainId());
        var reminder = reminders.FirstOrDefault();

        if (reminder != null)
        {
            await remindersRegistry.UnregisterReminder(this.GetGrainId(), reminder);
        }

        await grainFactory.GetGrain<IWorkerCatalogGrain>("stateless-with-timer-catalog")
                        .RemoveAsync(this.GetPrimaryKeyString());

        StopTimer();
    }

    private void StartTimer(TimeSpan timerDue, TimeSpan timerPeriod)
    {
        if (_timer != null)
            return;

        var opts = new GrainTimerCreationOptions
        {
            DueTime = timerDue,
            Period = timerPeriod
        };

        logger.LogInformation("{Type}: Registering timer (period={Period})", GetType().Name, opts.Period);

        _timer = timerRegistry.RegisterGrainTimer<object>(
            this.GrainContext,
            async (_, ct) =>
            {
                try
                {
                    await ProcessAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Type}: Error in ProcessAsync", GetType().Name);
                }
            },
            null!,
            opts);
    }

    private async Task ProcessAsync()
    {
        await Task.Delay(
            Random.Shared.Next(5, 20) > 18 ?
                Random.Shared.Next(800, 3200) :
                Random.Shared.Next(50, 800));

        logger.LogInformation(
            "[Tick] Grain={Id}, Time={Time:O}",
            this.GetPrimaryKeyString(),
            DateTime.UtcNow);

        //MigrateOnIdle();
    }

    private (TimeSpan timerDue, TimeSpan timerPeriod) ParseTimerPeriodFromName(string reminderName)
    {
        var slices = reminderName.Split('-');
        return (TimeSpan.FromSeconds(int.Parse(slices[slices.Length - 1])), TimeSpan.FromSeconds(int.Parse(slices[slices.Length - 2])));
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
        logger.LogInformation("{Type}: Timer stopped", GetType().Name);
    }
}
